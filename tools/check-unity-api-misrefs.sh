#!/usr/bin/env bash
# Catch common Unity API namespace mistakes that compile in IDE hints but fail in Unity.
set -euo pipefail

root="${1:-.}"
assets="$root/Assets"
project="$assets/_Project"

if [[ ! -d "$assets" ]]; then
  echo "check-unity-api-misrefs: missing $assets" >&2
  exit 1
fi

errors=0

hits="$(rg -n 'UnityEngine\.UI\.Canvas' "$assets" --glob '*.cs' 2>/dev/null || true)"
if [[ -n "$hits" ]]; then
  echo "check-unity-api-misrefs: UnityEngine.UI.Canvas is invalid (Canvas is UnityEngine.Canvas):" >&2
  echo "$hits" >&2
  errors=$((errors + 1))
fi

if [[ -d "$project" ]]; then
  nunit_assert_hits="$(rg -n 'Assert\.(Greater|Less|GreaterOrEqual|LessOrEqual|That|Contains|IsEmpty|Multiple)\b' \
    "$project" --glob '*.cs' 2>/dev/null || true)"
  if [[ -n "$nunit_assert_hits" ]]; then
    echo "check-unity-api-misrefs: NUnit-only Assert APIs in runtime Assets/_Project (use UnityEngine.Assertions.Assert.IsTrue etc.):" >&2
    echo "$nunit_assert_hits" >&2
    errors=$((errors + 1))
  fi

  tolerance_hits="$(rg -n 'Assert\.AreEqual\([^;]+,[^;]+,\s*[0-9]' "$project" --glob '*.cs' 2>/dev/null || true)"
  if [[ -n "$tolerance_hits" ]]; then
    echo "check-unity-api-misrefs: Assert.AreEqual with numeric third arg (NUnit tolerance) in Assets/_Project:" >&2
    echo "$tolerance_hits" >&2
    errors=$((errors + 1))
  fi
fi

if [[ "$errors" -gt 0 ]]; then
  exit 1
fi

echo "check-unity-api-misrefs: OK"
