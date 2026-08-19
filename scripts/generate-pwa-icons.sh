#!/usr/bin/env bash
#
# Rasterizes each app's hand-authored PWA icon SVG into the PNG set its
# manifest and its apple-touch-icon <link> point at (task 368).
#
# WHY PNGs EXIST AT ALL, given the SVGs render fine:
# Android/Chrome accepts an SVG manifest icon, so task 354 shipped SVG-only
# and that half works. iOS Safari's Add to Home Screen does not read the
# manifest's icons at all - it reads `<link rel="apple-touch-icon">`, and that
# link must point at a PNG. Without one, an installed icon on iOS is a
# screenshot of the page, which is why these files are generated rather than
# left to the SVG.
#
# The SVGs stay the source of truth: edit `$SOURCE` below, re-run this, commit
# the regenerated PNGs. Do not hand-edit a PNG.
#
# THE SOURCE SVG MUST BE FULL-BLEED AND OPAQUE. iOS masks an apple-touch-icon
# to its own squircle and fills transparency outside it with black, so an icon
# that rounds its own corners gets black corners on a home screen. Both source
# SVGs are square edge-to-edge for this reason - see their own comments.
#
# Requires one rasterizer. It prefers the portable ones and falls back to what
# macOS ships, so this runs with no install on a Mac:
#   - rsvg-convert   (brew install librsvg / apt install librsvg2-bin)
#   - magick         (ImageMagick 7)
#   - qlmanage+sips  (macOS built-ins, no install)
#
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

# One line per output set: <source svg>|<manifest icon dir>|<next app dir>
TARGETS=(
  "frontend/customer-web/public/icons/icon.svg|frontend/customer-web/public/icons|frontend/customer-web/src/app"
  "frontend/provider-web/public/icon.svg|frontend/provider-web/public|frontend/provider-web/src/app"
)

# 192/512 are the two manifest sizes Chrome's installability check looks for,
# and live beside the manifest that names them.
MANIFEST_SIZES=(192 512)

# 180 is the apple-touch-icon size iOS asks for (@3x of a 60pt home-screen
# icon). It goes to `src/app/apple-icon.png`, Next's file convention, which
# emits the <link rel="apple-touch-icon"> itself - rather than to public/ with
# a hand-written <link>, which would have to be kept in sync by hand and
# would sit awkwardly next to `metadata.icons` and the `favicon.ico` file
# convention both apps already use.
APPLE_SIZE=180

rasterize() {
  local svg="$1" out="$2" size="$3"

  if command -v rsvg-convert >/dev/null 2>&1; then
    rsvg-convert --width="$size" --height="$size" --output="$out" "$svg"
  elif command -v magick >/dev/null 2>&1; then
    magick -background none -density 384 "$svg" -resize "${size}x${size}" "$out"
  elif command -v qlmanage >/dev/null 2>&1 && command -v sips >/dev/null 2>&1; then
    # Quick Look renders at one size only, so render large once and downscale -
    # scaling a 1024px render down beats asking it for 180px directly.
    local work
    work="$(mktemp -d)"
    trap 'rm -rf "$work"' RETURN
    cp "$svg" "$work/icon.svg"
    qlmanage -t -s 1024 -o "$work" "$work/icon.svg" >/dev/null 2>&1
    [ -f "$work/icon.svg.png" ] || { echo "qlmanage produced no thumbnail for $svg" >&2; return 1; }
    sips -s format png --resampleHeightWidth "$size" "$size" "$work/icon.svg.png" --out "$out" >/dev/null
  else
    echo "No rasterizer found. Install librsvg (rsvg-convert) or ImageMagick (magick)." >&2
    return 1
  fi
}

for target in "${TARGETS[@]}"; do
  IFS='|' read -r source_rel icon_dir_rel app_dir_rel <<< "$target"
  source_svg="$REPO_ROOT/$source_rel"
  out_dir="$REPO_ROOT/$icon_dir_rel"
  app_dir="$REPO_ROOT/$app_dir_rel"

  # A malformed SVG is not a hypothetical: customer-web's icon carried a
  # literal `--` inside an XML comment until task 368, which is a
  # well-formedness error, so the icon rendered nowhere and nothing said so.
  python3 -c "import xml.etree.ElementTree as ET, sys; ET.parse(sys.argv[1])" "$source_svg"

  for size in "${MANIFEST_SIZES[@]}"; do
    out="$out_dir/icon-${size}.png"
    rasterize "$source_svg" "$out" "$size"
    echo "  $source_rel -> ${out#"$REPO_ROOT/"} (${size}x${size})"
  done

  apple_out="$app_dir/apple-icon.png"
  rasterize "$source_svg" "$apple_out" "$APPLE_SIZE"
  echo "  $source_rel -> ${apple_out#"$REPO_ROOT/"} (${APPLE_SIZE}x${APPLE_SIZE})"
done

echo "Done. Commit the regenerated PNGs alongside any SVG change."
