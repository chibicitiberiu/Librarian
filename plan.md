# Librarian — Metadata & Search Plan

Forward-looking roadmap for the metadata/search subsystem and the browsing UX.
Phases 1–3 (Tika integration, two-layer metadata model, code-based normalization,
full-text search, dockerization) are complete and shipped. The real-library
correctness/reliability program (formerly a separate `findings.md`, now folded in here) is
largely done — see **Status**. The old scratch `Ideas.md` (marking/clipboard, browse TODOs,
checksums, indexing config, non-indexed search) has also been folded into the phases below.
Progress re-evaluated against the code at commit `5f9069f` (Phase-4 write-back, checksums, and
vocabulary fixes all shipped). Updated 2026-06-25.

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
  (libavformat) for deep media; `FileMetadataProvider` for filesystem facts; **ExifTool** for deep
  embedded image/media tags (EXIF/IPTC/XMP/GPS/maker-notes/RAW). **ExifTool augments Tika, it does
  not replace it:** it runs as an additional raw provider alongside Tika (Tika still does
  content-text extraction); both contribute raw values and the normalizer reconciles. The **read**
  provider ships (ported from the older `NewLibrarian2` line); the **embed writer** (opt-in
  promote-to-source) is still Phase 4.
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
  ("promote to source") is opt-in, per-format. The attribute-layer write path **ships** (Phase 4,
  `.librarian.meta` + `MetadataService.SaveUserEditsAsync`); Item/role facts in the sidecar are still TODO.
  - **Why folder-level, not per-file `name.ext.meta` (don't relitigate).** (1) **Item/role facts are
    group-scoped** — "these 144 files are one Item, `OUTPOST2.EXE` is primary, this folder is not a
    bundle" has no natural per-file home; stashing it in the primary's sidecar orphans the grouping if
    the primary moves. Per-file would force a *second* folder-level file anyway → worst of both. (2) The
    **clutter** argument is weaker than it looks: we only write sidecars for *edited* files, so it's "1
    file per edited folder" vs "1 per edited file," not "a sidecar for every file." (3) The per-file
    **portability** win ("metadata travels with the file") is mostly illusory — a generic file manager
    won't co-move the `.meta`, and in a folder-organized library *folder* moves dominate, which the
    folder sidecar (living inside the folder) handles perfectly; in-app moves rewrite sidecars either
    way (Phase 6e). **Escape hatch if single-file portability ever matters:** keep folder-level as
    canonical and *additionally* honor a per-file `name.ext.meta` on read, written only on an explicit
    app "export/move one file" — YAGNI until asked.
- **Item corrections are durable overrides, surfaced in the Item Viewer.** The association pass is a
  re-runnable projection that resets its own assignments each run, so manual fixes need a protected
  layer: `RoleSource ∈ {Auto, Manual}` (on the file and the Item) + per-folder
  `BundlingMode ∈ {Auto, Disabled}`; reset clears only `Auto`, and manual always wins. (The columns
  exist; the editing UX is Phase 6d, persistence waits on Phase 4 write-back.)

---

## Next up

### Phase 4 — Curation & editing (close the loop)

Make the collected metadata actually *usable*, not just stored.

- [x] ~~**Metadata editing / `.meta` write-back (attribute layer).**~~ Done. User edits now persist to a
      hidden **folder-level** sidecar (`.librarian.meta`, one per folder, `<librarian>`→`<file name>`
      entries; the on-disk system of record) and survive re-indexing. `MetadataService.SaveUserEditsAsync`
      writes only genuine **overrides** (a submitted value kept only if it differs from what extraction
      reports, and only for editable, non-`File attributes` definitions; reverting to the extracted value
      drops the override and deletes empty sidecars). The sidecar is applied as an **authoritative
      override** — `ApplySidecarOverrides` (indexing) and `CollectMetadataAsync` (display) replace the
      matching extracted/promoted value rather than duplicating it. Tier-0 form-POST: `MetadataController.Save`
      via the `metadata_actions/save` route, the metadata view wrapped in a `<form>` (Save = submit,
      antiforgery). Verified live: author Title + multi-Tag → written to disk → survives `reindex?force`;
      override replaces an extracted Title (one row); revert drops it. Indexer already skips the sidecar
      (`IsMetaFile`). Serializer reuses the per-attribute vocabulary (text/int/float/date/timeSpan/blob).
- [ ] **Item/role facts in the sidecar** (the remaining write-back piece). The `<file>` element is the
      reserved home for `primary:`/`role:`/`bundle: none` + per-file overrides; wiring them (and making
      `ItemAssociationService` respect on-disk overrides) folds into the **M6 correction-actions** UI
      (Phase 6d) — the in-DB override layer (`RoleSource`) already exists; this just persists it to disk.
- [x] ~~**Vocabulary data bugs + validation test.**~~ Done. Cleared the bogus units that had landed on
      non-numeric fields (Artist/Initial key/Lyrics/Actor/Architecture/End-of-life-date carried
      `dB`/`bpm`/`bps`; the Artist = "dB" one surfaced as a meta-cli `'-4.23 dB'` `FormatException`),
      and relocated `bpm` to its real home (Beats per minute). `FixVocabularyUnits` migration ships the
      seed update; `VocabularyTests` validates the parsed dataset (sequential ids, name/group present,
      unique group+name, **units only on numeric types**). (Replaygain fields are now mapped — see
      "Real-library value-parsing hardening" below.)
- [x] ~~**Unit normalization (value-level).**~~ Done. Took the good ideas from the older
      `NewLibrarian2` normalization layer and **redesigned them to fit A's pipeline** (rather than a
      direct port of its parallel `DataNormalizer`/`NormalizingMetadataFactory`, which duplicated A's
      vocabulary + rule system + raw-layer audit). New pure `Units` catalog/converter
      (DataRate/DataSize/Frequency/FrameRate → canonical bps/byte/Hz/fps), a flexible `Duration`
      coercer (seconds, `HH:MM:SS`, `M:SS.ms`), and unit-aware `IntegerIn`/`FloatIn` coercers, wired
      into `MetadataNormalizer` with optional per-rule **range validation**. Unit-suffixed values A
      previously **dropped** ("320 kbps", "44.1 kHz", "2:30.5") now coerce to canonical units, and
      implausible values (e.g. sample rate > 192 kHz) are rejected. Sample rate / Bit rate / Frame
      rate / Size now carry canonical display units (`NormalizeNumericUnits` migration). The raw layer
      stays the audit trail (no audit columns). 26 unit tests. (Additive — only affects values that
      were being dropped; plain numbers are unchanged.)
