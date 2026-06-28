#!/usr/bin/env bash
#
# make-test-dataset.sh — mirror a real media library into a tiny test dataset.
#
# Videos and audio are truncated to the first N seconds with *stream copy*, so the container, codecs,
# stream layout, chapters and metadata are preserved byte-for-byte from the original — only the duration
# shrinks. Everything else (subtitles, .nfo/.opf, cover art, .txt, scripts — already small) is copied
# verbatim. The result mirrors the real folder structure and all its edge cases (mismatched extensions,
# sidecars, multi-disc layouts, release-scene names) at a fraction of the size, so it can be committed and
# used to regression-test indexing + item/collection detection as the algorithm evolves.
#
# Usage:   tools/make-test-dataset.sh <source-dir> <dest-dir> [seconds]
# Example: tools/make-test-dataset.sh "/mnt/zizo/Media/Children" testdata/library 10
#
set -uo pipefail

SRC="${1:?usage: make-test-dataset.sh <source-dir> <dest-dir> [seconds]}"
DEST="${2:?usage: make-test-dataset.sh <source-dir> <dest-dir> [seconds]}"
SECS="${3:-10}"
MAX_COPY_BYTES=$((2 * 1024 * 1024))   # copy non-media files up to 2 MiB verbatim; trim larger ones

SRC="${SRC%/}"
DEST="${DEST%/}"

command -v ffmpeg  >/dev/null || { echo "ERROR: ffmpeg not found";  exit 1; }
command -v ffprobe >/dev/null || { echo "ERROR: ffprobe not found"; exit 1; }
[ -d "$SRC" ] || { echo "ERROR: source '$SRC' is not a directory"; exit 1; }

# Media detection is content-based (ffprobe), not extension-based, so a video with a wrong/missing
# extension is still truncated rather than copied whole.
is_media() {
  ffprobe -v error -show_entries stream=codec_type -of csv=p=0 "$1" 2>/dev/null \
    | grep -qE '^(video|audio)$'
}

mkdir -p "$DEST"

# Recreate the directory tree first (so even empty folders survive — they matter to the heuristic).
find "$SRC" -type d -print0 | while IFS= read -r -d '' d; do
  mkdir -p "$DEST/${d#"$SRC"/}"
done

find "$SRC" -type f -print0 | while IFS= read -r -d '' f; do
  rel="${f#"$SRC"/}"
  out="$DEST/$rel"
  mkdir -p "$(dirname "$out")"

  if is_media "$f"; then
    # Truncate, copying every stream + global metadata + chapters.
    if ffmpeg -nostdin -v error -y -i "$f" -t "$SECS" \
              -map 0 -map_metadata 0 -map_chapters 0 -c copy "$out" 2>/dev/null; then
      echo "trunc  $rel"
    elif ffmpeg -nostdin -v error -y -i "$f" -t "$SECS" -map_metadata 0 "$out" 2>/dev/null; then
      # Stream copy can fail on some containers (no keyframe in window); fall back to a tiny re-encode.
      echo "reenc  $rel"
    else
      echo "SKIP   $rel  (ffmpeg could not process it)"
    fi
  else
    sz=$(stat -c%s "$f" 2>/dev/null || echo 0)
    if [ "$sz" -le "$MAX_COPY_BYTES" ]; then
      cp -p "$f" "$out"
      echo "copy   $rel"
    else
      head -c "$MAX_COPY_BYTES" "$f" > "$out"
      echo "trim   $rel  (large non-media → first ${MAX_COPY_BYTES} bytes)"
    fi
  fi
done

echo
echo "Test dataset written to: $DEST"
echo "Source size: $(du -sh "$SRC"  2>/dev/null | cut -f1)   Dataset size: $(du -sh "$DEST" 2>/dev/null | cut -f1)"
