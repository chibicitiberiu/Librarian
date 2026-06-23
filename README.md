# Librarian

**Note: this project is early in development, and far from being ready to use.**

The Librarian is a tool that can be used to curate and search through your data collection.

## The vision

In the "Librarian", the data collection is simply a big folder on the server. The goal is to provide a user interface not unlike a file manager that allows people to browse and manage their data. In addition to what a standard file manager offers, Librarian also collects and displays metadata about each file or folder, as well as allowing users to add their own metadata. Another part of the goal is to also allow users to add formatted text, allowing the creation of a hierarchical "wiki" based on the data.

Another important feature is indexing. The goal of Librarian is to index the entire data collection (metadata and content where possible), and provide tools for searching through that index.

## Development

The project is an ASP.NET web application targeting **.NET 10**, backed by PostgreSQL. There is a small helper utility written in C++ (`meta-cli`) that uses libavformat to collect media metadata; it is optional — without it, media metadata is simply skipped.

For the web UI, I'm simply using vanilla JS. The theme is inspired by Bluecurve, an old Fedora theme.

### Quick start (Linux)

A top-level `Makefile` wraps the common tasks. From the repository root:

```sh
make check-deps   # see which dependencies are installed
make start-db     # start a PostgreSQL container (Docker or Podman)
make run          # build and run the server at http://localhost:5080
```

The database schema is created automatically on first run (migrations are applied at startup), so there is no separate migration step. By default `make run` serves a throwaway library in `./.dev-library`; point it at real data with `make run LIBRARY_DIR=/path/to/media`.

Run `make help` for the full list of targets:

| Target | Description |
| --- | --- |
| `make` / `make all` | Build everything (.NET solution + `meta-cli`) |
| `make app` | Build just the .NET solution |
| `make cli` | Build `meta-cli` (needs `cmake` + ffmpeg dev libraries) |
| `make run` | Build and run the web server |
| `make start-db` / `make stop-db` / `make clean-db` | Start / stop / delete the dev database container |
| `make check-deps` | Check that build/run dependencies are installed |
| `make clean` | Remove build artifacts |

### Prerequisites

* The **.NET 10 SDK**.
* **Docker** or **Podman** (for the dev database via `make start-db`), or your own PostgreSQL instance.
* Optional, only for building `meta-cli`: **cmake**, a **C++ compiler**, and the **ffmpeg development libraries** (Fedora: `ffmpeg-free-devel`, or `ffmpeg-devel` from RPM Fusion; Debian/Ubuntu: `libavformat-dev libavcodec-dev libavutil-dev`). The git submodules under `meta-cli/import` are checked out automatically by `make cli`.

### Configuration

Configuration lives in `Librarian/appsettings.json`. Every value can also be overridden with environment variables (which is how `make run` wires things up), e.g. `ConnectionStrings__DB`, `BaseDirectory`, `MetadataCliPath`.

* `BaseDirectory` — the root folder of the data collection. For testing, create a temporary folder and drop some media files in it.
* `ConnectionStrings:DB` — the PostgreSQL connection string. The same value is used both at runtime and when applying/creating migrations (the design-time factory reads `appsettings.json` and environment variables).
* `Languages` — languages used for full-text search via PostgreSQL's full-text search. List the configurations supported by your server with `SELECT cfgname FROM pg_ts_config;`.
* `MetadataCliPath` — path to the built `meta-cli` binary (`meta-cli/build/meta-cli` when built with `make cli`). Leave it pointing at a missing path to disable media metadata collection.

### Creating migrations

Migrations are applied automatically at startup. To create a new one after changing the model, install the EF tools (`dotnet tool install --global dotnet-ef`) and run `./make-migrations.sh <Name>` from the `Librarian` directory (or `make-migrations.cmd` on Windows).

### Windows / Visual Studio

The solution still opens and runs in Visual Studio 2022. The `meta-cli` C++ project can be built with the CMake presets (including the WSL/Linux remote workflow), and the `.cmd` scripts in the `Librarian` folder mirror the `.sh` helpers.

## Current state/screenshots

The file browser looks like this:
![Screenshot 2024-01-27 170504](https://github.com/chibicitiberiu/Librarian/assets/5184913/dbb79bd2-b625-47ca-900c-8f6180f4e72d)

Some things I want to change:

* add a "title", "description" section that can be edited (like a wiki).

Metadata editor:
![Screenshot 2024-01-27 170754](https://github.com/chibicitiberiu/Librarian/assets/5184913/e92c1c6a-317f-4b2b-8f98-bdc926ca3376)

Search is not yet implemented.
