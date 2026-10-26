#!/usr/bin/env bash
# currency reports every dependency and toolchain that trails its latest
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

# Direct NuGet packages with a newer release.
packages=$(dotnet list package --outdated --format json |
	jq -r '.projects[] | .path as $p | .frameworks[]?.topLevelPackages[]? |
		"\($p | split("/") | last): \(.id) \(.requestedVersion) -> \(.latestVersion)"')
while read -r line; do
	[ -n "$line" ] && report "$line"
done <<<"$packages"

# mise tools, the .NET SDK among them, pinned below their latest version.
outdated=$(mise outdated --bump --local --json |
	jq -r 'to_entries[] | "mise.toml: \(.key) \(.value.requested) -> \(.value.bump)"')
while read -r line; do
	[ -n "$line" ] && report "$line"
done <<<"$outdated"

exit "$stale"
