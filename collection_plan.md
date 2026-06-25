# Containment & Collections — Implementation Plan

Status: proposed
Author: design discussion, 2026-06-25

## 0. One-paragraph summary

Today the app has **two unrelated containment mechanisms**: `Item ↔ IndexedFile`
(folder grouping, decided by `ItemAssociationService`) and `IndexedFile ↔ SubResource`
(streams, chapters, *and* archive entries, all flattened onto the parent file). This
plan unifies them. Archive entries become **virtual files** (`IndexedFile` rows with an
archive locator) so a `.zip` is processed exactly like a folder; `SubResource` is
narrowed to genuine *intra-file parts* (streams/chapters). On top of that, a new
recursive **`Collection`** entity gives a structural home to multi-level groupings
(Show → Season → Episode), including collection-level art and metadata, which today
have nowhere to live. The association pass changes from a flat per-folder `GroupBy` into
a **bottom-up tree fold** that classifies every directory/archive node as a Collection,
an Item-bundle, or a passthrough — using one shared classifier regardless of
folder-vs-archive.

---

## 1. Goals

1. **One model for containment.** Folders and archives are "a directory of files";
   streams/chapters are "parts of one file". Stop modeling zip-album and folder-album
   differently.
2. **Multi-level structural collections.** Show → Season → Episode, Artist → Album →
   Track, arbitrary depth. Collections own their own art and metadata.
3. **Archives are first-class.** Explode archive contents into the catalog; an archive
   can resolve to a Collection, an Item (game/setup), or a passthrough folder — same
   rules as a real folder.
4. **Reuse existing machinery.** Items, attributes, dedup/checksums, `.librarian.meta`
   write-back, and faceted browsing should "just work" on virtual files and collections
   with minimal special-casing.
5. **Preserve manual corrections.** `RoleSource.Manual` semantics extend to collection
   membership and collection kind.

### Non-goals (this plan)

- Writing metadata *inside* archives (archives are treated read-only; sidecars live
  beside them).
- Replacing the faceted browsing system. Faceted "virtual collections" stay; structural
  Collections complement them.
- A general virtual-filesystem mount layer beyond archives (deferred).

---

## 2. Current state (verified)

- `Item` (`Librarian.Core/Model/Item.cs`): `Id`, `RoleSource`, `ICollection<IndexedFile> Files`.
  One Item → many Files. No parent/child. Item-level metadata = attributes **promoted
  onto the primary file**.
- `IndexedFile` (`.../Model/IndexedFile.cs`): unique `Path`, `Exists`, `ItemId?`,
  `Role` (Primary/Sidecar/Companion), `RoleSource`, `Size/Created/Modified`,
  `PrefixHash`, nav collections to the typed attribute tables. **Directories are also
  `IndexedFile` rows** but are filtered out of Items.
