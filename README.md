# spike-ai-pipeline-office

The office-document tool of [spike-ai-pipeline](https://github.com/JaimeStill/spike-ai-pipeline):
a command-line utility on Microsoft's [Open XML SDK](https://github.com/dotnet/Open-XML-SDK) that
turns docx, xlsx, and pptx files into a JSON document model (headings, paragraphs, lists, tables
and their cells, sheets, slides in order with their notes, chart series, and embedded media
written out as files), with Markdown as a second rendering of the same model.

It is published as one NativeAOT binary each for linux-x64 and linux-arm64, with no .NET runtime
needed on the host, and spike-ai-pipeline-processing runs it like any other system tool. It
renders no page images: chart data and embedded pictures take their place. Legacy binary formats
are converted to OOXML before they reach it, by LibreOffice in a container.

```sh
spike-ai-pipeline-office <input> [--format json|markdown] [--media <dir>]
```

It writes the chosen rendering of the input to stdout, JSON when `--format` is left out, and
nothing else there. Embedded media go to the `--media` directory, each distinct content once,
named by the SHA-256 of its bytes and its stored extension. The exit code is 0 on success, 2 on
a usage error (usage goes to stderr), and 3 when the document is refused: missing or unreadable,
not a docx, xlsx, or pptx package, encrypted or a legacy binary format, or malformed. A refusal
happens before any output is written, with its reason on stderr. The JSON carries a `schema`
version, the output contract's.

It is released by tag, `vX.Y.Z`, with a CHANGELOG. The spike's question, evidence, and answer are
in the root repository's README.

Apache-2.0.
