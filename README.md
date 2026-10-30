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
nothing else there. Embedded media go to the `--media` directory, created if missing, each
distinct content once, as stored, named by the lowercase hex SHA-256 of its bytes and its stored
extension; without `--media` nothing is written to disk, and a linked picture is never fetched.
The exit code is 0 on success, 2 on a usage error (usage goes to stderr), 3 when the document is
refused: missing or unreadable, not a docx, xlsx, or pptx package, encrypted or a legacy binary
format, or malformed, and 1 when the media could not be written. A refusal happens before any
output is written, with its reason on stderr, and media are written only once the whole
conversion has succeeded, before stdout. The JSON carries a `schema`
version, the output contract's. pptx is refused as not yet supported.

An xlsx document's blocks are its sheets in workbook order, hidden ones included and marked
`hidden`. A sheet holds a table of the cells that have a value or a formula, each row with its
number and each cell with its reference (`B2`), so the table is as sparse as the sheet; then its
charts and pictures, in the order its drawing anchors them, each picture with its `sheet`. A
cell's `value` is typed by its `valueType`: `number` (as stored, no number format applied),
`text` (rich text flattened), `boolean`, `error` (`#DIV/0!`), or `date`, a number in a date or
time format as ISO 8601: a date alone when the format shows no time, a time alone when it shows
no date and the number is under a day, both otherwise, in the workbook's 1900 or 1904 date
system. A formula's cell has its `formula` text and its cached value, or no value when the file
caches none; nothing is evaluated. A shared formula's other cells name its anchor in
`sharedFrom`; an array formula's text is on its anchor. A merged range's anchor has its spans,
and the cells it covers are left out. In Markdown each sheet is a heading of its name, with
`(hidden)` after a hidden one's, and its table a GFM table whose header row is the column letters
its cells use and whose first column is the row numbers; a cell shows its value, or with none its
formula as code (`` `=SUM(A1:A3)` ``), or a shared formula's anchor.

It is released by tag, `vX.Y.Z`, with a CHANGELOG. The spike's question, evidence, and answer are
in the root repository's README.

Apache-2.0.