- `SubResource` (`.../Model/SubResource.cs`): `FileId`, `Name`, `InternalId`, `Kind`
  (Unknown/Stream/Chapter/**EmbeddedFile**). Archive entries currently land here, capped
  at **100** per archive (`TikaProvider`), with their metadata attached to the *archive*
  file via `SubResourceId`.
- `AttributeBase` (`.../Model/AttributeBase.cs`): `FileId` (effectively required),
  `SubResourceId?`, `AttributeDefinitionId`, `Editable`, `ProviderId`. Typed subclasses:
  Text/Integer/Float/Date/Blob (+ TS via Float).
- `ItemAssociationService` (`Librarian/Services/ItemAssociationService.cs`): flat
  `GroupBy(ParentDir)`; detects app-bundles, single-content, multi-content folders;
  `ResetAsync` wipes Auto rows each run, keeps Manual. Runs from `IndexingJob.Execute`
  **after** `IndexAll()`.
- `.librarian.meta` (`Librarian.Metadata/Metadata/MetadataSerializer.cs`,
  `MetadataService.cs`): **XML**, folder-level, `<librarian version="1"><file name="…">…`,
  keyed by **filename**, override-only, applied with `SidecarProvider` GUID; already has
  a `<subResources>` sub-element shape.
- Browsing (`LibraryController`/`LibraryService`, `LibraryCategories.cs`): faceted drill
  via `CategoryView.Path = DrillLevel[]`; **TV Shows view already drills Series→Season**
  on attributes. Item Viewer in `MetadataController.PopulateItem` (Content/Sidecars/
  Resources panes + `BestCover`).
- Migrations: `Librarian/DB/Migrations`, applied at startup via `Database.Migrate()`;
  `AttributeDefinition` seeded with `.HasData`, ids 1–121, curation id-space ≥ 1,000,000.

---

## 3. Target model

### 3.1 Physical / addressing layer

**`IndexedFile` gains a source + locator** so it can represent real *and* virtual files:

```csharp
public enum FileSource { Filesystem = 0, ArchiveEntry = 1 }

// new columns on IndexedFile
public FileSource Source { get; set; } = FileSource.Filesystem;
public int? ParentFileId { get; set; }      // archive IndexedFile this entry lives in
public IndexedFile? ParentFile { get; set; }
public string? InternalPath { get; set; }    // path within the archive, e.g. "Disc1/03.flac"
```

- **DECIDED (Q1): explicit columns are the canonical locator, not an overloaded `Path`.**
  Resolution routes through (`Source`, `ParentFileId`, `InternalPath`) under a composite
  unique index `UNIQUE(ParentFileId, InternalPath)` — no consumer ever parses a delimited
  string, so real filenames containing any delimiter are safe. `Path` is kept only as a
  synthesized **display/routing convenience** string (`<archive-path>!/<internal>`,
  e.g. `music/al.zip!/Disc1/03.flac`); it stays unique but is not the lookup mechanism for
  virtual files.
- Recursion is free: a zip-in-zip is an `ArchiveEntry` whose own entries are
  `ArchiveEntry` rows pointing at it via `ParentFileId`.
- The filesystem watcher only ever sees `Filesystem` files; `ArchiveEntry` rows are
  created/destroyed by the archive-expansion step (§7), keyed off the archive's change
  state. Filesystem resolution / disk I/O must check `Source` and route virtual files
  through the archive byte-reader (§7.3) instead of `File.OpenRead`.

**`SubResource` is narrowed to true intra-file parts.** `SubResourceKind.EmbeddedFile`
is retired from the live path; only `Stream`/`Chapter` (+ future `EmbeddedThumbnail`)
remain. Streams/chapters keep rolling their metadata up to their file. They are **never**
catalog nodes.

### 3.2 Catalog layer — recursive Collections + Item leaves

New entity:

```csharp
public enum CollectionKind { Generic = 0, Show = 1, Season = 2, Album = 3, Series = 4 /* books */, AppBundle = 5 }

public class Collection
{
    public int Id { get; set; }
    public int? ParentCollectionId { get; set; }     // self-ref → arbitrary nesting
    public Collection? Parent { get; set; }
    public ICollection<Collection> Children { get; set; } = new List<Collection>();
    public ICollection<Item> Items { get; set; } = new List<Item>();
    public ICollection<IndexedFile> Files { get; set; } = new List<IndexedFile>(); // collection-level art/nfo