- [x] ~~**Real-library value-parsing hardening.**~~ Done. Mined the `NewLibrarian2` TestData dump (a
      ~33k-file avformat metadata export) and fixed the value-parsing bugs it exposed. ReplayGain
      readings arrive as `"0.00 dB"` / `"-5.81 dB"` and Matroska stream durations as
      `"01:48:05.563000000"` — both crashed `MetadataFactory`'s `Convert.ToDouble`/`Convert.ToInt64`/
      timespan conversion and aborted a whole file's meta-cli extraction (the known `'-4.23 dB'`
      `FormatException`, now confirmed common). Conversion is now centralized, lenient and
      invariant-culture: `ConvertToInt64`/`ConvertToDouble` (via `Units.TryParseQuantity`) tolerate a
      unit suffix and the `N/M` track/disc form; `ConvertToTimeSpan` (via `ValueCoercer.Duration`)
      tolerates `HH:MM:SS(.fff)`. **`TLEN`** (ID3 length in **milliseconds**, redundant with the real
      stream duration) was mapping to Duration as if seconds → set to Ignore (`IgnoreTlenDurationAlias`
      migration). ReplayGain is now also promoted on the **Tika** path (lenient `Number` coercer →
      Audio Track/Album gain+peak + reference loudness), closing the long-standing "replaygain
      unmapped" gap. The meta-cli alias table was already comprehensive (built from this same data), so
      the win here was **parsing, not mapping**. 10 real-data-derived tests.
      *Noted follow-up:* `MetadataFactory`'s date parsing still uses current-culture `DateTimeOffset.Parse`
      (real data has both `"1999-10-25"` and `"05/01/2018 17:50:54"`), a candidate for the same
      invariant/multi-format hardening.
