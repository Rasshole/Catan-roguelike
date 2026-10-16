#!/usr/bin/env bash
# Fail when Game/Tests .cs files use Core types without the matching `using CatanRoguelike.Core[.*];`.
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

errors="$(python3 - "$core_dir" "${scan_dirs[@]}" <<'PY'
import os
import re
import sys

core_dir = sys.argv[1]
scan_dirs = sys.argv[2:]

TYPE_RE = re.compile(
    r"^[ \t]*public (?:sealed |static |partial |readonly )*"
    r"(?:class|struct|enum|interface|record) ([A-Za-z_][A-Za-z0-9_]*)",
    re.MULTILINE,
)
NS_RE = re.compile(
    r"^namespace (CatanRoguelike\.Core(?:\.[A-Za-z0-9_]+)?)\s*[{;]",
    re.MULTILINE,
)
USING_RE = re.compile(
    r"^(?:global )?using ([A-Za-z0-9_.]+)\s*;",
    re.MULTILINE,
)


def strip_strings_and_comments(source: str) -> str:
    source = re.sub(r"/\*.*?\*/", "", source, flags=re.DOTALL)
    source = re.sub(r'@"(?:[^"]|"")*"', '""', source)
    source = re.sub(r'"([^"\\]|\\.)*"', '""', source)
    source = re.sub(r"'([^'\\]|\\.)*'", "''", source)
    source = re.sub(r"//.*", "", source)
    return source


def index_core_types(directory: str) -> dict[str, set[str]]:
    type_to_namespaces: dict[str, set[str]] = {}
    for dirpath, _, filenames in os.walk(directory):
        for name in filenames:
            if not name.endswith(".cs"):
                continue
            path = os.path.join(dirpath, name)
            text = open(path, encoding="utf-8").read()
            ns_match = NS_RE.search(text)
            if not ns_match:
                continue
            namespace = ns_match.group(1)
            for match in TYPE_RE.finditer(text):
                type_name = match.group(1)
                type_to_namespaces.setdefault(type_name, set()).add(namespace)
    return type_to_namespaces


def file_namespace(text: str) -> str | None:
    match = NS_RE.search(text)
    return match.group(1) if match else None


def parse_usings(text: str) -> set[str]:
    return {m.group(1) for m in USING_RE.finditer(text)}


def is_type_used_unqualified(body: str, type_name: str) -> bool:
    if re.search(rf"\bCatanRoguelike\.Core(?:\.[A-Za-z0-9_]+)*\.{re.escape(type_name)}\b", body):
        return False
    # Field/constant/property named like a Core type (e.g. const string GameLog).
    if re.search(
        rf"(?:public|private|protected|internal)\s+(?:static\s+)?"
        rf"(?:const\s+)?(?:readonly\s+)?(?:\w+(?:<[^>]*>)?\s+)+{re.escape(type_name)}\s*[;=]",
        body,
    ):
        return False
    return bool(re.search(rf"(?<!\.)\b{re.escape(type_name)}\b", body))


def missing_usings_for_file(path: str, type_to_namespaces: dict[str, set[str]]) -> list[str]:
    text = open(path, encoding="utf-8").read()
    file_ns = file_namespace(text)
    if file_ns and file_ns.startswith("CatanRoguelike.Core"):
        return []

    usings = parse_usings(text)
    body = strip_strings_and_comments(text)

    missing: list[str] = []
    for type_name, namespaces in sorted(type_to_namespaces.items()):
        if not is_type_used_unqualified(body, type_name):
            continue
        if any(ns in usings for ns in namespaces):
            continue
        if len(namespaces) == 1:
            missing.append(f"{type_name} (need using {next(iter(namespaces))};)")
        else:
            ns_list = ", ".join(sorted(namespaces))
            missing.append(f"{type_name} (need using one of: {ns_list}; or fully qualify)")
    return missing


type_index = index_core_types(core_dir)
if not type_index:
    print("check-core-usings: no public types under CatanRoguelike.Core*", file=sys.stderr)
    sys.exit(1)

error_count = 0
scanned = 0
for scan_dir in scan_dirs:
    if not os.path.isdir(scan_dir):
        continue
    for dirpath, _, filenames in os.walk(scan_dir):
        for name in filenames:
            if not name.endswith(".cs"):
                continue
            path = os.path.join(dirpath, name)
            scanned += 1
            missing = missing_usings_for_file(path, type_index)
            if missing:
                print(f"MISSING Core usings in {path}:", file=sys.stderr)
                for line in missing:
                    print(f"  - {line}", file=sys.stderr)
                error_count += 1

if error_count:
    print(f"check-core-usings: {error_count} error(s)", file=sys.stderr)
    sys.exit(1)

namespace_count = len({ns for namespaces in type_index.values() for ns in namespaces})
type_count = len(type_index)
print(
    f"check-core-usings: OK ({type_count} types, {namespace_count} Core namespaces, "
    f"{scanned} Game/Tests files scanned)"
)
PY
)"

status=$?
if [[ "$status" -ne 0 ]]; then
  if [[ -n "$errors" ]]; then
    echo "$errors" >&2
  fi
  exit "$status"
fi
echo "$errors"
