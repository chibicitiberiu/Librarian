# Librarian — developer convenience Makefile
#
# Quick start (Linux):
#   make check-deps     # see what's installed
#   make start-db       # start a PostgreSQL container
#   make run            # build + run the web server (auto-applies migrations)
#
# Run "make help" for the full list of targets.

# ---- .NET ----
SOLUTION    := New Librarian.sln
WEB_PROJECT := Librarian/Librarian.csproj
CONFIG      ?= Debug

# ---- Dev database (container) ----
DB_CONTAINER ?= librarian-pg
DB_IMAGE     ?= postgres:17-alpine
DB_PORT      ?= 55432
DB_USER      ?= librarian
DB_PASSWORD  ?= librarian
DB_NAME      ?= librarian
DB_CONNECTION ?= Host=localhost;Port=$(DB_PORT);Database=$(DB_NAME);Username=$(DB_USER);Password=$(DB_PASSWORD);Include Error Detail=true

# ---- Dev runtime paths/port (overridable, e.g. make run LIBRARY_DIR=/data/media) ----
LIBRARY_DIR ?= $(CURDIR)/.dev-library
APPDATA_DIR ?= $(CURDIR)/.dev-appdata
HTTP_PORT   ?= 5080

# ---- meta-cli (C++ libavformat tool) ----
CLI_DIR   := meta-cli
CLI_BUILD := $(CLI_DIR)/build
CLI_BIN   := $(CLI_BUILD)/meta-cli

# Container engine: prefer docker, fall back to podman
CONTAINER_ENGINE ?= $(shell command -v docker >/dev/null 2>&1 && echo docker || echo podman)

.DEFAULT_GOAL := all
.PHONY: all app cli submodules run check-deps start-db stop-db clean-db clean help

all: app cli ## Build everything (.NET solution + meta-cli)

app: ## Build the .NET solution
	dotnet build "$(SOLUTION)" -c $(CONFIG)

submodules: ## Check out git submodules (argparse, nlohmann/json) if missing
	@if git submodule status | grep -q '^-'; then \
		echo "Initializing git submodules..."; \
		git submodule update --init --recursive; \
	fi

cli: submodules ## Build the meta-cli C++ tool (needs cmake + ffmpeg dev libraries)
	@command -v cmake >/dev/null 2>&1 || { \
		echo "ERROR: cmake not found (Fedora: sudo dnf install cmake)."; exit 1; }
	@pkg-config --exists libavformat || { \
		echo "ERROR: libavformat not found. Install the ffmpeg development libraries:"; \
		echo "  Fedora:  sudo dnf install ffmpeg-free-devel   (or ffmpeg-devel from RPM Fusion)"; \
		echo "  Debian:  sudo apt install libavformat-dev libavcodec-dev libavutil-dev"; \
		exit 1; }
	cmake -S $(CLI_DIR) -B $(CLI_BUILD) -DCMAKE_BUILD_TYPE=Release
	cmake --build $(CLI_BUILD)
	@echo "Built $(CLI_BIN)"

run: app ## Build + run the web server against the dev database
	@mkdir -p "$(LIBRARY_DIR)" "$(APPDATA_DIR)"
	@echo "Library: $(LIBRARY_DIR)"
	@echo "Server:  http://localhost:$(HTTP_PORT)"
	ASPNETCORE_ENVIRONMENT=Development \
		ASPNETCORE_URLS="http://localhost:$(HTTP_PORT)" \
		ConnectionStrings__DB="$(DB_CONNECTION)" \
		BaseDirectory="$(LIBRARY_DIR)" \
		AppDataDirectory="$(APPDATA_DIR)" \
		MetadataCliPath="$(CURDIR)/$(CLI_BIN)" \
		dotnet run --project "$(WEB_PROJECT)" -c $(CONFIG) --no-build --no-launch-profile

start-db: ## Start the PostgreSQL dev database container
	@[ -n "$(CONTAINER_ENGINE)" ] || { echo "ERROR: neither docker nor podman found."; exit 1; }
	@if $(CONTAINER_ENGINE) ps -a --format '{{.Names}}' | grep -qx "$(DB_CONTAINER)"; then \
		$(CONTAINER_ENGINE) start "$(DB_CONTAINER)" >/dev/null && \
		echo "Started existing container '$(DB_CONTAINER)'."; \
	else \
		$(CONTAINER_ENGINE) run -d --name "$(DB_CONTAINER)" \
			-e POSTGRES_USER=$(DB_USER) -e POSTGRES_PASSWORD=$(DB_PASSWORD) -e POSTGRES_DB=$(DB_NAME) \
			-p $(DB_PORT):5432 "$(DB_IMAGE)" >/dev/null && \
		echo "Created and started container '$(DB_CONTAINER)' on port $(DB_PORT)."; \
	fi
	@echo "Waiting for PostgreSQL to accept connections..."
	@for i in $$(seq 1 30); do \
		if $(CONTAINER_ENGINE) exec "$(DB_CONTAINER)" pg_isready -U $(DB_USER) -d $(DB_NAME) >/dev/null 2>&1; then \
			echo "Database is ready."; exit 0; \
		fi; \
		sleep 1; \
	done; \
	echo "WARNING: database did not become ready in time."; exit 1

stop-db: ## Stop the dev database container (keeps data)
	-$(CONTAINER_ENGINE) stop "$(DB_CONTAINER)"

clean-db: ## Stop and delete the dev database container (destroys data)
	-$(CONTAINER_ENGINE) rm -f "$(DB_CONTAINER)"

check-deps: ## Check that build/run dependencies are installed
	@echo "Dependencies:"
	@printf "  %-20s" ".NET 10 SDK"; \
		if dotnet --list-sdks 2>/dev/null | grep -q '^10\.'; then echo "OK"; \
		else echo "MISSING  -> install the .NET 10 SDK"; fi
	@printf "  %-20s" "container engine"; \
		if [ -n "$(CONTAINER_ENGINE)" ]; then echo "OK ($(CONTAINER_ENGINE))"; \
		else echo "MISSING  -> install docker or podman"; fi
	@printf "  %-20s" "file"; \
		if command -v file >/dev/null 2>&1; then echo "OK"; \
		else echo "MISSING  -> file-type detection disabled"; fi
	@printf "  %-20s" "cmake (meta-cli)"; \
		if command -v cmake >/dev/null 2>&1; then echo "OK"; else echo "MISSING"; fi
	@printf "  %-20s" "C++ compiler"; \
		if command -v g++ >/dev/null 2>&1 || command -v clang++ >/dev/null 2>&1; then echo "OK"; else echo "MISSING"; fi
	@printf "  %-20s" "libavformat"; \
		if pkg-config --exists libavformat 2>/dev/null; then echo "OK"; \
		else echo "MISSING  -> meta-cli media metadata disabled (install ffmpeg dev libs)"; fi

clean: ## Remove build artifacts (.NET + meta-cli)
	dotnet clean "$(SOLUTION)" -c $(CONFIG)
	rm -rf $(CLI_BUILD)

help: ## Show this help
	@echo "Librarian make targets:"
	@grep -E '^[a-zA-Z_-]+:.*?## .*$$' $(MAKEFILE_LIST) \
		| sort \
		| awk 'BEGIN {FS = ":.*?## "}; {printf "  \033[36m%-12s\033[0m %s\n", $$1, $$2}'
