#!/usr/bin/env bash
# Applies pending EF Core migrations to the database.
# Make sure dotnet-ef is installed: dotnet tool install --global dotnet-ef
# Override the target DB with: ConnectionStrings__DB="..." ./migrate.sh
set -euo pipefail

dotnet ef database update --context PostgresDatabaseContext
