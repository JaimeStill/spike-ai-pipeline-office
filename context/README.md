# spike-ai-pipeline-office

The Open XML CLI of spike-ai-pipeline, released by tag and built for spike-ai-pipeline-processing
to run as a system tool. This context records only working knowledge the code and the README do
not express; the spike's question and evidence live in the root repository's `context/README.md`.

## Capability map

The README gives each capability's contract, and `CHANGELOG.md` records its changes.

- **cli**: one input path, the rendering on stdout, media to `--media`, exit codes 0 to 4.
- **extract**: docx, xlsx, and pptx, and their macro-enabled, template, and slide-show variants,
  to the JSON document model (schema `"1"`, office's output contract) through the Open XML SDK.
- **render**: the same model as Markdown.
- **media**: embedded pictures written as stored, content-addressed by SHA-256; linked pictures
  as their targets.
- **refuse**: legacy, encrypted, and malformed packages, and tables nested past eight, before any
  output.
- **release**: NativeAOT binaries for linux-x64 and linux-arm64 by `vX.Y.Z` tag.
- **baseline**: office, markitdown, and xberg scored over the pool on the laptop
  (`docs/findings/office-cli.md`, the root's evidence 12).
