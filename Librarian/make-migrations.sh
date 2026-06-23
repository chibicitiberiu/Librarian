#!/usr/bin/env bash
# Creates a new EF Core migration. Usage: ./make-migrations.sh <MigrationName>
# Make sure dotnet-ef is installed: dotnet tool install --global dotnet-ef
set -euo pipefail

if [ -z "${1:-}" ]; then
    echo "Usage: $0 <MigrationName>" >&2
    exit 1
fi

mkdir -p DB/Migrations
dotnet ef migrations add "$1" --context PostgresDatabaseContext --output-dir DB/Migrations
