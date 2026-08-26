#!/usr/bin/env bash
# Verificér at en frisk git-klon åbner i Unity uden compile-fejl.
# Se docs/TOOLING.md.
#
# Exit codes:
#   0  OK
#   1  FAIL (compile, missing scripts, tests, Unity-fejl)
#   2  Unity-licens mangler — kræver .ulf (se docs/BLOCKED.md)
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
ORIGIN_URL="$(git -C "$ROOT" remote get-url origin)"
REF="${1:-$(git -C "$ROOT" rev-parse HEAD)}"
UNITY_EDITOR="${UNITY_EDITOR:-/home/box/Unity/Hub/Editor/6000.3.15f1/Editor/Unity}"
UNITY_TIMEOUT="${UNITY_TIMEOUT:-180}"
SCENE_REL="Assets/_Project/Scenes/Game.unity"

TMP=""
cleanup() {
  if [[ -n "${TMP}" && -d "${TMP}" ]]; then
    rm -rf "${TMP}"
  fi
}
trap cleanup EXIT

die() {
  echo "FAIL: $*" >&2
  exit 1
}

license_fail() {
  echo "FAIL: Unity-licens påkrævet (No valid Unity Editor license)." >&2
  echo "Upload ALF på https://license.unity3d.com/manual og returnér .ulf. Se docs/BLOCKED.md." >&2
  exit 2
}

has_license_error() {
  local log="$1"
  [[ -f "$log" ]] || return 1
  grep -qiE 'No valid Unity Editor license' "$log"
}

run_unity() {
  local project="$1"
  local logfile="$2"
  shift 2
  local -a cmd=()
  if [[ -z "${DISPLAY:-}" ]] && command -v xvfb-run >/dev/null 2>&1; then
    cmd+=(xvfb-run -a)
  fi
  cmd+=("$UNITY_EDITOR" -batchmode -nographics -quit -projectPath "$project" -logFile "$logfile")
  cmd+=("$@")

  set +e
  timeout --signal=TERM --kill-after=20 "${UNITY_TIMEOUT}" "${cmd[@]}"
  local rc=$?
  set -e

  if has_license_error "$logfile"; then
    license_fail
  fi
  if [[ $rc -eq 124 || $rc -eq 137 ]]; then
    die "Unity timed out after ${UNITY_TIMEOUT}s (log: $logfile). Process killed — will not hang."
  fi
  return "$rc"
}

log_has_compile_errors() {
  local log="$1"
  grep -E 'error CS[0-9]+:|Scripts have compiler errors|Compilation failed' "$log" >/dev/null 2>&1
}

log_has_missing_scripts() {
  local log="$1"
  grep -Ei 'The referenced script \(Unknown\)|Missing \(Mono Script\)|Can.t add script behaviour' "$log" >/dev/null 2>&1
}

echo "verify-fresh-clone: origin=${ORIGIN_URL}"
echo "verify-fresh-clone: ref=${REF}"
echo "verify-fresh-clone: UNITY_EDITOR=${UNITY_EDITOR}"

[[ -x "$UNITY_EDITOR" ]] || die "Unity editor not executable: $UNITY_EDITOR"

TMP="$(mktemp -d /tmp/catan-fresh-clone.XXXXXX)"
CLONE="${TMP}/repo"
echo "verify-fresh-clone: clone dir=${CLONE}"

git clone --quiet "$ORIGIN_URL" "$CLONE"
git -C "$CLONE" checkout --quiet "$REF"

OPEN_LOG="${TMP}/unity-open.log"
TEST_LOG="${TMP}/unity-tests.log"
TEST_XML="${TMP}/editmode-results.xml"
SCENE_PATH="${CLONE}/${SCENE_REL}"

echo "verify-fresh-clone: opening project (batchmode)..."
set +e
run_unity "$CLONE" "$OPEN_LOG"
open_rc=$?
set -e

if log_has_compile_errors "$OPEN_LOG"; then
  echo "---- compile errors in ${OPEN_LOG} ----" >&2
  grep -E 'error CS[0-9]+:|Scripts have compiler errors|Compilation failed' "$OPEN_LOG" >&2 || true
  die "compile errors on fresh clone"
fi
if log_has_missing_scripts "$OPEN_LOG"; then
  die "missing script references on fresh clone (see ${OPEN_LOG})"
fi
if [[ $open_rc -ne 0 ]]; then
  die "Unity open exited ${open_rc} (log: ${OPEN_LOG})"
fi
echo "OK: Unity opened project without compile/missing-script errors."

if [[ -f "$SCENE_PATH" ]]; then
  if grep -Ei 'Failed to load scene|Cannot open scene|is not a valid scene' "$OPEN_LOG" >/dev/null 2>&1; then
    die "Game.unity present but unloadable"
  fi
  echo "OK: ${SCENE_REL} is present in the clone."
else
  echo "WARN: ${SCENE_REL} absent (Fase 0.7 not done) — not treating as FAIL."
  echo "      When Game.unity is committed, this script will FAIL if it is missing or unloadable."
fi

echo "verify-fresh-clone: running EditMode tests..."
set +e
run_unity "$CLONE" "$TEST_LOG" -runTests -testPlatform EditMode -testResults "$TEST_XML"
test_rc=$?
set -e

if log_has_compile_errors "$TEST_LOG"; then
  die "compile errors during EditMode tests"
fi
if [[ ! -f "$TEST_XML" ]]; then
  if [[ $test_rc -ne 0 ]]; then
    die "EditMode tests produced no XML and Unity exited ${test_rc} (log: ${TEST_LOG})"
  fi
  die "EditMode tests produced no results XML"
fi

failed="$(grep -oE 'failed="[0-9]+"' "$TEST_XML" | head -n1 | grep -oE '[0-9]+' || true)"
inconclusive="$(grep -oE 'inconclusive="[0-9]+"' "$TEST_XML" | head -n1 | grep -oE '[0-9]+' || true)"
result_failed="$(grep -c 'result="Failed"' "$TEST_XML" || true)"
failed="${failed:-0}"
inconclusive="${inconclusive:-0}"

if [[ "$failed" != "0" || "$inconclusive" != "0" || "$result_failed" -gt 0 ]]; then
  echo "---- ${TEST_XML} ----" >&2
  cat "$TEST_XML" >&2 || true
  die "EditMode tests failed (failed=${failed} inconclusive=${inconclusive})"
fi
if [[ $test_rc -ne 0 ]]; then
  die "EditMode Unity exited ${test_rc} despite XML (log: ${TEST_LOG})"
fi

echo "OK: EditMode tests passed."
echo "verify-fresh-clone: PASS"
exit 0
