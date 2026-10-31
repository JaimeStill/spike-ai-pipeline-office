#!/usr/bin/env bash
# baseline runs the baseline tier on this machine, never in CI: office's NativeAOT binary over the
# committed pool (fixtures/generated and fixtures/realworld), nothing else, its Markdown scored
# against the ground truth, each run measured for time and peak memory. office calls no model.
# Run it through `mise run baseline`, which passes its arguments here:
#
#   mise run baseline                     keeps nothing: the results go to
#                                         .state/baseline/results.json, overwritten each run, and
#                                         the summary to the terminal
#   mise run baseline -- --record [name]  records the run in docs/findings/office-cli/runs/<name>/
#                                         results.json, <name> by default <date>-<n>; an existing
#                                         run directory is never overwritten
#
# A name is letters, digits, '.', '_', and '-', and doesn't start with '.'. The builds go to
# .state/baseline/ either way. BASELINE_RUNS (5) and BASELINE_TIMEOUT (60 s a run) override the
# defaults.
set -euo pipefail
cd "$MISE_PROJECT_ROOT"

usage() {
	echo "usage: mise run baseline [-- --record [name]]" >&2
	exit 2
}

runs_dir=docs/findings/office-cli/runs
state=.state/baseline
record=""
case "$#" in
0) ;;
1 | 2)
	[[ $1 == --record ]] || usage
	if [[ $# -eq 2 ]]; then
		record=$2
		if [[ ! $record =~ ^[A-Za-z0-9_-][A-Za-z0-9._-]*$ ]]; then
			echo "baseline: run name '$record' isn't letters, digits, '.', '_', and '-', not starting with '.'" >&2
			exit 2
		fi
	else
		# The first <date>-<n> that no run directory has.
		day=$(date +%F)
		n=1
		while [[ -e $runs_dir/$day-$n ]]; do
			n=$((n + 1))
		done
		record=$day-$n
	fi
	# Refused here, before the builds, and again by the bench when it writes, as a run takes a
	# while.
	if [[ -e $runs_dir/$record ]]; then
		echo "baseline: $runs_dir/$record exists; a recorded run is never overwritten" >&2
		exit 1
	fi
	;;
*) usage ;;
esac

case "$(uname -m)" in
x86_64) rid=linux-x64 ;;
aarch64) rid=linux-arm64 ;;
*)
	echo "baseline: no runtime identifier for $(uname -m)" >&2
	exit 1
	;;
esac

dotnet publish src/SpikeAiPipeline.Office -c Release -r "$rid" -warnaserror -o "$state/office"
dotnet build bench/SpikeAiPipeline.Office.Baseline -c Release -warnaserror -o "$state/bench"
cc -O2 -Wall -Wextra -Werror -o "$state/rusage" bench/rusage.c

if [[ -n $record ]]; then
	out=(--record "$runs_dir/$record")
else
	out=(--out "$state/results.json")
fi
"$state/bench/SpikeAiPipeline.Office.Baseline" \
	--root . \
	--office "$state/office/spike-ai-pipeline-office" \
	--rusage "$state/rusage" \
	--runs "${BASELINE_RUNS:-5}" \
	--timeout "${BASELINE_TIMEOUT:-60}" \
	"${out[@]}"
