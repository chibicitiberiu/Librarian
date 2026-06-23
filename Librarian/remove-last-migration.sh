#!/usr/bin/env bash
# Removes the most recent (unapplied) EF Core migration.
# Make sure dotnet-ef is installed: dotnet tool install --global dotnet-ef
set -euo pipefail

dotnet ef migrations remove --context PostgresDatabaseContext
