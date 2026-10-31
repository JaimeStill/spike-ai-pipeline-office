# spike-ai-pipeline-office

The Open XML CLI of spike-ai-pipeline, released by tag and built for spike-ai-pipeline-processing
to run as a system tool. This context records only working knowledge the code and the README do
not express; the spike's question and evidence live in the root repository's `context/README.md`.

## Capability map

- **cli**: `spike-ai-pipeline-office <input> [--format json|markdown] [--media <dir>]`, stdout
  only the rendering; exit codes 0 converted, 1 media not written, 2 usage, 3 refused, 4 failed;
  a skipped invalid reference a warning on stderr.
- **extract**: docx, xlsx, and pptx (and their macro-enabled, template, and slide-show variants,
  read as their base format, macros never run) to the JSON document model, through the Open XML
  SDK. The model, schema `"1"`, is office's output contract, versioned with its tags and recorded
  in `CHANGELOG.md`.
- **render**: the same model as Markdown.
- **media**: embedded pictures written out beside the model, as stored, content-addressed by
  SHA-256 and stored extension; linked pictures as their targets, never fetched.
- **refuse**: legacy doc, xls, and ppt, encrypted or malformed packages, and tables nested past
  eight, before any output.
- **release**: NativeAOT binaries for linux-x64 and linux-arm64, by `vX.Y.Z` tag, as
  `spike-ai-pipeline-office-vX.Y.Z-linux-{x64,arm64}.tar.gz` (binary and LICENSE) and
  `SHA256SUMS`, notes from the tag's CHANGELOG section.
- **baseline**: office, markitdown, and xberg scored over the pool on the laptop; the findings
  are `docs/findings/office-cli.md`, the evidence for the root's evidence 12.
