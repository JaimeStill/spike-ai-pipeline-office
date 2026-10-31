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
format, malformed at the ZIP or part level, or past a limit of office's (tables nested more than
eight deep, named with where: `refused: <path>: tables nested 9 deep (limit 8), at body
paragraph 112`), 1 when the media could not be written, and 4 when the conversion failed in a way
office expects of no document, a fault of office's own, never reported as any other code. A
refusal happens before any output is written, with its reason on stderr, and media are written
only once the whole conversion has succeeded, before stdout. A single invalid reference, such as
a header reference of a type no header has or a sheet whose relationship names no part, is
skipped and the rest converts, exit code 0, with one line on stderr for each:
`spike-ai-pipeline-office: warning: <path>: <what was skipped>`. The JSON carries a `schema`
version, the output contract's.

A macro-enabled document, a template, or a slide show (docm, dotx, dotm, xlsm, xltx, xltm, pptm,
potx, potm, ppsx) is read as its base format, by its main part's content type whatever the file is
named: its macros are ignored, never run.

A docx document's blocks are its body's in order, its tracked changes accepted. An ordered list
has its `start`, its first number, when that isn't 1: its numbering's start, or for a list a
paragraph interrupted, the number its count resumes at, as Word numbers it; Markdown numbers its
items from there. A cell of a row that starts late (`w:gridBefore`) has the grid columns it
skips as `skipBefore`, so Markdown places it in its own column. Content controls and custom XML
are read through wherever they wrap text, around runs, paragraphs, rows, or cells, as are smart
tags; a control showing its placeholder ("Choose an item.") holds a prompt, not text, and is left
out. A ruby's base text is the text, its annotation left out. An embedded OLE object is never
extracted, but the picture Word shows in its place is a media block.

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

A pptx document's blocks are its slides in presentation order (the presentation's slide list,
not the parts' names), hidden ones included and marked `hidden`, each with its `number` from 1,
its blocks, and its `notes`. A slide's shapes are read in its shape tree's order, back to front,
never re-sorted by position, and a group's shapes in place: a title placeholder's text is a
level-1 heading, its paragraphs its lines; any other shape's paragraphs are paragraphs, or list
items where they have a bullet, nested by their level, a character or picture bullet unordered
and an auto-number ordered, its list's `start` its first item's `startAt`. A paragraph's bullet is its own, or else the first its level's
properties give in the shape's list style, then for a placeholder its layout placeholder's (by
index, or else type), that one's master placeholder's, and the master's title, body, or other
text style, or for any other shape the master's other text style, and last the presentation's
default text style. A table keeps its merges as spans, the cells they cover left out; a chart is
its cached data; a picture is a media block with its `slide`, the slide's index in the
document's blocks. An empty paragraph or placeholder is left out, and a layout's prompt text
never appears. A slide's notes are its notes page's body placeholder's text, never its slide
image or slide number. In Markdown each slide is a heading, `# Slide 3`, with `(hidden)` after a
hidden one's, its title a level-2 heading below it, then its content, then its notes after a
bold `**Notes**` label.

It is released by tag, `vX.Y.Z`, with a CHANGELOG. The spike's question, evidence, and answer are
in the root repository's README.

Apache-2.0.
