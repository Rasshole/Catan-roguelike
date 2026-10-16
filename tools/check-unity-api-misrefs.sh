#!/usr/bin/env bash
# Catch common Unity API namespace mistakes that compile in IDE hints but fail in Unity.
set -euo pipefail

root="${1:-.}"
assets="$root/Assets"

if [[ ! -d "$assets" ]]; then
  echo "check-unity-api-misrefs: missing $assets" >&2
  exit 1
fi

hits="$(rg -n 'UnityEngine\.UI\.Canvas' "$assets" --glob '*.cs' 2>/dev/null || true)"
if [[ -n "$hits" ]]; then
  echo "check-unity-api-misrefs: UnityEngine.UI.Canvas is invalid (Canvas is UnityEngine.Canvas):" >&2
  echo "$hits" >&2
  exit 1
fi

echo "check-unity-api-misrefs: OK"
