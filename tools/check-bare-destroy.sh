#!/usr/bin/env bash
set -euo pipefail

root="${1:-.}"
allowed="ColonistSceneDestroy.cs"
errors=0

while IFS= read -r -d '' file; do
  base="$(basename "$file")"
  if [[ "$base" == "$allowed" ]]; then
    continue
  fi
  if rg -n '\bDestroy\s*\(' "$file" >/dev/null 2>&1; then
    echo "bare Destroy( in $file (use ColonistSceneDestroy.SafeDestroy):"
    rg -n '\bDestroy\s*\(' "$file" || true
    errors=$((errors + 1))
  fi
done < <(find "$root/Assets/_Project" -name '*.cs' -print0)

if [[ "$errors" -gt 0 ]]; then
  echo "check-bare-destroy: $errors file(s) with bare Destroy("
  exit 1
fi

echo "check-bare-destroy: OK"
