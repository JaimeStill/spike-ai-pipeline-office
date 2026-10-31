# spike-ai-pipeline-office

The Open XML CLI of spike-ai-pipeline. The repository is managed with the marathon workflow;
start from `context/README.md`.

- **Standards:** `STANDARDS.md`, with pointers into the architecture repository.
- **Layout:** `SpikeAiPipeline.Office.slnx`; the CLI in `src/SpikeAiPipeline.Office`, its xUnit v3
  tests in `test/SpikeAiPipeline.Office.Tests` on Microsoft.Testing.Platform (`global.json`);
  the binary is `spike-ai-pipeline-office`. The committed fixtures are in `fixtures/`, regenerated
  by `mise run fixtures` (`fixtures/README.md`), never by the check.
- **Check:** `mise run check`: shellcheck, `dotnet format --verify-no-changes`, a build and a
  NativeAOT publish for this machine into `.state/aot/` with warnings as errors, then the tests,
  whose CLI tests run that published binary.
- **CI:** `.github/workflows/ci.yml` runs `mise run check` on pull requests into main and pushes
  to main, on x64 and arm64; marathon's merge (`.claude/marathon.toml`) waits on the pull
  request's checks. main's ruleset is `.github/rulesets/main.json`, applied with `gh api
  repos/JaimeStill/spike-ai-pipeline-office/rulesets --method POST --input
  .github/rulesets/main.json`.
- **Release:** a pushed `vX.Y.Z` tag runs `.github/workflows/release.yml`: the NativeAOT binary
  for linux-x64 and linux-arm64, each checked that its `--version` is the tag's (the csproj's
  `Version` must match the tag), packed with LICENSE as
  `spike-ai-pipeline-office-vX.Y.Z-<rid>.tar.gz`, then the release from the tag's CHANGELOG.md
  section with both archives and `SHA256SUMS`.
- **Currency:** `mise run currency`: NuGet packages, mise tools, and GitHub Action pins.
- **Goals:** the spike's tasks live in the coordinator's roadmap,
  `standards-lab/context/roadmap.toml`, under `experiment.ai.spike-ai-pipeline`. Its goal record
  is in the root repository, `spike-ai-pipeline`, at
  `context/goals/experiment.ai.spike-ai-pipeline.md`.
- **Dependencies:** NuGet packages at published versions; the .NET SDK pinned in mise.
- **References:** the repositories this spike reads are keys in the coordinator's
  `references.toml` and `references.local.toml`. The spike reads them and never writes to them.
