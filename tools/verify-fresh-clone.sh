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
UNITY_CLI="${UNITY_CLI:-}"
UNITY_TIMEOUT="${UNITY_TIMEOUT:-180}"
UNITY_TEST_TIMEOUT="${UNITY_TEST_TIMEOUT:-300}"
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

resolve_unity_cli() {
  if [[ -n "$UNITY_CLI" ]]; then
    if [[ -x "$UNITY_CLI" ]]; then
      return 0
    fi
    if command -v "$UNITY_CLI" >/dev/null 2>&1; then
      UNITY_CLI="$(command -v "$UNITY_CLI")"
      return 0
    fi
    return 1
  fi
  if [[ -x "${HOME}/.local/bin/unity" ]]; then
    UNITY_CLI="${HOME}/.local/bin/unity"
    return 0
  fi
  if command -v unity >/dev/null 2>&1; then
    UNITY_CLI="$(command -v unity)"
    return 0
  fi
  return 1
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

run_editmode_tests_batchmode() {
  local project="$1"
  local logfile="$2"
  local xml="$3"
  run_unity "$project" "$logfile" -runTests -testPlatform EditMode -testResults "$xml"
}

run_editmode_tests_cli() {
  local project="$1"
  local xml="$2"
  local shell_timeout=$((UNITY_TEST_TIMEOUT + 30))
  local -a cmd=("$UNITY_CLI" test "$project" --mode EditMode --output "$xml" --timeout "$UNITY_TEST_TIMEOUT")
  cmd+=(--no-banner --non-interactive)

  set +e
  timeout --signal=TERM --kill-after=20 "$shell_timeout" "${cmd[@]}"
  local rc=$?
  set -e

  if [[ $rc -eq 124 || $rc -eq 137 ]]; then
    die "unity test timed out after ${UNITY_TEST_TIMEOUT}s (output: $xml). Process killed — will not hang."
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
if resolve_unity_cli; then
  echo "verify-fresh-clone: UNITY_CLI=${UNITY_CLI}"
else
  echo "verify-fresh-clone: UNITY_CLI=missing (will fall back to Editor -batchmode -runTests; see docs/TOOLING.md)"
fi

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
if resolve_unity_cli; then
  echo "verify-fresh-clone: using unity test (CLI)..."
  run_editmode_tests_cli "$CLONE" "$TEST_XML"
else
  echo "WARN: unity CLI not found — falling back to Editor -batchmode -runTests (may not emit XML on this project)."
  echo "      Install Unity CLI 1.0.0-beta.6+ to ~/.local/bin/unity — see docs/TOOLING.md."
  run_editmode_tests_batchmode "$CLONE" "$TEST_LOG" "$TEST_XML"
fi
test_rc=$?
set -e

if [[ -f "$TEST_LOG" ]]; then
  if log_has_compile_errors "$TEST_LOG"; then
    die "compile errors during EditMode tests"
  fi
  if has_license_error "$TEST_LOG"; then
    license_fail
  fi
fi
if [[ ! -f "$TEST_XML" ]]; then
  if [[ $test_rc -ne 0 ]]; then
    die "EditMode tests produced no XML and Unity exited ${test_rc} (log: ${TEST_LOG:-none})"
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
# unity test exit 6 = test failures (already handled via XML); other non-zero = tooling error.
if [[ $test_rc -ne 0 && $test_rc -ne 6 ]]; then
  die "EditMode test runner exited ${test_rc} despite green XML (log: ${TEST_LOG:-unity CLI})"
fi

echo "OK: EditMode tests passed."
echo "verify-fresh-clone: PASS"
exit 0
