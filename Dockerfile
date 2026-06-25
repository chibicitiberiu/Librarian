# Librarian — multi-stage build.
#
# Three stages:
#   1. dotnet-build : restore + publish the ASP.NET app (.NET 10 SDK).
#   2. cli-build    : compile the meta-cli C++ tool against the ffmpeg dev libraries.
#   3. final        : aspnet runtime + ffmpeg runtime libs + meta-cli binary + the app.
#
# meta-cli is built on the .NET *SDK* image and run on the matching .NET *runtime*
# image. Both share the same Debian release for a given .NET version, so the ffmpeg
# libraries meta-cli links against (build) and the ones installed at runtime are the
# same major version — no ABI mismatch.

# ---- 1. Build the .NET app ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS dotnet-build
WORKDIR /src

# Restore against just the project files first, so the restore layer is cached
# until a dependency actually changes.
COPY ["Librarian/Librarian.csproj", "Librarian/"]
COPY ["Librarian.Core/Librarian.Core.csproj", "Librarian.Core/"]
COPY ["Librarian.Metadata/Librarian.Metadata.csproj", "Librarian.Metadata/"]
RUN dotnet restore "Librarian/Librarian.csproj"

COPY . .
RUN dotnet publish "Librarian/Librarian.csproj" -c Release -o /app/publish /p:UseAppHost=false

# ---- 2. Build meta-cli ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS cli-build
RUN apt-get update && apt-get install -y --no-install-recommends \
        cmake g++ make pkg-config \
        libavformat-dev libavcodec-dev libavutil-dev \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /src
# The git submodules under meta-cli/import (argparse, nlohmann/json) must be checked
# out in the build context — `make cli` / `git submodule update --init` does this.
COPY meta-cli/ meta-cli/
RUN cmake -S meta-cli -B meta-cli/build -DCMAKE_BUILD_TYPE=Release \
    && cmake --build meta-cli/build

# ---- 3. Runtime image ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
# ffmpeg pulls the libav* shared libraries meta-cli needs; file backs file-type detection;
# libimage-exiftool-perl provides the exiftool binary that augments Tika with deep embedded tags.
RUN apt-get update && apt-get install -y --no-install-recommends \
        ffmpeg file libimage-exiftool-perl \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=dotnet-build /app/publish ./
COPY --from=cli-build /src/meta-cli/build/meta-cli /app/meta-cli/meta-cli

# Defaults; docker-compose overrides the connection string, library path, Tika URL, etc.
ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_HTTP_PORTS=8080 \
    AppDataDirectory=/data \
    MetadataCliPath=/app/meta-cli/meta-cli \
    ExifToolPath=exiftool
EXPOSE 8080

ENTRYPOINT ["dotnet", "Librarian.dll"]