    public CollectionKind Kind { get; set; } = CollectionKind.Generic;
    public RoleSource RoleSource { get; set; } = RoleSource.Auto; // Manual = user-pinned membership/kind
    public string? SourcePath { get; set; }          // folder or archive path this was derived from (stable identity)
}
```

Extend `Item`:

```csharp
public int? ParentCollectionId { get; set; }         // the collection this item belongs to
public Collection? ParentCollection { get; set; }
```

Extend `IndexedFile` (collection-level companions/sidecars — the season poster, the
`tvshow.nfo`):

```csharp
public int? CollectionId { get; set; }               // file owned directly by a collection (art/nfo)
public Collection? Collection { get; set; }
```

A file is owned by **at most one** of `Item` or `Collection`. `Role`
(Primary/Sidecar/Companion) still applies; for collection-owned files only
Sidecar (nfo) and Companion (art) are meaningful.

This solves the long-standing "shared art has no owner" problem: album covers,
`folder.jpg`, `tvshow.nfo`, `season.nfo` attach to the Collection instead of floating as
orphan companions.

### 3.3 Metadata ownership — attributes can key to a Collection

`AttributeBase` gains an optional collection owner so collection metadata (Show
title/genre/year, Season number) uses the *same* typed-attribute machinery as files:

```csharp
public int? FileId { get; set; }          // relax [Required]
public int? SubResourceId { get; set; }
public int? CollectionId { get; set; }    // NEW — exactly one of File / Collection set
public Collection? Collection { get; set; }
```

Constraint: exactly one of {`FileId`, `CollectionId`} non-null (DB check constraint +
guard in the writer). `SubResourceId` only valid alongside `FileId`.

Collection metadata is **promoted** from collection sidecars (`tvshow.nfo`,
`season.nfo`, an `.opf` series file, or `folder.jpg` → cover blob) exactly as Item
metadata is promoted from file sidecars today — same `PromoteAttributesAsync`/
`PromoteContentAsync` pattern, just targeting a `CollectionId` instead of a primary
`FileId`.

---

## 4. Decisions (recorded from discussion)

| Topic | Decision | Consequence |
|---|---|---|
| Archives | **Explode.** An archive is classified by the same rules as a folder → Collection / Item / passthrough. | §7 expansion + §6 classifier run on archive subtrees. |
| Materialization | **Full** (no 100-cap). The cap only existed because entry metadata was glued onto the archive; with entries as their own files that problem is gone. | Remove `embeddedLimit`; add per-archive guard rails (§7.4, §11). |
| Locator | **Sidecar next to the archive** — can't write inside a read-only zip. | `.librarian.meta` keying extended to archive-internal paths (§8). |
| Locator schema (Q1) | **Explicit columns** (`Source`/`ParentFileId`/`InternalPath`), `Path` display-only. | Composite unique index; no string-parsing (§3.1, §5). |
| Dedup | **Yes, including across the archive boundary**, size-gated at **1 MiB** (`Checksum:MinSize`). | Checksum/PrefixHash on virtual files ≥ 1 MiB (§7.5). |
| Albums (Q4) | **Fold album folders into `Collection(Album)`** in C6 (per-track Items as children; cover/nfo collection-owned). | Replaces orphan-cover behavior; staged late (§10 C6, §11.4). |
| Re-extraction | Tika unpacks archives natively. For **other** providers (ExifTool, meta-cli): **extract entry → temp file → run providers → delete**. | New temp-materialization helper (§7.3). |

---

## 5. Schema changes & migrations

All migrations under `Librarian/DB/Migrations`, applied at startup. Each is additive and
nullable-first so existing rows survive; backfill happens on the next association pass.

1. **`AddFileSourceAndLocator`** — `IndexedFile.Source`, `ParentFileId` (FK → IndexedFile,
   `OnDelete: Cascade` so deleting an archive drops its entries), `InternalPath`. Composite
   `UNIQUE(ParentFileId, InternalPath)` index — the canonical locator (Q1). `Path` kept
   unique for display/routing but resolution uses the columns.
2. **`AddCollections`** — `Collection` table; self-ref FK `ParentCollectionId`
   (`OnDelete: Restrict` / handle in code); `Item.ParentCollectionId` (FK → Collection,
   `SetNull`); `IndexedFile.CollectionId` (FK → Collection, `SetNull`). Indexes on all
   three FKs and on `Collection.SourcePath`.
3. **`AttributeOwnerCollection`** — add `CollectionId` to every typed attribute table;
   relax `FileId` to nullable; add a check constraint
   `num_nonnull(FileId, CollectionId) = 1`. Index `CollectionId` per table.
4. **`RetireEmbeddedFileSubResources`** — data migration: delete
   `SubResource` rows with `Kind = EmbeddedFile` and their attributes (they will be
   rebuilt as virtual files on next reindex). Keep the enum value for back-compat reads.

`OnModelCreating` additions (in `DatabaseContext`):
- `IndexedFile` → `ParentFile`/`Children` (`WithMany`, `HasForeignKey(ParentFileId)`).
- `Collection` self-ref `Parent`/`Children`; `Items` (`WithOne(ParentCollection)`);
  `Files` (`WithOne(Collection)`).
- Per attribute entity: optional `Collection` nav + the check constraint via
  `ToTable(t => t.HasCheckConstraint(...))`.

No new seed `AttributeDefinition`s are strictly required, but add a few collection-scoped
defs (e.g. `Show / Title`, `Show / Year`, `Season / Number`, `Collection / Cover`) in
`MetadataAttributes.csv` (next sequential ids) so promotion has canonical targets. Keep
them in the < 1,000,000 seed range.

---

## 6. Association algorithm v2 (bottom-up tree fold)

Replace the flat `GroupBy(ParentDir)` with a recursive classification over the **merged
tree of directories + archives**. The unit at each node is a directory-like container
(real folder, the library root, or an archive root/sub-dir-within-archive).

### 6.1 Build the container tree

- Nodes = every directory `IndexedFile` + every archive root + every directory *within*
  an archive (derived from entry `InternalPath`s) + the synthetic library root.
- Children of a node = its immediate files + immediate sub-directories. An archive file,
  once expanded, is treated as a **directory node** (its entries are its children); the
  archive's own `IndexedFile` row becomes the collection/item's `SourcePath` anchor.

### 6.2 Classify each node, bottom-up

Process leaves first so a parent can see what its children resolved to. For a node `N`
with classified children:

```
classify(N):
  files   = immediate non-companion content files in N         (existing Sidecars.Classify)
  subcols = children that resolved to Collection
  subitems= children that resolved to a single Item (single-content subfolder)

  # (a) App/Game bundle  — unchanged rule, now also fires inside archives
  if N has a content-exe AND a CompanionResource:
      -> Item(AppBundle owns subtree)            # ProcessAppBundle, extended to archives

  # (b) Passthrough single-content folder
  elif files.count == 1 and subcols+subitems == 0:
      -> Item (primary = that file; rest = sidecars/companions)   # SingleItem

  # (c) Multi-content leaf (e.g. album of loose tracks)
  elif files.count > 1 and subcols+subitems == 0:
      if looks like an Album (audio) or other groupable set:
          -> Collection(Album) { Items = one per track; Files = shared art/nfo }
      else:
          -> per-content Items, parent stays passthrough        # PerContentItems
             (back-compat default; promote to Collection later)

  # (d) Container of containers  -> a structural Collection
  elif (subcols + subitems) >= 2  OR  (mixes sub-collections with loose items):
      -> Collection(kind = inferKind(N))
         { Children = subcols, Items = subitems + loose-content Items,
           Files = collection-level art/nfo (cover/folder/poster/*.nfo) }
```

### 6.3 `inferKind(N)` — naming heuristics (refinement, not the backbone)

The **structure** (a folder of folders) is what makes something a Collection; naming only
chooses the *kind/labels*:

- Child folders matching `^(season|series|s)\s*\d+`, `specials`, `extras` → children are
  `Season`, `N` is `Show`.
- `N` is a single artist/discography folder whose children are albums → `Generic`
  (or `Artist` if we add the kind) with `Album` children.
- Otherwise `Generic`.

All of this is `Auto`; `RoleSource.Manual` on a Collection/Item pins kind & membership
across re-runs (mirrors existing file-role behavior).

### 6.4 Persistence & reset

- Extend `ResetAsync` to also delete `Auto` Collections and null out `Auto`
  `ParentCollectionId` / `CollectionId`, plus delete collection-scoped promoted
  attributes (by `ProviderId == PromotionProvider`). Manual rows survive.
- `SourcePath` gives a stable identity so a Manual collection re-binds to the same
  folder/archive after reindex even though Auto collections are rebuilt.
- Collection-level promotion pass: after items are built, promote `*.nfo` / `folder.*`
  → collection attributes/cover, analogous to the existing sidecar promotion loop.
- Triggered from `IndexingJob.Execute` after `IndexAll()` (unchanged ordering), now
  preceded by the archive-expansion step (§7) which must complete during `IndexAll`.

---

## 7. Archive pipeline

### 7.1 Detection & expansion

- During `MetadataService.UpdateMetadata`, when a file is an archive (by MIME/extension,
  reuse Tika/`Sidecars`), run an **ArchiveExpansionStep** that enumerates entries and
  upserts one `ArchiveEntry` `IndexedFile` per entry (full materialization), setting
  `ParentFileId`, `InternalPath`, `Path = archive!/internal`, `Size`, timestamps.
- Removed/renamed entries (archive changed): diff against existing children and soft- or
  hard-delete stale rows. Gate the whole step on the archive's change detection
  (timestamp/size) so untouched archives aren't re-expanded.
- After expansion, each entry is queued for normal metadata extraction like any file.

### 7.2 Metadata for entries

- **Tika** already returns embedded content/metadata per entry — route each entry's Tika
  result to *its own* `IndexedFile` (not glued to the archive). This is the change that
  removes the need for the 100-cap.
- **Content/FTS**: entry text content → `IndexedFileContents` on the entry row.

### 7.3 Re-extraction for non-Tika providers (temp materialization)

```
IArchiveByteSource.OpenEntry(entry) -> Stream         # streams bytes from the archive
TempMaterializer.With(entry, async tempPath => {       # writes to $CLAUDE/tmp-like scratch
    run ExifTool(tempPath); run meta-cli(tempPath);    # providers that need a real path
}); // deletes tempPath in finally
```

- Batch by archive (open once, extract the entries that need ExifTool/meta-cli, run,
  clean up) to amortize open cost.
- Skip providers that don't apply (e.g. meta-cli only for AV entries).

### 7.4 Guard rails (replacing the cap)

- No hard entry cap, but: a configurable **max-expanded-entries-per-archive** safety
  ceiling (default high, e.g. 50k) that, if exceeded, falls back to "index archive as a
  single opaque Item" and **logs what was skipped** (no silent truncation).
- Skip well-known non-content noise inside archives via `Sidecars` (same classifier).

### 7.5 Dedup across the boundary

- Entries are `IndexedFile`s, so `ChecksumService` works unchanged: compute `PrefixHash`
  and full `Checksum` (attr id 121) on entries **≥ `Checksum:MinSize` = 1 MiB** (DECIDED
  Q3); smaller files skipped to avoid extracting tiny entries just to hash them. The same
  gate applies to loose files for consistency.
- Cross-boundary dedup falls out: the loose `foo.iso` and `archive.zip!/foo.iso` share a
  checksum → surfaced by the existing duplicates view. Decide UX for "duplicate where one
  copy is inside an archive" (informational; don't offer to delete inside the zip).

---

## 8. Write-back / `.librarian.meta`

- **Filesystem files**: unchanged (folder-level `.librarian.meta`, keyed by filename).
- **Archive entries**: the archive is read-only, so overrides live in the
  `.librarian.meta` of the archive's **containing folder**, with the file key extended to
  include the archive + internal path. Extend `MetadataSerializer` keying from bare
  filename to a locator string, e.g. `<file name="al.zip!/Disc1/03.flac">`. Application
  (`ApplySidecarOverrides`) matches on the same locator.
- **Collections**: collection-level user edits (e.g. correct a Show title) serialize into
  the collection folder's `.librarian.meta` under a new `<collection>` element (alongside
  `<file>`), keyed by the collection `SourcePath`. Applied during the collection
  promotion pass with `SidecarProvider`, so they survive reindex and override derived
  values — same contract as file overrides.
- Bump the sidecar `version` to `2`; reader stays back-compatible with `version=1`.

---

## 9. Browsing & UI

### 9.1 Collection Viewer (new)

- New route `collection/{id}` → `CollectionController.Index`.
- Renders, Tier-0 server-side: collection cover (`BestCover` over collection `Files`),
  collection metadata (its attributes), **breadcrumb up the parent chain**, and a listing
  of children = sub-collections + items (with counts), plus collection-level files in a
  collapsible `<details>` pane (mirrors Item Viewer's Resources pane).
- Reuse `_ItemFileRow`-style partials; add `_CollectionRow` / `_ChildEntry` partials.

### 9.2 Item Viewer

- Extend `PopulateItem`: if the item has `ParentCollectionId`, show a "Part of:"
  breadcrumb linking up the collection chain. Cover falls back to the nearest ancestor
  collection cover when the item has none (e.g. an episode with no thumbnail uses the
  season/show poster).

### 9.3 Faceted browsing

- Keep existing `CategoryView` drills. Add a structural entry point: a "Collections"
  category (or per-media "Shows", "Albums") that lists root Collections and navigates the
  real `Collection` tree (not attribute facets), giving exact hierarchy + collection art.
- Sidebar: add structural-collection roots under the Library section; arbitrary depth via
  the breadcrumb, no fixed `Path[]` needed (the tree is real).

### 9.4 Data access

- New `CollectionService` (mirror `LibraryService`): `GetCollection(id)` loads children,
  items, files, and collection attributes for display; `GetRoots()` for the landing list.
  Direct LINQ over `DatabaseContext`, consistent with current services.

---

## 10. Phased rollout

Milestones sized to ship independently; each ends green + reindexable.

- **C0 — Schema foundation.** Migrations 1–3 (§5), entity + `OnModelCreating` wiring,
  attribute-owner check constraint, writer guards. No behavior change yet.
  *Accept:* migrations apply on a populated DB; existing browsing unaffected.

- **C1 — Virtual files for archives.** `FileSource`/locator, ArchiveExpansionStep,
  route Tika entry metadata to entry rows, retire `EmbeddedFile` SubResources, remove the
  100-cap + add guard rail. Filesystem-resolution routing for virtual files.
  *Accept:* a sample `.zip` of media shows each entry as its own indexed file with its own
  metadata; archive itself no longer carries entry attributes.

- **C2 — Archive entries in the catalog.** Run the classifier over archive subtrees so a
  zip resolves to Item / passthrough / (later) Collection; temp-materialization for
  ExifTool/meta-cli; size-gated checksums + cross-boundary duplicates.
  *Accept:* a game-setup zip → one AppBundle Item; a media zip → individual Items;
  `archive!/foo` dedups against loose `foo`.

- **C3 — Collection entity + association v2 (structural).** Bottom-up tree fold,
  `inferKind`, collection-level art/nfo ownership + promotion, Reset/Manual handling.
  *Accept:* a `Show/Season N/episode` tree produces Show⊃Season⊃Episode collections;
  season poster & `tvshow.nfo` attach to the right collection; re-running association is
  idempotent and preserves a manually-pinned collection.

- **C4 — Collection browsing UI.** Collection Viewer, Item Viewer breadcrumb + cover
  fallback, sidebar/structural category, `CollectionService`.
  *Accept:* can navigate Show → Season → Episode in the browser on a Tier-0 (no-JS)
  client; covers and metadata render at each level.

- **C5 — Write-back for entries & collections.** `.librarian.meta` v2 locator keying +
  `<collection>` overrides; save forms in the new viewers.
  *Accept:* correcting a Show title and an in-archive file's tag both survive a full
  reindex.

- **C6 — Folds & polish (optional/iterative).** Route album folders into
  `Collection(Album)` (replacing orphan-cover behavior); `Artist` kind; duplicates UX for
  in-archive copies; per-collection validate action.

---

## 11. Risks & open questions

1. **Locator delimiter collision — RESOLVED (Q1).** Going with explicit columns
   (`Source`, `ParentFileId`, `InternalPath`) + composite unique index; `Path` is a
   synthesized display string only, never parsed for resolution. Real filenames containing
   any delimiter are safe.
2. **Blast radius of `AttributeBase.FileId` going nullable.** Many queries assume
   `FileId != null` (e.g. `LibraryService.LevelPairs` filters `a.FileId != null`). Audit
   all attribute queries; collection attributes must be excluded from file-faceted
   listings. Add `CollectionId == null` guards where file-only is intended.
3. **Re-extraction cost on big archives.** Temp-materializing every entry for ExifTool/
   meta-cli can be heavy. Mitigate: only materialize entries whose type those providers
   handle; batch per archive; gate on archive change. Watch disk usage of the scratch dir.
4. **Album behavior change.** Folding albums into `Collection(Album)` changes today's
   "each track is a standalone Item, cover orphaned" model. Good change, but it moves the
   album cover owner and may affect existing faceted "By Album" views. Stage in C6 and
   verify the Music category still works.
5. **Idempotency of structural detection.** The bottom-up fold must be deterministic and
   stable so Manual pins re-bind by `SourcePath`. Needs solid tests with reordered/renamed
   folders.
6. **Nested archives depth.** Allow but bound (max nesting depth setting) to avoid zip-bomb
   pathologies; log when the bound truncates.

### Decisions

- Q1 — Locator strategy: **explicit columns** (`Source`/`ParentFileId`/`InternalPath`),
  `Path` display-only. ✅ resolved.
- Q3 — `Checksum:MinSize`: **1 MiB**. ✅ resolved.
- Q4 — Albums: **fold into `Collection(Album)` in C6** (replaces per-track-only behavior). ✅ resolved.

### Still open

- Q2: Do we introduce `CollectionKind.Artist`, or keep artist/discography folders
  `Generic`? (Low stakes; can default to `Generic` and add `Artist` later if useful.)

---

## 12. Test plan (high level)

- **Fixtures**: a TV tree (`Show/Season 01/ep.mkv`+`.srt`, `Show/poster.jpg`,
  `Show/Season 01/folder.jpg`); an album folder (loose tracks + `cover.jpg`); a game zip
  (`game.exe`+`*.dll`); a media zip (mixed tracks); a zip containing the loose-file
  duplicate; a zip-in-zip.
- **Association**: assert the resolved Collection/Item tree, kinds, primaries, and
  ownership of art/nfo for each fixture. Re-run twice → identical (idempotent). Pin one
  collection Manual, reindex → pin preserved.
- **Archives**: each entry indexed with its own metadata; no entry attributes on the
  archive row; 100-cap removed; guard-rail fallback logs.
- **Dedup**: loose vs in-archive copy share checksum and appear as duplicates; sub-threshold
  files skipped.
- **Write-back**: file override inside an archive + collection-title override both round-trip
  through reindex.
- **UI**: Tier-0 navigation Show→Season→Episode; cover fallback to ancestor; breadcrumbs.
```
