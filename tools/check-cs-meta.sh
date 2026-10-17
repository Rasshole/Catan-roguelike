#!/usr/bin/env bash
set -euo pipefail

root="${1:-.}"
assets="$root/Assets"
errors=0

if [[ ! -d "$assets" ]]; then
  echo "check-cs-meta: missing $assets" >&2
  exit 1
fi

while IFS= read -r -d '' cs; do
  if [[ ! -f "${cs}.meta" ]]; then
    echo "MISSING meta for $cs" >&2
    errors=$((errors + 1))
  fi
done < <(find "$assets" -name '*.cs' -print0)

if [[ "$errors" -gt 0 ]]; then
  echo "check-cs-meta: $errors error(s)" >&2
  exit 1
fi

count="$(find "$assets" -name '*.cs' | wc -l | tr -d ' ')"
echo "check-cs-meta: OK ($count .cs files)"
