# Librarian — Metadata & Search Plan

Forward-looking roadmap for the metadata/search subsystem and the browsing UX.
Phases 1–3 (Tika integration, two-layer metadata model, code-based normalization,
full-text search, dockerization) are complete and shipped. The real-library
correctness/reliability program (formerly a separate `findings.md`, now folded in here) is
largely done — see **Status**. Updated 2026-06-25.

## Status — real-library program (was findings.md M0–M6)

Tested against a genuinely messy library (books, music, TV, software), almost every category view
was empty, wrong, or noisy. Fixed milestone by milestone:

- **Done:** ingestion hygiene (default ignore patterns; never store extractor error strings as
  values) · normalization coverage (Music/Software/Documents facets populate from already-extracted
  raw data) · derived **Type** taxonomy + sub-resource denoise + idempotent promotion · the
  **Item/bundle model** — the keystone (see Architecture): sidecar association + metadata promotion,
  companion classification, item-centric browsing · reindex reliability (idempotent
  replace-on-reindex, per-unit-of-work `DbContext`, watcher debounce, prune-missing, force-reindex;
  0 duplicate rows) · facet refinement (multi-value genre/tag split, ebook MIME filter, TV filename
  parser unlocks Video, tag junk-filter, Photos year▸month drills) · the **Item Viewer** read-only
  view (cover preview + role-grouped "Files in this Item" pane).
- **Remaining** is the editing + UX tail — tracked in Phases 4–6 below.

Admin/maintenance endpoints (re-runnable, no file re-reads unless noted):
`POST /api/metadata/{renormalize, associate, reindex?force=true, reindex-search}`.

## Goal

Index a data collection (a big folder on a server), extract metadata and content from a
broad range of file formats, normalize that metadata into a consistent set of fields, and
make everything browsable and searchable — delivered as a self-hosted server via docker-compose.

## Architecture (as built)

```
                 ┌─────────── Providers (extract) ───────────┐
 file on disk →  │  FileMetadataProvider   (canonical: file)  │
                 │  MetadataCliProvider    (canonical: media) │ → records
                 │  TikaProvider           (raw: dc/exif/...) │
                 └────────────────────────────────────────────┘
                              │
                    ┌─────────┴─────────┐
              canonical             raw layer (lossless, source of truth)
              providers                   │
                    │            MetadataNormalizer (code rules + coercers)
                    │                     │  promote (namespace, key) → definition
                    └─────────┬───────────┘
                              ▼
              canonical typed attributes  +  extracted text content
                              ▼
                    Postgres: structured query + full-text search
```

- **Raw layer** (`RawMetadataAttribute`): every value a provider reports, kept verbatim under
  a schema namespace (`dc`, `exif`, `tika`, …). Lossless source of truth → promotion can be
  re-run without re-reading files.
- **Canonical layer** (typed `AttributeBase` tables): the curated projection produced by the
  normalizer. Per-file, joined to `AttributeDefinition` — which is exactly what faceted
  ("category") browsing needs. Only mapped keys land here; unmapped keys stay raw.
- **Item layer** (`Item` + `IndexedFile.Role`/`ItemId`): the catalogued unit is an **Item** — one
  primary file plus its sidecars/companions — grouped per folder by a re-runnable association pass
  (`ItemAssociationService`). An app/game install (exe + resources) collapses to one Item over its
  whole subtree; a book folder is one Item; album tracks are one Item each. Browsing and faceting
  enumerate Items (their primaries; sidecars/companions are filtered out); a sidecar's metadata
  (e.g. a book's `.opf`, an `.lrc`'s lyrics) is **promoted onto its primary**. See the Item-model
  decisions under Standing decisions.

## Standing decisions (don't relitigate)

- **Extraction:** Apache **Tika** (sidecar) for broad formats + content text; **meta-cli**
  (libavformat) for deep media; `FileMetadataProvider` for filesystem facts.
- **Search:** PostgreSQL **full-text search**, **not** Lucene — revisit only if PG FTS proves
  insufficient.
- **Normalization rules live in code** (`MetadataNormalizer`), not in data.
- **meta-cli / file stay canonical providers** — they're curated and map cleanly, so they don't
  have the key-sprawl the raw layer solves. Raw and canonical providers run side by side.
