#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "$0")/.." && pwd)"
"$root/tools/check-meta-guids.sh" "$root"
"$root/tools/check-core-usings.sh" "$root"
