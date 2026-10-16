#!/usr/bin/env bash
# Flags `const` field initializers that reference static properties (=>) or static readonly
# members — those are not compile-time constants (CS0133 in Unity/C#).
set -euo pipefail

root="${1:-.}"
project="$root/Assets/_Project"
errors=0

if [[ ! -d "$project" ]]; then
  echo "check-const-initializers: skip (no Assets/_Project)"
  exit 0
fi

nonconst_names=()
while IFS= read -r line; do
  [[ -z "$line" ]] && continue
  nonconst_names+=("$line")
done < <(
  rg -No --glob '*.cs' -e 'static\s+\w+\s+(\w+)\s*=>' "$project" 2>/dev/null \
    | sort -u
  rg -No --glob '*.cs' -e 'static\s+readonly\s+\w+\s+(\w+)\s*=' "$project" 2>/dev/null \
    | sort -u
)

if [[ ${#nonconst_names[@]} -eq 0 ]]; then
  echo "check-const-initializers: OK (no non-const static members indexed)"
  exit 0
fi

while IFS= read -r -d '' file; do
  while IFS= read -r line_num; do
    line="$(sed -n "${line_num}p" "$file")"
    for name in "${nonconst_names[@]}"; do
      if [[ "$line" =~ \bconst\b ]] && [[ "$line" == *"$name"* ]]; then
        echo "check-const-initializers: $file:$line_num const initializer references non-const '$name':"
        echo "  $line"
        errors=$((errors + 1))
      fi
    done
  done < <(rg -n '^\s*(public|private|protected|internal)?\s*(static\s+)?const\s+' "$file" 2>/dev/null | cut -d: -f1 || true)
done < <(find "$project" -name '*.cs' -print0)

if [[ "$errors" -gt 0 ]]; then
  echo "check-const-initializers: $errors issue(s) (use const literals, or static readonly)"
  exit 1
fi

echo "check-const-initializers: OK"
