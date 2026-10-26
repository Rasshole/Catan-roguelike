#!/usr/bin/env bash
# Fail when a noEngineReferences asmdef folder uses UnityEngine types.
set -euo pipefail

root="${1:-.}"
assets="$root/Assets"

if [[ ! -d "$assets" ]]; then
  echo "check-no-engine-references: missing $assets" >&2
  exit 1
fi

python3 - "$assets" <<'PY'
import os
import re
import sys

assets = sys.argv[1]
errors = []

USING_UNITY = re.compile(r"^\s*using\s+UnityEngine\b", re.MULTILINE)
UNITY_TYPES = re.compile(r"\b(Vector2|Vector3|Vector4|Quaternion|Mathf|Color)\b")
DEBUG_CALL = re.compile(r"\bDebug\.(Log|LogWarning|LogError|Assert)\b")


def strip_strings_and_comments(source: str) -> str:
    source = re.sub(r"/\*.*?\*/", "", source, flags=re.DOTALL)
    source = re.sub(r'@"(?:[^"]|"")*"', '""', source)
    source = re.sub(r'"([^"\\]|\\.)*"', '""', source)
    source = re.sub(r"'([^'\\]|\\.)*'", "''", source)
    source = re.sub(r"//.*", "", source)
    return source


def asmdef_is_no_engine(path: str) -> bool:
    text = open(path, encoding="utf-8").read()
    return bool(re.search(r'"noEngineReferences"\s*:\s*true', text))


def scan_file(asm_name: str, path: str) -> None:
    raw = open(path, encoding="utf-8").read()
    body = strip_strings_and_comments(raw)

    if USING_UNITY.search(body):
        for i, line in enumerate(raw.splitlines(), 1):
            if USING_UNITY.search(line):
                errors.append(f"{asm_name}: using UnityEngine in {path}:{i}")
        return

    for match in UNITY_TYPES.finditer(body):
        line = body[: match.start()].count("\n") + 1
        errors.append(
            f"{asm_name}: Unity type '{match.group(1)}' in {path}:{line}"
        )

    for match in DEBUG_CALL.finditer(body):
        line = body[: match.start()].count("\n") + 1
        errors.append(
            f"{asm_name}: Debug.{match.group(1)} in {path}:{line}"
        )


for dirpath, _, filenames in os.walk(assets):
    for name in filenames:
        if not name.endswith(".asmdef"):
            continue
        asm_path = os.path.join(dirpath, name)
        if not asmdef_is_no_engine(asm_path):
            continue
        asm_name = os.path.splitext(name)[0]
        for root, _, cs_names in os.walk(dirpath):
            for cs_name in cs_names:
                if not cs_name.endswith(".cs"):
                    continue
                scan_file(asm_name, os.path.join(root, cs_name))

if errors:
    print("check-no-engine-references:", file=sys.stderr)
    for err in errors:
        print(f"  {err}", file=sys.stderr)
    sys.exit(1)

print("check-no-engine-references: OK")
PY