- [x] ~~**Collector-driven rule + parsing expansion.**~~ Done. Built `MetadataCollector` (see
      Diagnostics) and ran it over a real library; it showed **ExifTool only ~1% mapped** (its tags land
      under namespaces with no rules) plus parsing gaps. Parsing: **content type** strips MIME params
      (`text/plain; charset=…` → `text/plain`); **image width/height** now map from Tika
      (`tiff:imagewidth`/`tiff:imagelength`, `tika:Image Width`="N pixels" via a lenient integer
      coercer), not just exiftool; **audio channels/sample-rate/bits** map from `flac:`; **archive
      recursion capped** (`TikaMaxEmbeddedResources`, default 100 — a 2.2k-entry zip went 12.9k → 589
      raw rows). Rule batch: **`vorbis:`/`id3:`** audio tags (mirror of the xmpDM:/tika: set), **`exe:`**
      PE VERSIONINFO → Product/Publisher/Copyright/Description/Version (+ a PE-machine-type → Architecture
      coercer), **codec** (xmpDM/QuickTime compressor), and Tika cataloguing extras (sort names,
      grouping, media, MusicBrainz album status/type). Result on the dev library: **ExifTool ~1% → ~36%
      mapped, canonical items +60%**; exe metadata now populates Software facets (Version / Architecture
      / Publisher). 26 new tests.
