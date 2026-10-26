#!/usr/bin/env bash
# Flag internal types under Assets/_Project that are referenced from Assets/Tests.
set -euo pipefail

root="${1:-.}"
project_dir="$root/Assets/_Project"
tests_dir="$root/Assets/Tests"

if [[ ! -d "$project_dir" ]] || [[ ! -d "$tests_dir" ]]; then
  echo "check-internal-test-refs: missing project or tests tree" >&2
  exit 1
fi

errors="$(python3 - "$project_dir" "$tests_dir" <<'PY'
import os
import re
import sys

project_dir = sys.argv[1]
tests_dir = sys.argv[2]

INTERNAL_TYPE_RE = re.compile(
    r"^[ \t]*internal (?:sealed |static |abstract |partial |readonly )*"
    r"(?:class|struct|enum|interface|record) ([A-Za-z_][A-Za-z0-9_]*)",
    re.MULTILINE,
)
NS_RE = re.compile(
    r"^namespace ([A-Za-z0-9_.]+)\s*[{;]",
    re.MULTILINE,
)


def strip_strings_and_comments(source: str) -> str:
    source = re.sub(r"/\*.*?\*/", "", source, flags=re.DOTALL)
    source = re.sub(r'@"(?:[^"]|"")*"', '""', source)
    source = re.sub(r'"([^"\\]|\\.)*"', '""', source)
    source = re.sub(r"'([^'\\]|\\.)*'", "''", source)
    source = re.sub(r"//.*", "", source)
    return source


def index_internal_types(directory: str) -> dict[str, str]:
    types: dict[str, str] = {}
    for dirpath, _, filenames in os.walk(directory):
        for name in filenames:
            if not name.endswith(".cs"):
                continue
            path = os.path.join(dirpath, name)
            text = open(path, encoding="utf-8").read()
            for match in INTERNAL_TYPE_RE.finditer(text):
                type_name = match.group(1)
                types[type_name] = path
    return types


def type_used_in_tests(body: str, type_name: str) -> bool:
    if re.search(rf"\b{re.escape(type_name)}\b", body):
        return True
    return False


internal_types = index_internal_types(project_dir)
errors: list[str] = []

for dirpath, _, filenames in os.walk(tests_dir):
    for name in filenames:
        if not name.endswith(".cs"):
            continue
        test_path = os.path.join(dirpath, name)
        raw = open(test_path, encoding="utf-8").read()
        body = strip_strings_and_comments(raw)
        for type_name, decl_path in sorted(internal_types.items()):
            if type_used_in_tests(body, type_name):
                rel_decl = os.path.relpath(decl_path, project_dir)
                rel_test = os.path.relpath(test_path, tests_dir)
                errors.append(
                    f"internal type {type_name} ({rel_decl}) referenced from Tests/{rel_test}"
                )

for line in errors:
    print(line)
print(len(errors))
PY
)"

count="$(echo "$errors" | tail -n1)"
if [[ "$count" != "0" ]]; then
  echo "$errors" | head -n -1 >&2
  echo "check-internal-test-refs: $count error(s)" >&2
  exit 1
fi

echo "check-internal-test-refs: OK"
