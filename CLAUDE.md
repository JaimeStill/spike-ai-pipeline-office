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
- **Currency:** `mise run currency`.
- **Goals:** the spike's tasks live in the coordinator's roadmap,
  `standards-lab/context/roadmap.toml`, under `experiment.ai.spike-ai-pipeline`. Its goal record
  is in the root repository, `spike-ai-pipeline`, at
  `context/goals/experiment.ai.spike-ai-pipeline.md`.
- **Dependencies:** NuGet packages at published versions; the .NET SDK pinned in mise.
- **References:** the repositories this spike reads are keys in the coordinator's
  `references.toml` and `references.local.toml`. The spike reads them and never writes to them.
