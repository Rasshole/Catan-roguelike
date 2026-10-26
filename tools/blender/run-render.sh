#!/usr/bin/env bash
set -euo pipefail
root="$(cd "$(dirname "$0")/../.." && pwd)"
export BLENDER_TILE_REPO_ROOT="$root"
export BLENDER_TILE_RES="${BLENDER_TILE_RES:-1024}"
if command -v xvfb-run >/dev/null 2>&1; then
  xvfb-run -a blender -b --python "$root/tools/blender/render_hex_terrains.py"
else
  blender -b --python "$root/tools/blender/render_hex_terrains.py"
fi
