#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "$0")/.." && pwd)"
"$root/tools/check-meta-guids.sh" "$root"
"$root/tools/check-cs-meta.sh" "$root"
"$root/tools/check-internal-test-refs.sh" "$root"
"$root/tools/check-core-usings.sh" "$root"
chmod +x "$root/tools/check-no-engine-references.sh" 2>/dev/null || true
"$root/tools/check-no-engine-references.sh" "$root"
"$root/tools/check-unity-api-misrefs.sh" "$root"
chmod +x "$root/tools/check-span-without-system.sh" 2>/dev/null || true
"$root/tools/check-span-without-system.sh" "$root"
chmod +x "$root/tools/check-bare-destroy.sh" 2>/dev/null || true
"$root/tools/check-bare-destroy.sh" "$root"
chmod +x "$root/tools/check-const-initializers.sh" 2>/dev/null || true
"$root/tools/check-const-initializers.sh" "$root"
