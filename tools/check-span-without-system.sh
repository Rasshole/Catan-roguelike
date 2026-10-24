#!/usr/bin/env bash
# Span/ReadOnlySpan require System; Unity game scripts often omit it (Object ambiguity).
set -euo pipefail

root="${1:-.}"
assets="$root/Assets/_Project"
if [[ ! -d "$assets" ]]; then
  echo "check-span-without-system: missing $assets" >&2
  exit 1
fi

fail=0
while IFS= read -r file; do
  [[ -z "$file" ]] && continue
  if rg -q 'using System;' "$file" 2>/dev/null; then
    continue
  fi
  if rg -q 'global using System;' "$file" 2>/dev/null; then
    continue
  fi
  echo "check-span-without-system: Span types without 'using System;' in $file" >&2
  rg -n 'ReadOnlySpan<|Span<' "$file" >&2 || true
  fail=1
done < <(rg -l 'ReadOnlySpan<|Span<' "$assets" --glob '*.cs' 2>/dev/null || true)

if [[ "$fail" -ne 0 ]]; then
  exit 1
fi

echo "check-span-without-system: OK"
