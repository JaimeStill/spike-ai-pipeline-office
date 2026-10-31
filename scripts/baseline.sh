#!/usr/bin/env bash
# baseline runs the baseline tier on this machine, never in CI: office's NativeAOT binary,
# markitdown, and xberg over the committed pool (fixtures/generated and fixtures/realworld),
# nothing else, each tool's Markdown scored against the ground truth, each run measured for time
# and peak memory. It writes one new directory, docs/findings/office-cli/runs/<date>-<n>/, holding
# results.json, and its builds under .state/baseline/. No tool calls a model. Run it through
# `mise run baseline`; BASELINE_RUNS (5) and BASELINE_TIMEOUT (60 s a run) override the defaults,
# and BASELINE_OUT the runs directory.
set -euo pipefail
cd "$MISE_PROJECT_ROOT"

# The baseline's tools are in mise.baseline.toml, loaded only in this environment, so neither the
# check nor CI installs them.
export MISE_ENV=baseline
mise install --quiet

case "$(uname -m)" in
x86_64) rid=linux-x64 ;;
aarch64) rid=linux-arm64 ;;
*)
	echo "baseline: no runtime identifier for $(uname -m)"
	exit 1
	;;
esac

state=.state/baseline
dotnet publish src/SpikeAiPipeline.Office -c Release -r "$rid" -warnaserror -o "$state/office"
dotnet build bench/SpikeAiPipeline.Office.Baseline -c Release -warnaserror -o "$state/bench"
cc -O2 -Wall -Wextra -Werror -o "$state/rusage" bench/rusage.c

markitdown=$(mise which markitdown)
xberg=$(mise which xberg)
"$state/bench/SpikeAiPipeline.Office.Baseline" \
	--root . \
	--office "$state/office/spike-ai-pipeline-office" \
	--markitdown "$markitdown" \
	--xberg "$xberg" \
	--rusage "$state/rusage" \
	--runs "${BASELINE_RUNS:-5}" \
	--timeout "${BASELINE_TIMEOUT:-60}" \
	--out "${BASELINE_OUT:-docs/findings/office-cli/runs}"