- [x] ~~**AttributeDefinition id-space.**~~ Done. Seed defs (CSV/`HasData`, ids 1–120) and
      runtime-curated "Other"-group defs shared one identity space, so a CSV-appended attribute would
      collide on the PK. `ReserveCurationIdSpace` migration vacates any curated rows squatting in the
      reserved range (they're rebuildable from the raw layer → recreated by `renormalize`) and restarts
      the identity sequence at **1,000,000**: ids `<1,000,000` are reserved for the seed, curation lives
      at `1,000,000+`. (Previously worked around by reusing `General.Collection` for TV "Series".)
- [x] ~~**Checksums & integrity** *(Ideas.md)*.~~ Done (`ChecksumService`, its own pass — not inline with
      extraction). SHA-256, **change-gated**: the prefix hash is cleared and the full-hash attribute
      dropped when a file changes, so a normal incremental index never re-hashes an unchanged file.
      **Three-state toggle** (`Checksum:Mode`, default **Off**; overridable per-run via `?mode=`):
      - **Dedup** — *staged*: group by **size** (free) → store a **prefix hash** (first block) for
        size-collisions → full hash only for files that still collide. Lazy — most files never fully read.
      - **Integrity** — always full SHA-256 (a prefix hash can't catch mid-file bitrot).
      **Storage = hybrid:** full hash = the read-only `File attributes/Checksum` canonical attribute
      (id 121 — the seed append the id-space fix unblocked; shows in the Item Viewer); prefix hash =
      internal `IndexedFile.PrefixHash` column the dedup pass groups on. Endpoints:
      `POST /api/metadata/checksum[?mode=]` + `GET /api/metadata/duplicates` (sets sharing a full hash).
      Verified live on the dev library: integrity hashed 276 files; **dedup found the same 8 duplicate
      sets / 17 files while fully reading only 17** (16× fewer reads); change-gating clears only the
      edited file's hashes. *Remaining (smaller follow-ups):* a per-Item **validate** action + a
      **duplicates view** in the Item Viewer (the data + endpoints exist; this is UI).
- [x] ~~**ExifTool provider (read) — deep embedded metadata.**~~ Done. Ported from the older
      `NewLibrarian2` line and rewritten A-idiomatically as an **`IRawMetadataProvider`** that
      **augments Tika** (both run side by side; the normalizer reconciles). `ExifToolService` invokes
      the external `exiftool` binary (`-json -G0 -n`, mirroring the meta-cli subprocess pattern —
      config `ExifToolPath`, defaults to PATH, warn-once-and-skip when absent); `ExifToolProvider`
      splits each `Group0:Tag` into a raw namespaced record (`EXIF:Make` → `exif`/`Make`), dropping
      filesystem-duplicate keys and binary blobs. Normalizer rules promote image width/height; the
      existing `exif:datetimeoriginal` rule already coerces exiftool dates. Wired into DI; `exiftool`
      added to the Docker runtime image; 8 unit tests. Maker-notes/IPTC/XMP/GPS/RAW land in the raw
      layer now (browsable; promotable once canonical camera/GPS attributes are seeded — see id-space).
- [ ] **ExifTool — the embed writer (`SaveMetadataAsync`).** The remaining half: ExifTool is the
      natural writer for the opt-in "promote to source" path (write EXIF/IPTC/XMP back into
      originals), complementing the `.meta` write-back. Open sub-decision: per-file invoke vs a
      `-stay_open` daemon for throughput.
- [x] ~~**Cross-provider precedence / dedup.**~~ Investigated and **moot**: there are no real
      meta-cli↔Tika value conflicts; the MIME generic-vs-specific case is handled by `MediaType.Resolve`
      (M2), and stale accumulated values were flushed by the force-reindex. Canonical writes are now
      idempotent (0 duplicate rows). Remaining "conflicts" are legitimately multi-valued fields.

### Phase 5 — Hardening for real use

Required before exposing this beyond a trusted dev box.

- [ ] **Authentication.** There is none today, and the server exposes the whole mounted library.
      Add auth (and document running behind a TLS-terminating reverse proxy).
- [ ] **Indexing configuration + admin UI** *(Ideas.md)*. Indexing is configured by env-vars /
      `appsettings.json` only (`BaseDirectory`, ignore list, schedule). Add an in-app settings page to
      configure indexing (roots, ignore patterns, schedule, checksum on/off) and to trigger the
      existing re-runnable admin endpoints (`reindex` / `renormalize` / `associate` /
      `reindex-search`) from the UI instead of curl.
- [x] ~~**Indexing robustness.**~~ Done: per-unit-of-work `DbContext` (each file/dir in its own
      scope), FS-watcher **debounce** (per-path, coalesces a write's event burst), idempotent
      replace-on-reindex (no more duplicate rows), `PruneMissing` after a full walk, a `force` full
      reindex, and unchanged files/dirs skip re-extraction (tolerance-based change detection). Still
      open: the meta-cli-under-bulk race couldn't be reproduced (the binary is simply absent in this
      env — it now warns once and skips; Tika fully covers).
- [x] ~~**Provider resilience + incomplete tracking (error strategy).**~~ Done. Replaced the
      scattered swallow-everywhere behaviour with one central policy: `ProviderExecutor` (singleton)
      retries transient provider failures (network / timeout / 5xx, surfaced as
      `TransientMetadataException`) with exponential backoff and trips a per-provider **circuit
      breaker**, so a down provider (typically Tika) neither stalls the index nor gets hammered file
      after file. `TikaService` now classifies HTTP failures (5xx/408/429/network → transient; other
      4xx → simply no metadata) and `TikaProvider` propagates transient errors instead of swallowing
      them. A file whose extraction is cut short after retries is flagged
      `IndexedFile.ExtractionIncomplete` (migration); `POST /api/metadata/reindex-incomplete`
      re-indexes just those files (resetting the breaker first), and a full reindex resets it too.
      Tunable via `Metadata:{MaxRetries,RetryBaseDelayMs,CircuitFailureThreshold,CircuitResetSeconds}`.
      Display/preview paths still degrade quietly (no retry storms on page views). 7 unit tests.
      (Ported the *ideas* from the older `NewLibrarian2` `ResilientMetadataProvider` — retry, backoff,
      circuit breaker — but centralized instead of a per-provider decorator, which A's swallow-at-
      source providers would have left inert.)

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

**6e — Browse: view modes, selection & actions, chrome.**
- **View modes:** Details (with a per-category **column chooser** sourced from the vocabulary;
  defaults per category — Music → Track/Artist/Album/Duration, Photos → Dimensions/Date, folder
  → Size/Type/Modified), Icons, Thumbnails. Persist the chosen mode/columns per category.
- **Selection & actions — the "mark set" model** *(Ideas.md; supersedes the bare cut/copy
  clipboard).* A persistent set of *marked* files is the single action target:
  - Mark files from **browse** pages, **search** results, or a **quick-mark box** that accepts simple
    glob patterns (`*.exe`, `*.bmp`). Marks **persist across pages** (as the clipboard does today).
  - Operations on the mark set: **move, copy, delete, bulk rename** (only single-file `RenameFile`
    exists today — add a pattern/sequence bulk rename), plus **bulk Properties** (edit common fields
    across the set) and the Item-correction actions from 6d.
  - Context menu is the primary per-row surface; the mark set drives the toolbar/menu actions.
  - Modern tier adds drag-and-drop — **onto a folder = move; onto a category facet = set that
    attribute** (drag songs onto "Jazz" → set genre; onto a Tag node → add tag) — plus trash +
    undo for move/rename/delete.
  - **Tier-0 note:** the current actions go through `XMLHttpRequest`+JSON (`browse.js`), which breaks
    on RetroZilla. The mark set must have classic **form-POST** endpoints as the baseline, enhanced
    by JS — same as the rest of Phase 6.
- **Menus & chrome** *(Ideas.md)*: a Nautilus-style **menu bar** (File / Edit / View / Go) as the
  Tier-0 home for actions a context menu can't always reach; a **browse-settings page** for view
  defaults, column sets, and ignore / quick-mark preferences.
- **Index stays consistent with app-initiated file ops** *(Ideas.md)*. Move/copy/rename/delete
  currently rely on the FS-watcher round-trip; when the app itself performs the operation it should
  update the index **directly** so the browse view is correct immediately.
- **Gestures:** single-click selects/marks, double-click opens the Viewer, the name link
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
- [ ] **Non-indexed (live-filesystem) search** *(Ideas.md)*. Full-text + structured search over the
      *index* is done; add an option to also search the live filesystem for not-yet-indexed paths
      (name globs / recent files), so freshly-added files are findable before the next index pass.
- [ ] **Caching layer** *(deferred from the NewLibrarian2 merge)*. The older line has a managed
      `IMemoryCache` wrapper, but A has no hot path that needs it today (FTS + DB indexes are fast),
      and its metadata-invalidate was a no-op on `IMemoryCache`. Revisit only if profiling shows a
      bottleneck — e.g. cache the FTS result set with a short TTL / invalidate on reindex.

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
`BaseDirectory`, `TikaUrl`, `MetadataCliPath`, `ExifToolPath`.

### Diagnostics — `MetadataCollector`

A standalone, **database-free** tool that runs the real collectors (Tika + ExifTool raw providers +
meta-cli) over a file set and dumps what they extract, flagging which keys the pipeline can currently
map. Use it to see "what does our extraction look like" over a large real library and to generate the
**unmapped-keys worklist** for improving rules/aliases. (Rewritten from the old meta-cli-only stub —
it reads the vocabulary/aliases from the embedded CSVs, so no DB is needed.)

```sh
dotnet run --project MetadataCollector -- -p <path...> -r \
  --metadata-cli-path meta-cli/build/meta-cli [--filter .mp3 .flac .mkv] [--max-files N] [-o out-dir]
```

Outputs CSVs: `raw-metadata.csv` (every provider/namespace/key/value + Mapped), `canonical-metadata.csv`
(what would be stored after promotion), `unmapped-keys.csv` (ranked by #files — the rules worklist),
`file-summary.csv` (per-file counts/status).
