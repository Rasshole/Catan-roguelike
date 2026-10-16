#!/usr/bin/env bash
set -euo pipefail

root="${1:-.}"
declare -A seen=()
errors=0

while IFS= read -r -d '' meta; do
  guid="$(grep -E '^guid: ' "$meta" | head -n1 | sed 's/^guid: //' || true)"
  if [[ -z "$guid" ]]; then
    continue
  fi

  if [[ ! "$guid" =~ ^[0-9a-f]{32}$ ]]; then
    echo "INVALID guid length/format in $meta: '$guid' (${#guid} chars)" >&2
    errors=$((errors + 1))
    continue
  fi

  if [[ -n "${seen[$guid]:-}" ]]; then
    echo "DUPLICATE guid $guid in:" >&2
    echo "  ${seen[$guid]}" >&2
    echo "  $meta" >&2
    errors=$((errors + 1))
    continue
  fi
  seen[$guid]="$meta"
done < <(find "$root" -name '*.meta' -print0)

if [[ "$errors" -gt 0 ]]; then
  echo "check-meta-guids: $errors error(s)" >&2
  exit 1
fi

echo "check-meta-guids: OK ($(printf '%s' "${#seen[@]}") guids)"
