#!/usr/bin/env bash
# Fail when Game/Tests .cs files use root-namespace Core types without `using CatanRoguelike.Core;`.
set -euo pipefail

root="${1:-.}"
core_dir="$root/Assets/_Project/Scripts/Core"
scan_dirs=(
  "$root/Assets/_Project/Scripts/Game"
  "$root/Assets/Tests"
)

if [[ ! -d "$core_dir" ]]; then
  echo "check-core-usings: missing $core_dir" >&2
  exit 1
fi

types_file="$(mktemp)"
trap 'rm -f "$types_file"' EXIT

while IFS= read -r -d '' file; do
  if ! grep -qE '^namespace CatanRoguelike\.Core\s*$' "$file"; then
    continue
  fi
  rg '^[[:space:]]*public (sealed |static |partial )*(class|struct|enum|interface|record) [A-Za-z_][A-Za-z0-9_]*' \
    "$file" -o --no-line-number 2>/dev/null \
    | sed -E 's/.*(class|struct|enum|interface|record) ([A-Za-z_][A-Za-z0-9_]*).*/\2/' \
    >>"$types_file" || true
done < <(find "$core_dir" -name '*.cs' -print0)

if [[ ! -s "$types_file" ]]; then
  echo "check-core-usings: no public types in namespace CatanRoguelike.Core" >&2
  exit 1
fi

sort -u "$types_file" -o "$types_file"
type_count="$(wc -l <"$types_file" | tr -d ' ')"

errors=0
while IFS= read -r -d '' file; do
  if grep -qE '^namespace CatanRoguelike\.Core(\s|;|$)' "$file"; then
    continue
  fi
  if grep -qE '(^|\s)using CatanRoguelike\.Core\s*;' "$file"; then
    continue
  fi
  if grep -qE '^global using CatanRoguelike\.Core\s*;' "$file"; then
    continue
  fi

  missing="$(python3 - "$file" "$types_file" <<'PY'
import re
import sys

path, types_path = sys.argv[1], sys.argv[2]
text = open(path, encoding="utf-8").read()


def strip_strings_and_comments(source: str) -> str:
    source = re.sub(r"/\*.*?\*/", "", source, flags=re.DOTALL)
    source = re.sub(r'@"(?:[^"]|"")*"', '""', source)
    source = re.sub(r'"([^"\\]|\\.)*"', '""', source)
    source = re.sub(r"'([^'\\]|\\.)*'", "''", source)
    source = re.sub(r"//.*", "", source)
    return source


text = strip_strings_and_comments(text)
types = [line.strip() for line in open(types_path, encoding="utf-8") if line.strip()]
missing = []
for name in types:
    pattern = re.compile(r"(?<!CatanRoguelike\.Core\.)\b" + re.escape(name) + r"\b")
    if pattern.search(text):
        missing.append(name)
if missing:
    print(", ".join(missing))
PY
)"
  if [[ -n "$missing" ]]; then
    echo "MISSING using CatanRoguelike.Core in $file (uses: $missing)" >&2
    errors=$((errors + 1))
  fi
done < <(
  for dir in "${scan_dirs[@]}"; do
    [[ -d "$dir" ]] && find "$dir" -name '*.cs' -print0
  done
)

if [[ "$errors" -gt 0 ]]; then
  echo "check-core-usings: $errors error(s)" >&2
  exit 1
fi

echo "check-core-usings: OK ($type_count Core types, Game/Tests scanned)"
