# Librarian — Metadata & Search Plan

Roadmap for the metadata collection, normalization, and search subsystem. Updated 2026-06-23.

## Goal

Index a data collection (a big folder on a server), extract metadata and content from a
broad range of file formats, normalize that metadata into a consistent set of fields, and
make everything searchable — delivered as a self-hosted server via docker-compose.

## Guiding architecture

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
                    Postgres: structured query + full-text search (Phase 3)
```

**Two-layer metadata model**
- **Raw layer** (`RawMetadataAttribute`): every value a provider reports, kept verbatim under
  a schema namespace (e.g. `dc`, `exif`, `tika`). Lossless source of truth → promotion can be
  re-run without re-reading files.
- **Canonical layer** (typed `AttributeBase` tables): the curated projection, produced by the
  normalizer. Only mapped keys land here; unmapped keys stay raw (curation backlog).

## Key decisions

- **Extraction:** Apache **Tika** (sidecar) for broad formats + content text. **meta-cli**
  (libavformat) for deep media. `FileMetadataProvider` for filesystem facts.
- **Search:** PostgreSQL **full-text search** (tsvector columns already in the schema), **not
  Lucene** — revisit only if PG FTS proves insufficient.
- **Normalization rules live in code** (`MetadataNormalizer`), not in data: the right value
  transform depends on the source, so mapping + transform sit together, reference typed
  definition constants, and need no migration to change.
- **meta-cli / file stay canonical providers** (not converted to raw): they are curated and
  map cleanly via typed constants, so they don't have the key-sprawl the raw layer solves.
  The system runs raw and canonical providers side by side.

---

## Phase 1 — Integrate Tika ✅ DONE

- [x] `tika-server` dev sidecar via `make start-tika` (+ `TikaUrl` config, graceful degradation)
- [x] `TikaService` calling `/rmeta/text` (recursive metadata + content), JSON parsed
- [x] `TikaProvider` mapping the document + embedded resources (archive entries → sub-resources)

## Phase 2 — Two-layer metadata model ✅ DONE

- [x] `RawMetadataAttribute` entity + migration (the raw layer)
- [x] `MetadataNormalizer` — code-based promotion rules `(namespace, key) → definition` with
      per-rule value transforms; unmapped keys stay raw
- [x] `ValueCoercer` — fail-soft, invariant-culture transforms (int/float/seconds/ISO date/EXIF date/text)
- [x] Tika wired through raw → promote (verified: `dc:title` → canonical General/Title, 0 "Other" sprawl)
- [x] Content extraction stored to `IndexedFileContents.Content` (every format)
- [x] Re-normalize command — `POST /api/metadata/renormalize` (rebuild canonical from raw, no file reads)
- [x] Unmapped-keys curation view — `GET /api/metadata/unmapped` (counts + sample values)
- [x] Test project converted to xUnit, in the solution, 26 tests (coercers + normalizer)

## Phase 3 — Search + dockerization ✅ DONE

### Search
- [x] Populate the FTS tsvectors via `SearchVectorService`: server-side `to_tsvector` UPDATE over
      the configured `Languages` (+ `simple`), run at write time (`MetadataService`,
      `RenormalizationService`) and as a backfill (`POST /api/metadata/reindex-search`). GIN
      indexes on both vectors (`Phase3SearchIndexes` migration; verified the planner uses them).
      Chose stored+indexed vectors over generated columns to keep languages config-driven.
- [x] `SearchService` — single Postgres query: content + metadata matches unioned, grouped per
      file, `ts_rank`-ordered, `ts_headline` snippets, optional path-prefix filter. Wired both the
      MVC `SearchController` (server-rendered) and `GET /api/search` (JSON).
- [x] Search results UI — query box, content/metadata toggles, `<mark>` snippets, paging, links to
      metadata/browse; functional Advanced search (folder filter). XSS-safe snippet rendering.
- [x] Seamless multilingual search: the language-agnostic `simple` pass is accent-folded (the
      `unaccent` extension, `AddUnaccentExtension` migration) and prefix-matched, so partial and
      accented words match in **any** language with no language selection — verified `cafe`→café,
      `hauser`→Häuser, `resume`→résumé, `biblio`→Bibliothek, `управлен`→управление. Configured
      `Languages` still add proper (accented) stemming on top. CJK still needs an extension.

### Dockerization
- [x] Multi-stage `Dockerfile` (.NET 10): SDK build → meta-cli build (cmake + ffmpeg-dev on the
      matching base) → aspnet runtime + ffmpeg libs + `file` + meta-cli binary + app. Verified
      end-to-end: meta-cli links libav*.so.60/58 at runtime, no ABI mismatch.
- [x] `docker-compose.yml`: `db` (postgres + volume + healthcheck), `tika` (apache/tika + port
      healthcheck), `librarian` (built image, `depends_on` db healthy, env-wired, library
      bind-mount RW, port). Auto-migration provisions the schema on first run.
- [x] `make up` / `make down` / `make logs` / `make ps`; README updated for the compose flow.
- [x] Fixed a latent bug: `appsettings.json` log level `"Info"` → `"Information"` (only the
      Development override was valid, so Production/containers crashed at startup).

---

## Backlog / known follow-ups

- **Cross-provider precedence:** an mp3's title is currently stored once from meta-cli and once
  from Tika (both with provenance). Add a precedence/dedup pass for the canonical view.
- **Convert meta-cli/file to raw** (optional unification): the 105 ffmpeg-alias code rules are
  already generated and ready if a unified raw layer for media is wanted.
- **Metadata editing / `.meta` write-back:** `SaveMetadataAsync` is unimplemented; closing the
  edit loop (UI + write to `.meta`) is still open.
- **Authentication:** there is none. The server exposes the whole filesystem — do not expose it
  on a network without auth / a reverse proxy.
- **Indexing robustness:** shared `DbContext` across the recursive walk; no FS-watcher event
  debounce; full-subtree reindex per event. Worth hardening as per-file work gets heavier.
- **Vocabulary data bugs:** some units in `MetadataAttributes.csv` are wrong (e.g. Artist = "dB").
  Add a validation test.
- **CJK search:** stock Postgres can't word-segment Chinese/Japanese/Thai. If that content
  matters, add an extension (`pgroonga` / `zhparser`). Latin/Cyrillic/Greek work today.

## Dev workflow

```sh
make check-deps          # what's installed
make start-db            # PostgreSQL container
make start-tika          # Apache Tika container
make run                 # build + run at http://localhost:5080 (auto-migrates)
make app | cli | clean   # build .NET | build meta-cli | clean
make stop-db | clean-db  # manage the dev database
```

Configuration is env-var driven (also `Librarian/appsettings.json`): `ConnectionStrings__DB`,
`BaseDirectory`, `TikaUrl`, `MetadataCliPath`.
