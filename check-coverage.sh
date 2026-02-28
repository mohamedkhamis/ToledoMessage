#!/usr/bin/env bash
# check-coverage.sh — Validate code coverage meets project thresholds.
#
# Thresholds (from tasks.md T134):
#   >=80% overall line coverage  (server + crypto + shared; Blazor Client excluded —
#                                  WebAssembly UI requires bUnit/Playwright, not xUnit)
#   >=90% crypto library         (ToledoMessage.Crypto)
#
# Usage: bash check-coverage.sh [--open-report]
#
# Requirements:
#   dotnet reportgenerator global tool:
#     dotnet tool install -g dotnet-reportgenerator-globaltool
#
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "$0")" && pwd)"
RESULTS_DIR="$REPO_ROOT/coverage-results"
REPORT_DIR="$REPO_ROOT/coverage-report"

# On Windows (Git Bash / MSYS), convert Unix paths to Windows paths for native tools
if command -v cygpath &>/dev/null; then
  W_RESULTS="$(cygpath -w "$RESULTS_DIR")"
  W_REPORT="$( cygpath -w "$REPORT_DIR")"
else
  W_RESULTS="$RESULTS_DIR"
  W_REPORT="$REPORT_DIR"
fi

OVERALL_THRESHOLD=80
CRYPTO_THRESHOLD=90

GREEN='\033[0;32m'; RED='\033[0;31m'; NC='\033[0m'
pass() { echo -e "${GREEN}PASS ✓${NC}  $1"; }
fail() { echo -e "${RED}FAIL ✗${NC}  $1"; FAILED=1; }
FAILED=0

echo "=== Code Coverage Validation ==="
echo "Overall threshold (excl. Blazor Client): >= ${OVERALL_THRESHOLD}%"
echo "Crypto threshold                        : >= ${CRYPTO_THRESHOLD}%"
echo

# ─── 1. Run tests ────────────────────────────────────────────────────────────
echo "Running unit tests with coverage..."
rm -rf "$RESULTS_DIR"

dotnet test "$REPO_ROOT/tests/ToledoMessage.Crypto.Tests" \
  --collect:"XPlat Code Coverage" \
  --results-directory "$RESULTS_DIR/crypto" \
  --nologo -v q

dotnet test "$REPO_ROOT/tests/ToledoMessage.Server.Tests" \
  --collect:"XPlat Code Coverage" \
  --results-directory "$RESULTS_DIR/server" \
  --nologo -v q

dotnet test "$REPO_ROOT/tests/ToledoMessage.Client.Tests" \
  --collect:"XPlat Code Coverage" \
  --results-directory "$RESULTS_DIR/client" \
  --nologo -v q

echo

# ─── 2. Generate reports ─────────────────────────────────────────────────────
echo "Generating coverage report..."

# Full report (all assemblies, for human review)
reportgenerator \
  "-reports:$W_RESULTS/**/*.xml" \
  "-targetdir:$W_REPORT" \
  -reporttypes:"Html;TextSummary;Cobertura" \
  "-assemblyfilters:+ToledoMessage;+ToledoMessage.*;+Toledo.SharedKernel;-*Tests;-*Benchmarks" \
  "-classfilters:-*Migrations*;-*Program" \
  -verbosity:Warning

# Server-side-only report (no Blazor Client) — used for threshold checking
reportgenerator \
  "-reports:$W_RESULTS/**/*.xml" \
  "-targetdir:$W_REPORT/server-only" \
  -reporttypes:"Cobertura" \
  "-assemblyfilters:+ToledoMessage;+ToledoMessage.Crypto;+ToledoMessage.Shared;+Toledo.SharedKernel;-*Tests;-*Benchmarks;-ToledoMessage.Client" \
  "-classfilters:-*Migrations*;-*Program" \
  -verbosity:Warning

# Crypto-only report — for crypto-specific threshold
reportgenerator \
  "-reports:$W_RESULTS/crypto/**/*.xml" \
  "-targetdir:$W_REPORT/crypto-only" \
  -reporttypes:"Cobertura" \
  "-assemblyfilters:+ToledoMessage.Crypto" \
  -verbosity:Warning

echo

# ─── 3. Extract coverage percentages from Cobertura XML ──────────────────────
extract_line_rate() {
  # First <coverage line-rate="..."> in the Cobertura XML
  local xml="$1"
  grep -o 'line-rate="[0-9.]*"' "$xml" | head -1 | grep -o '[0-9.]*'
}

OVERALL_RATE=$(extract_line_rate "$REPORT_DIR/server-only/Cobertura.xml" 2>/dev/null || echo "0")
CRYPTO_RATE=$( extract_line_rate "$REPORT_DIR/crypto-only/Cobertura.xml"  2>/dev/null || echo "0")

OVERALL_PCT=$(awk "BEGIN {printf \"%.1f\", $OVERALL_RATE * 100}")
CRYPTO_PCT=$( awk "BEGIN {printf \"%.1f\", $CRYPTO_RATE  * 100}")

# ─── 4. Threshold checks ─────────────────────────────────────────────────────
echo "=== Results ==="
echo "Overall (server + crypto + shared)  : ${OVERALL_PCT}%   threshold: ${OVERALL_THRESHOLD}%"
echo "ToledoMessage.Crypto                : ${CRYPTO_PCT}%   threshold: ${CRYPTO_THRESHOLD}%"
echo

if awk "BEGIN { exit !($OVERALL_PCT >= $OVERALL_THRESHOLD) }"; then
  pass "Overall coverage ${OVERALL_PCT}% >= ${OVERALL_THRESHOLD}%"
else
  fail "Overall coverage ${OVERALL_PCT}% < ${OVERALL_THRESHOLD}%  — add tests to close the gap"
fi

if awk "BEGIN { exit !($CRYPTO_PCT >= $CRYPTO_THRESHOLD) }"; then
  pass "Crypto coverage  ${CRYPTO_PCT}% >= ${CRYPTO_THRESHOLD}%"
else
  fail "Crypto coverage  ${CRYPTO_PCT}% < ${CRYPTO_THRESHOLD}%  — add crypto unit tests"
fi

echo
echo "HTML report: $REPORT_DIR/index.html"
if [[ "${1:-}" == "--open-report" ]]; then
  if command -v xdg-open &>/dev/null; then
    xdg-open "$REPORT_DIR/index.html"
  elif command -v open &>/dev/null; then
    open "$REPORT_DIR/index.html"
  fi
fi

exit $FAILED