- **UI metaphor: Nautilus-in-the-browser** (GNOME 2 / Bluecurve). One mental model for the whole
  app. A **category is a virtual folder** — browsing a facet feels identical to browsing a real
  folder; only the path scheme differs (`library://…` vs `/…`), so both browse modes share the
  view, columns, actions, and viewer.
- **Progressive enhancement / retro-browser compatibility is a hard requirement.** The app must
  stay usable on old systems/browsers (RetroZilla on Win9x/XP). Baseline (Tier 0) is pure
  server-rendered HTML — `<form>` POST + links, full-page navigation, **no-JS-functional**;
  modern features (Tier 1) are layered on by JS without breaking the baseline. Theming stays
  consistent across tiers; only the feature level differs. See Phase 6.
- **The catalogued unit is an _Item_** (one primary file + its companions/sidecars); the whole
  collection is **the Library / Catalog**. ("Work" — FRBR jargon from early drafts — is retired.)
  Applies to the entity, the routes, and every UI label; the single-item screen is the **Item Viewer**.
- **Metadata persistence — DB is a cache, disk is the system of record.** Three data kinds:
  (1) extracted/derived (Tika/meta-cli/EXIF) → **DB-only cache, never written to disk**;
  (2) user-authored (title/description/tags) → **persisted to disk**; (3) organizational/relational
  (Item grouping, file roles, primary designation, category overrides) → **persisted to disk,
  folder-scoped**. **Invariant:** the DB is always rebuildable from *file contents + on-disk
  sidecars*. On-disk format = **one hidden, dot-named, folder-level sidecar** per edited folder (an
  edited 14-track album = one file; the only place Item/role facts can live), **co-located** so
  metadata survives partial moves. **Never mutate originals by default** — embedding tags back
  ("promote to source") is opt-in, per-format. (The write path doesn't exist yet → Phase 4.)
- **Item corrections are durable overrides, surfaced in the Item Viewer.** The association pass is a
  re-runnable projection that resets its own assignments each run, so manual fixes need a protected
  layer: `RoleSource ∈ {Auto, Manual}` (on the file and the Item) + per-folder
  `BundlingMode ∈ {Auto, Disabled}`; reset clears only `Auto`, and manual always wins. (The columns
  exist; the editing UX is Phase 6d, persistence waits on Phase 4 write-back.)

---

## Next up

### Phase 4 — Curation & editing (close the loop)

Make the collected metadata actually *usable*, not just stored.

- [ ] **Metadata editing / `.meta` write-back.** `SaveMetadataAsync` is unimplemented (both providers
      throw, no save endpoint). Wire the edit UI through to persistence and write back to the hidden
      **folder-level** sidecar so user edits survive re-indexing. **This is the prerequisite for
      disk-persisting Item/role overrides** (Standing decisions) — the `.meta` schema must be extended
      to express Item facts (`primary:`, `role:`, `bundle: none`, per-file overrides), not just a flat
      per-file attribute list.
- [ ] **Vocabulary data bugs + validation test.** Some units in `MetadataAttributes.csv` are wrong
      (e.g. Artist = "dB"), which surfaced as a meta-cli `'-4.23 dB'` `FormatException`. Fix them and
      add a test that validates the vocabulary on load. (Replaygain fields are also still unmapped.)
- [ ] **AttributeDefinition id-space.** Seed defs (CSV/`HasData`, ids 1–120) and runtime-curated
      "Other"-group defs (auto-identity, **121+**) share one identity space, so a CSV-appended
      attribute collides on the PK. Separate the ranges (reserve a high seed range, or give curation
      its own range) before adding more seed attributes. (Worked around once by reusing
      `General.Collection` for TV "Series".)
- [x] ~~**Cross-provider precedence / dedup.**~~ Investigated and **moot**: there are no real
      meta-cli↔Tika value conflicts; the MIME generic-vs-specific case is handled by `MediaType.Resolve`
      (M2), and stale accumulated values were flushed by the force-reindex. Canonical writes are now
      idempotent (0 duplicate rows). Remaining "conflicts" are legitimately multi-valued fields.

### Phase 5 — Hardening for real use

Required before exposing this beyond a trusted dev box.

- [ ] **Authentication.** There is none today, and the server exposes the whole mounted library.
      Add auth (and document running behind a TLS-terminating reverse proxy).
- [x] ~~**Indexing robustness.**~~ Done: per-unit-of-work `DbContext` (each file/dir in its own
      scope), FS-watcher **debounce** (per-path, coalesces a write's event burst), idempotent
      replace-on-reindex (no more duplicate rows), `PruneMissing` after a full walk, a `force` full
      reindex, and unchanged files/dirs skip re-extraction (tolerance-based change detection). Still
      open: the meta-cli-under-bulk race couldn't be reproduced (the binary is simply absent in this
      env — it now warns once and skips; Tika fully covers).

### Phase 6 — Browsing & viewer UX

Lean fully into the Nautilus-in-the-browser metaphor (see standing decisions). Everything below
must respect the progressive-enhancement tiers described at the end of this phase.

**6a — Places sidebar + dual browse roots. ✅ Shipped.** A persistent left sidebar (inside the
window, Nautilus-style) replaces the bare `Browse | Search` nav:

```
Places
  🏠 Home            ← the folder tree (browse by folder, as today)
  ⭐ Bookmarks
Library              ← faceted views over metadata (browse by category)
  🎵 Music  🎬 Video  🖼️ Photos  📄 Documents  💿 Software  🏷️ Tags
Smart views          ← user-defined
  …
  ＋ New view…
```

**6b — Category browsing (live). ✅ Shipped + refined (M1–M5).** A category owns a file filter and a
set of named facet **views** ("By Artist", "By Year", …); a view = `(ordered drill-down path) +
(leaf sort)`. Each level is virtual folders (Artist → Album → Track). Computed **live** from the
typed attribute tables (no materialization needed so far). Refinements done: a derived **Type**
taxonomy (clean media-class labels, not the raw `file` string); multi-value genre/tag splitting; a
TV filename parser (Series/Season/Episode) that unlocks Video; Photos year▸month date drills; ebook
MIME filter; tag junk-filter. Browsing enumerates **Items** (primaries), so sidecars/companions and
bundle resources don't pollute facets or counts.

Categories shipped (Music / Video / Photos / Documents / Software / Tags / All-by-type), each with
"By X" views — grounded in the actual vocabulary (`Audio`, `Media`, `Video`, `Image`, `General`,
`Software`, `File attributes`). Original sketch:

| Category | Filter | Drill-down path |
|---|---|---|
| Music | `mime audio/*` | `Audio.Album artist → Audio.Album → Audio.Track` |
| ↳ by Genre | `mime audio/*` | `Media.Genre → Album artist → Album` |
| Movies | `mime video/*` | `Media.Genre → General.Year → Title` |
| TV | `mime video/*` | `Title (series) → Media.Season → Media.Episode number` |
| Photos | `mime image/*` | `General.Date created (year ▸ month)` |
| Documents | `text/* + pdf + office` | `General.Written by → Title` (or `File type → Title`) |
| Software | `Software.*` present | `Software.Platform → Software.Architecture → Title` |
| Tags | `General.Tag` present | `General.Tag` (multi-valued) |
| All by type | none | `File attributes.File type` |

The normalization-coverage caveat is largely closed (Music/Software/Documents-by-author now
populate; Documents-by-author works because the `.opf`'s author is promoted onto the book). Still
incremental: Photos-by-camera, and richer date sources (exif over filesystem date) when present.

**6c — Smart-view builder (user-defined categories).** The 6b builder exposed in the UI —
essentially generalized "Smart Playlists", riding on the Search subsystem. Form: name + icon,
filter rows (`attribute · op · value`, optional free-text FTS), group-by path, leaf sort. Stored
as a `SmartView` entity. Any `/search` gets a "Save as view" button → lands in the sidebar.

**6d — Item Viewer (presentation window).** Collapse "open file" + the separate metadata page
into one single-item window. *Read-only view ✅ shipped* (the Properties screen now has a cover
preview + a role-grouped **"Files in this Item"** pane — Content / Sidecars / Resources, resources
collapsed for big bundles, primary badged; cover = the file if it's an image, else the Item's best
cover-art companion, else folder art). Remaining:
- **Adaptive preview** — a registry of previewers keyed by mime: image / audio / video / pdf /
  text+code (with raw-vs-rendered toggle for md/html) / archive (lists sub-resources) / fallback
  (icon + download). (Today it serves the file inline via `/browse/{path}`.)
- **Info pane** — a *curated* key-facts subset (Title, markdown Description, top fields for the
  file's primary category), NOT the full dump. "Show all metadata ▸" expands to today's grouped
  table; one more level reveals the **raw layer** (`dc`/`exif`/`tika`).
- Keep the two senses of "raw" distinct: preview source-vs-rendered vs. metadata raw layer.
- **Item-correction actions** (the durable-overrides UX from Standing decisions): per-file
  set-primary / change-role / detach / move; per-Item split / merge / "not a bundle" / set-cover /
  change-category; reset-to-auto; bulk. **Tier-0 form-POST endpoints** that write `RoleSource=Manual`
  so the association pass can't clobber them. A View-menu **"Group files into Items"** toggle
  (`?grouped=0`, a plain link) drops to the flat file list. Disk persistence waits on Phase 4
  write-back; the override layer works in-DB today.
- **Edit properties** flips the info pane inline (Tier 1) / opens the edit form (Tier 0); saving
  depends on Phase 4 write-back.
- **Prev/next** across the current listing — a lightbox over the browse results (Tier 1).
- Replaces the standalone metadata page as a file's "open" target.

**6e — Browse view modes + actions.**
- **View modes:** Details (with a per-category **column chooser** sourced from the vocabulary;
  defaults per category — Music → Track/Artist/Album/Duration, Photos → Dimensions/Date, folder
  → Size/Type/Modified), Icons, Thumbnails.
- **Actions:** context menu as the primary surface; clipboard with clear cut-state feedback;
  bulk Properties (edit common fields across a selection). Modern tier adds drag-and-drop —
  **onto a folder = move; onto a category facet = set that attribute** (drag songs onto "Jazz" →
  set genre; onto a Tag node → add tag) — plus trash + undo for move/rename/delete.
- Separate the gestures: single-click selects, double-click opens the Viewer, the name link
  navigates (folders) / opens (files), Properties lives in the viewer.

**Progressive-enhancement tiers (applies to all of 6a–6e):**
- **Tier 0 — baseline (RetroZilla / Win9x / XP):** pure server-rendered HTML, `<form>` POST +
  links, full-page navigation, no-JS-functional. The existing **session clipboard is the correct
  Tier-0 action model** (Cut/Copy/Paste as form submits + reload). The viewer is a full page;
  prev/next are links; categories are plain links (faceting is server-side anyway). Bluecurve
  theming via old-Gecko-safe CSS (avoid flexbox/grid where it breaks).
- **Tier 1 — modern:** JS progressively enhances the *same* HTML — inline lightbox viewer, AJAX
  actions, drag-and-drop, undo toasts, dynamic column chooser, keyboard nav.
- Server selects the tier by UA (or feature detection); theming is identical across tiers.
- **Code implication:** browse actions currently use `fetch()` + JSON (breaks on RetroZilla). Add
  classic form-POST endpoints as the baseline, then enhance with JS.

**Remaining build order** (category browsing + Item Viewer read-only are done): listing thumbnails
(needs the thumbnail service below) → Item-correction actions + action-model upgrade (pair with
Phase 4 write-back) → view modes / column chooser → smart-view builder.

### Backlog (optional / lower priority)

- [ ] **Thumbnail service.** Server-generated, **resized + cached** thumbnails for image/video/pdf —
      unlocks listing thumbnails (6e). An Item's cover art is already associated, so it's mostly an
      image endpoint + resize: add a pure-managed image library (**SixLabors.ImageSharp** — Docker
      friendly) and cache to the app data dir. Plain `<img>`, so it works on every tier. (Today a
      single cover *preview* works dependency-free via `/browse/{path}`, browser-scaled; resize matters
      for many small list thumbnails.)
- [ ] **Convert meta-cli/file providers to raw** (unification). The 105 ffmpeg-alias code rules are
      already generated and ready if a single raw layer for media is wanted.
- [ ] **CJK search.** Stock Postgres can't word-segment Chinese/Japanese/Thai. If that content
      matters, add an extension (`pgroonga` / `zhparser`). Latin/Cyrillic/Greek work today.

---

## Dev workflow

```sh
make check-deps          # what's installed
make start-db            # PostgreSQL container
make start-tika          # Apache Tika container
make run                 # build + run at http://localhost:5080 (auto-migrates)
make app | cli | clean   # build .NET | build meta-cli | clean
make stop-db | clean-db  # manage the dev database
make up | down           # full self-hosted stack (docker-compose)
```

Configuration is env-var driven (also `Librarian/appsettings.json`): `ConnectionStrings__DB`,
`BaseDirectory`, `TikaUrl`, `MetadataCliPath`.
