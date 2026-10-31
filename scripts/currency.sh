#!/usr/bin/env bash
# currency reports every dependency, toolchain, and pin that trails its latest
# release, one line each, and exits non-zero when it reports any. It reads the
# network and writes nothing outside obj/. Run it through `mise run currency`.
#
# Each scan captures its command's output in a variable before reading it, so
# under set -e a scan command that fails fails currency instead of reporting
# nothing.
set -euo pipefail
cd "$MISE_PROJECT_ROOT"

stale=0
report() {
	echo "$1"
	stale=1
}

# matches prints each match of a grep pattern in the given files that exist.
# A missing file or no match is no output; any other grep error fails.
matches() {
	local pattern=$1 file
	local files=()
	shift
	for file in "$@"; do
		if [ -e "$file" ]; then files+=("$file"); fi
	done
	[ ${#files[@]} -gt 0 ] || return 0
	grep -ho "$pattern" "${files[@]}" || [ $? -eq 1 ]
}

# Direct NuGet packages with a newer release.
packages=$(dotnet list package --outdated --format json |
	jq -r '.projects[] | .path as $p | .frameworks[]?.topLevelPackages[]? |
		"\($p | split("/") | last): \(.id) \(.requestedVersion) -> \(.latestVersion)"')
while read -r line; do
	[ -n "$line" ] && report "$line"
done <<<"$packages"

# mise tools, the .NET SDK among them, pinned below their latest version.
outdated=$(mise outdated --bump --local --json |
	jq -r 'to_entries[] | "\(.value.source.path | split("/") | last): \(.key) \(.value.requested) -> \(.value.bump)"')
while read -r line; do
	[ -n "$line" ] && report "$line"
done <<<"$outdated"

# GitHub Actions pinned below the action's latest release. With no workflows,
# the glob stays unexpanded and matches skips it.
workflows=(.github/workflows/*.yml)
actions=$(matches 'uses: *[^ ]*@[^ ]*' "${workflows[@]}" | sed 's/uses: *//' | sort -u)
for uses in $actions; do
	action=${uses%@*}
	pin=${uses#*@}
	repo=$(echo "$action" | cut -d/ -f1,2)
	latest=$(gh api "repos/$repo/releases/latest" --jq .tag_name)
	[ "$pin" = "$latest" ] || report ".github/workflows: $action $pin -> $latest"
done

exit "$stale"
