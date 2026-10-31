# Finding: office's Markdown against markitdown and xberg

This page records the baseline tier of task `office-cli` (the root's goal record,
`context/goals/experiment.ai.spike-ai-pipeline.md`): office, markitdown, and xberg each converted
every document of the committed pool to Markdown, each Markdown was scored element by element
against the pool's ground truth, and each run was measured for time and peak memory. It also
records each tool's footprint on disk and its licenses. Every number below, every table's,
comes from the latest run,
[`office-cli/runs/2026-10-31-2/results.json`](office-cli/runs/2026-10-31-2/results.json), on the
laptop on 2026-10-31 from 17:48:49 to 17:53:21 (UTC-4); that file has every file's detail. The
earlier run, [`office-cli/runs/2026-10-31-1/results.json`](office-cli/runs/2026-10-31-1/results.json)
(16:51:19 to 16:55:41), stays committed as the record of the first scoring: it has the pooled
fidelity only, and its `chartValue` is the table-only rule this run reports as `chartValueTable`
(its counts are the same in both runs). This page takes no number from it.

No tool was asked to call a model, and none called one over the network. office calls none.
markitdown ran with no LLM client and no Document Intelligence endpoint, and as shipped: it always
runs magika, Google's file-type classifier, a small ONNX model, through onnxruntime inside
markitdown's own process, on every input, local inference no option of its CLI turns off. That is
markitdown's, not office's: office never loads markitdown, xberg, or anything of theirs (they are
installed only for the bench, which starts each as its own process), so office's rule of no
inference is untouched by this run. xberg ran with its OCR off, no configuration file, and no
cache.

## At a glance

| Question | Answer |
|---|---|
| Whose Markdown keeps the document's structure? | **office's**: it matched every element of every kind the truth holds, 1,768 of 1,768 over the pool and 1,144 of 1,144 over the 30 real-world files alone. markitdown and xberg lose most list structure in slides, almost all chart data, every header and footer, and every comment. |
| How fast? | office's median file took **17.2 ms**, xberg's **12.3 ms**, markitdown's **675 ms**. |
| How much memory? | Peak resident memory, median file: office **35.4 MiB**, xberg **38.6 MiB**, markitdown **153.4 MiB**. |
| How big? | office's linux-x64 binary is **28.80 MiB**; xberg's binary 129.16 MiB, its install 152.30 MiB; markitdown's environment 250.11 MiB, on a Python interpreter and library of another 71.85 MiB. |
| Licenses? | office Apache-2.0, every package it restores MIT. markitdown MIT, its dependencies permissive or weak copyleft (certifi MPL-2.0). xberg MIT, with LGPL and GPL shared libraries beside its binary. |
| Did any tool fail? | markitdown failed on one file, `realworld/xlsx/simple-strict.xlsx`, an ISO 29500 Strict workbook. No tool timed out. |

## The pool and the tools

The pool is `fixtures/` and nothing else (`fixtures/README.md`): 72 documents, 42 of the
generated set (python-docx, openpyxl, and python-pptx files and their LibreOffice re-saves) and
30 of the real-world set (files Word, Excel, and PowerPoint wrote, `fixtures/realworld/SOURCES.md`);
38 docx, 20 xlsx, and 14 pptx. Each has its ground truth, `*.truth.json`, the file office's own
tests check its JSON against exactly. Here office is scored on its Markdown, like the others.

| Tool | Version | Command |
|---|---|---|
| office | 0.0.0+b8a64f8, the NativeAOT binary `mise run baseline` publishes for linux-x64 from this repository | `spike-ai-pipeline-office <file> --format markdown` |
| markitdown | 0.1.9, `pipx:markitdown` with the `docx`, `xlsx`, and `pptx` extras, on CPython 3.14.8 | `markitdown <file>` |
| xberg | 1.3.10, `xberg-cli-x86_64-unknown-linux-gnu.tar.gz` | `xberg extract <file> --content-format markdown --no-config-discovery --no-cache true --disable-ocr true` |

Each tool's Markdown was parsed by Markdig 1.5.1, in the bench only (it is never in office's
binary). The host: Intel Core Ultra 9 185H, 22 logical CPUs, 32 GB of memory (32,290,188 kB),
Linux 7.2.11-arch1-1 (Omarchy, on Arch), .NET 10.0.13 for the bench.

## Structure fidelity

Each cell is found / expected, then the score, summed over every file whose truth holds that
kind; "Files" is how many files that is. Both tables are the latest run's (`fidelity.pooled` and
`fidelity.realworld` in its `results.json`; `fidelity.generated` has the generated set's alone).
Chart values are counted under two rules, the region rule this run introduced and the first run's
table-only rule beside it, so the change between them shows (both under "How the fidelity is
scored").

### The whole pool, 72 files

| Element kind | Files | office | markitdown | xberg |
|---|---|---|---|---|
| Heading | 28 | 62 / 62, **1.000** | 58 / 62, 0.935 | 59 / 62, 0.952 |
| Paragraph | 47 | 179 / 179, **1.000** | 140 / 179, 0.782 | 135 / 179, 0.754 |
| List item | 15 | 167 / 167, **1.000** | 72 / 167, 0.431 | 99 / 167, 0.593 |
| Table cell | 33 | 825 / 825, **1.000** | 730 / 825, 0.885 | 771 / 825, 0.935 |
| Sheet name | 20 | 39 / 39, **1.000** | 36 / 39, 0.923 | 29 / 39, 0.744 |
| Slide order | 13 | 38 / 38, **1.000** | 32 / 38, 0.842 | 35 / 38, 0.921 |
| Slide notes | 6 | 14 / 14, **1.000** | 14 / 14, **1.000** | 0 / 14, 0.000 (not rendered) |
| Chart value, region rule | 15 | 331 / 331, **1.000** | 22 / 331, 0.066 | 22 / 331, 0.066 |
| Chart value, table-only rule | 15 | 331 / 331, **1.000** | 47 / 331, 0.142 | 27 / 331, 0.082 |
| Media reference | 18 | 50 / 50, **1.000** | 33 / 50, 0.660 | 17 / 50, 0.340 |
| Header or footer | 6 | 32 / 32, **1.000** | 1 / 32, 0.031 (not rendered) | 1 / 32, 0.031 (not rendered) |
| Footnote or endnote | 5 | 13 / 13, **1.000** | 13 / 13, **1.000** | 11 / 13, 0.846 |
| Comment | 6 | 18 / 18, **1.000** | 0 / 18, 0.000 (not rendered) | 0 / 18, 0.000 (not rendered) |

### The real-world files alone, 30 files

The pooled table favors office. The generated set was built around the features office
implements, each fixture exercising one of them, and office's Markdown was developed against the
same ground truth the bench scores with; the other tools were not. The 30 real-world files were
written by Word, Excel, and PowerPoint (`fixtures/realworld/SOURCES.md`), not for office, and
their truth was written by hand from each file's XML, not from office's output, so this table is
the fairest comparison the pool allows. It is still not neutral: office was run against these
truths during its development too, and several of the files are the regression files of reading
faults office had and was fixed for (`fixtures/README.md`, "Real-world set").

| Element kind | Files | office | markitdown | xberg |
|---|---|---|---|---|
| Heading | 6 | 18 / 18, **1.000** | 16 / 18, 0.889 | 17 / 18, 0.944 |
| Paragraph | 19 | 89 / 89, **1.000** | 71 / 89, 0.798 | 65 / 89, 0.730 |
| List item | 5 | 91 / 91, **1.000** | 30 / 91, 0.330 | 45 / 91, 0.495 |
| Table cell | 13 | 577 / 577, **1.000** | 504 / 577, 0.873 | 539 / 577, 0.934 |
| Sheet name | 8 | 17 / 17, **1.000** | 14 / 17, 0.824 | 9 / 17, 0.529 |
| Slide order | 5 | 18 / 18, **1.000** | 16 / 18, 0.889 | 17 / 18, 0.944 |
| Slide notes | 2 | 4 / 4, **1.000** | 4 / 4, **1.000** | 0 / 4, 0.000 (not rendered) |
| Chart value, region rule | 10 | 294 / 294, **1.000** | 12 / 294, 0.041 | 12 / 294, 0.041 |
| Chart value, table-only rule | 10 | 294 / 294, **1.000** | 34 / 294, 0.116 | 22 / 294, 0.075 |
| Media reference | 8 | 18 / 18, **1.000** | 11 / 18, 0.611 | 1 / 18, 0.056 |
| Header or footer | 2 | 11 / 11, **1.000** | 1 / 11, 0.091 (not rendered) | 1 / 11, 0.091 (not rendered) |
| Footnote or endnote | 3 | 3 / 3, **1.000** | 3 / 3, **1.000** | 2 / 3, 0.667 |
| Comment | 2 | 4 / 4, **1.000** | 0 / 4, 0.000 (not rendered) | 0 / 4, 0.000 (not rendered) |

### By format

The whole pool by format, from the same run's per-file detail:

| Format | Kind | office | markitdown | xberg |
|---|---|---|---|---|
| docx | Paragraph | 135 / 135 | 128 / 135 | 99 / 135 |
| docx | List item | 79 / 79 | 72 / 79 | 71 / 79 |
| docx | Table cell | 156 / 156 | 136 / 156 | 142 / 156 |
| docx | Chart value, either rule | 206 / 206 | 0 / 206 | 0 / 206 |
| docx | Media reference | 26 / 26 | 20 / 26 | 17 / 26 |
| xlsx | Table cell | 596 / 596 | 557 / 596 | 559 / 596 |
| xlsx | Chart value, region rule | 37 / 37 | 0 / 37 | 0 / 37 |
| xlsx | Chart value, table-only rule | 37 / 37 | 25 / 37 | 25 / 37 |
| xlsx | Media reference | 11 / 11 | 0 / 11 | 0 / 11 |
| pptx | Paragraph | 44 / 44 | 12 / 44 | 36 / 44 |
| pptx | List item | 88 / 88 | 0 / 88 | 28 / 88 |
| pptx | Table cell | 73 / 73 | 37 / 73 | 70 / 73 |
| pptx | Chart value, region rule | 88 / 88 | 22 / 88 | 22 / 88 |
| pptx | Chart value, table-only rule | 88 / 88 | 22 / 88 | 2 / 88 |
| pptx | Media reference | 13 / 13 | 13 / 13 | 0 / 13 |

### What the region rule changed

- **xberg's pptx charts gain.** xberg writes a slide chart's data as one comma-separated line under
  its title (`Series 1, Category 1, ..., 4.3, 2.5, ...`), not as a table; the table-only rule
  found 2 of its 88 values, the region rule 22: all 12 of `realworld/pptx/chart-2d.pptx` and all 5
  of each `generated/pptx/slide-objects` file. markitdown's pptx charts are tables in their place,
  22 under both rules.
- **The xlsx credit goes.** Neither markitdown nor xberg renders an xlsx chart; the table-only rule
  credited them 25 of 37 because a chart's values are often the sheet's own cells, and the rule
  took any table. Under the region rule they find none: in `realworld/xlsx/with-two-charts.xlsx`
  and `realworld/xlsx/chart-sheet.xlsx` the chart is on a sheet of its own whose name xberg doesn't
  write (unplaced, 0) and where markitdown writes an empty table (0); in
  `generated/xlsx/drawings.libreoffice.xlsx` and `realworld/xlsx/simple-monthly-budget.xlsx` the
  chart follows the sheet's table and the tools write nothing there.
- docx charts: neither tool writes any of their data, 0 under both rules. The remaining pptx chart,
  `realworld/pptx/chartex-slide.pptx`'s chartEx box-and-whisker, neither tool writes.

### The rows a tool can't score

A zero, or a near zero, in these rows means the tool doesn't render the element in its Markdown,
not that it renders it wrong:

- **Headers and footers.** Neither markitdown nor xberg writes a docx file's headers or footers.
  The one text each is credited with is `2`, a header's page number in
  `realworld/docx/word-with-attachments.docx`, which the containment rule (below) finds inside
  other text: their true count is 0.
- **Comments.** markitdown writes none. xberg writes each comment as a footnote definition
  (`[^cmt0]:`), but marks its anchor with plain text (`[cmt:0]`) rather than a footnote
  reference, so the definitions are unreferenced, and Markdig drops a footnote definition
  nothing references. The texts are in xberg's output; a Markdown reader
  never shows them.
- **Slide notes.** xberg writes no notes.
- **Chart values in docx and xlsx files.** Neither writes a docx chart's data or an xlsx chart's
  at all; under the table-only rule, the sheet's own cells stood in for an xlsx chart's (above).
- **Media in xlsx files, and in xberg's pptx output.** Neither renders an xlsx file's pictures.
  xberg writes a slide's picture as escaped text, `\![alt](../media/image1.png)`, which Markdown
  reads as text, not an image.

## How the fidelity is scored

The bench is `bench/SpikeAiPipeline.Office.Baseline`; its rules are in `Fidelity.cs`, the truth's
reading in `Expected.cs`, and the Markdown's in `Rendered.cs`. The same rules apply to every tool.

**Parsing.** Markdig parses each Markdown with CommonMark and the GitHub extensions that change
what text reads as: pipe tables, footnotes, and strikethrough. A text is its inlines' plain text:
Markdown's escaping undone, emphasis and code keeping their text, a line break or a `<br>` a space,
an entity its character, a link its text. Markdown's reference marks are left out of the text: a
footnote reference (`[^1]`) and a link into the document itself (`[[1]](#footnote-1)`,
`[↑](#footnote-ref-1)`, markitdown's). Anything else a tool writes into the text stays there.

**Normalizing.** Every text, the truth's and the Markdown's, is composed to Unicode NFC, has each
run of whitespace (a line break, a tab, a non-breaking space) made one space, and is trimmed.
Table cells and chart values also compare by key: a number by its value (14, 14.0, and 1.4E1 are
one key), a boolean by its value whatever its case (TRUE, True), and an ISO 8601 date or date and
time by its value, whether T or a space joins the time, a midnight time left out.

**Matching.** Each truth element, in document order, takes the first Markdown element it matches
that no earlier truth element took.

| Kind | Expected (from the truth) | Matches when |
|---|---|---|
| Heading | each heading of the body, a slide's included | a heading of the same text and level, a level past Markdown's six as level 6; in a pptx file the level is not compared, as each tool sets a slide's title at a level of its own |
| Paragraph | each paragraph of the body | a paragraph of the same text outside a table, a list item's later paragraphs included and its first, which is the item's text, not |
| List item | each list item of the body | an item of the same text, nesting level, and kind (ordered or not), first in its list exactly when the truth's starts its list, and, for an ordered list's first item, with its list's first number (the truth's `startAt`, else 1) |
| Table cell | each cell of a table with text: a document cell's elements joined, a sheet cell's value (with no value, its formula after `=`) | the cell's key at its row and column of the Markdown table the truth table maps to. Truth tables map in order, each to the unmapped Markdown table where most of its cells match. Rows and columns are aligned first, so a header row a tool adds, a row-number column, or an empty column shifts nothing: the truth's rows are paired in order with the Markdown rows that share the most cell keys with them (a weighted longest common subsequence), and the columns the same way |
| Sheet name | each sheet | a heading of that text, or of that text and a parenthesized note after it, such as `(hidden)` |
| Slide order | each slide's first heading, paragraph, or list item, in presentation order; a slide with no text is left out | a text of the Markdown equal to it after the one the last counted slide matched, so the slides that count are in order |
| Slide notes | each text of a slide's notes | it is within some text of the Markdown |
| Chart value, region rule | each value a chart's series hold | a number token equal to it in the chart's region of the Markdown, each token standing for one value at most (below) |
| Chart value, table-only rule | the same | the chart's Markdown table holds it: each chart, in order, takes the unmapped table holding most of its values as numbers, each cell standing for one value at most; the table can be anywhere in the Markdown |
| Media reference | each picture, embedded or linked, wherever it is | the Markdown has an image (an image link or an HTML `img` tag), one for each picture, by count; the target isn't compared, as each tool names a picture its own way |
| Header or footer | each text of the distinct header and footer stories (one sections share is one story) | it is within some text of the Markdown |
| Footnote or endnote | each text of each note | it is within some text of the Markdown |
| Comment | each text of each comment and reply | it is within some text of the Markdown |

"Within some text" lets a tool's own label stand before a story's text (office's `Comment by ...:`,
markitdown's note list) or join a note's paragraphs into one, as xberg does. It can also credit a
short text found inside another, as the header `2` above was.

**The chart-value region rule** (`Fidelity.ChartValues`, `Text.NumberTokens`). A value counts
wherever the tool writes it near the chart, in a table or in running text, and nowhere else:

1. *Texts and anchors.* The Markdown's texts are read in order: headings, paragraphs, list items'
   texts, table cells, and code blocks. The truth's are read in its order too, each heading,
   paragraph, list item, table cell, sheet name, and chart title; an anchor is one of those that
   isn't a number, so a sheet cell's `1` never places a chart.
2. *Start.* A chart's region starts after the first text, from the last placed chart's start on,
   that holds the chart's title (a tool may write `Chart: ` before it). An untitled chart, or one
   whose title the Markdown lacks, starts after the first text equal to the nearest anchor before
   the chart in the truth; when that text is a table cell, after that table's last cell, as the
   chart follows the table. A chart with no anchor before it starts at the Markdown's beginning.
   If the Markdown has neither the title nor that anchor, the chart is unplaced and none of its
   values count, rather than a farther anchor stretching the region over whatever came before.
3. *End.* The region ends before the first text after its start equal to the first anchor after
   the chart that the Markdown has there (a later chart's title, a text holding it), or before the
   first cell of the table that text is in; with none, at the Markdown's end. The region's own
   texts, title excluded, are the ones scored.
4. *Labels.* The chart's series names and category labels that aren't numbers (`Series 1`,
   `Category 2`) are blanked out of the region first, so the `1` of `Series 1` is no value.
5. *Tokens.* A number token is a whole number as written: an optional sign, digits grouped in
   threes by commas or not, an optional fraction, an optional exponent. It is never part of a word
   or of another number: nothing of `Q1`, `x2`, `v1.2.3`, `2021-10-31`, `10/31`, `16:51`, or `1-5`
   is a token. A token's value is its digits with the commas dropped, read as a double, and
   compared by its shortest round-trip form, as the truth's value is: `1,234.50`, `1234.5`, and
   `1.2345E3` are one value, `2.0` and `2` one, and `4.4000000000000004` (xberg's) is `4.4`. Every
   tool's text is read the same way.
6. *Distinct occurrences.* Each of the chart's values, in series order, takes the first token of
   its value in the region that no value took before; a token one chart took, no later chart can
   (consecutive untitled charts share a region).

What the rule can still credit wrongly: a number a tool writes for another reason inside a
chart's region, a numeric category (a year) as a value, and a tool's comma-separated list written
without spaces (`1,234`) as one grouped number.

## Memory and time

Each tool ran 5 times on each file, the runs of one file and tool back to back, files in pool
order and tools office, markitdown, xberg for each. Each run was started through
`bench/rusage.c`, a small wrapper that forks the tool and reports the tool's own `wait4` rusage:
its peak resident memory (`ru_maxrss`) and its wall time from fork to exit. The wrapper exists
because a child carries its parent's resident memory across the fork and exec that start it, so a
tool started from the .NET bench would never report less than the bench's own peak. The wrapper's
floor, `/bin/true` run through it, was 1,528 KiB and 0.69 ms. The timeout was 60 s a run.

| Tool | Median file, median of 5 | Fastest and slowest file | Whole pool | Peak memory, median file | Least and most |
|---|---|---|---|---|---|
| office | 17.19 ms | 12.11 to 30.21 ms | 1,347 ms | 35.35 MiB | 30.52 to 44.36 MiB (`generated/docx/sections.libreoffice.docx`) |
| markitdown | 675.06 ms | 585.17 to 937.25 ms | 51,960 ms | 153.42 MiB | 151.70 to 169.60 MiB (`generated/docx/lists.python-docx.docx`) |
| xberg | 12.34 ms | 9.17 to 19.58 ms | 891 ms | 38.58 MiB | 36.85 to 40.95 MiB (`realworld/docx/word-with-attachments.docx`) |

"Median file" is the median over the 72 files of each file's median run; "whole pool" the sum of
the files' medians, one pass over the pool. Peak memory is a file's largest `ru_maxrss` of its 5
runs. Every number includes the process's start and exit, which for files this small is most of
it: markitdown's start (the interpreter, its imports, and magika's model) dominates each of its
runs.

### Cold start

| Tool | First run of the whole baseline | First run of a file, median file | First run less the other four's median, median file | Spread of 5 runs (max less min), median file |
|---|---|---|---|---|
| office | 25.56 ms | 17.29 ms | 0.18 ms | 3.23 ms |
| markitdown | 915.35 ms | 668.97 ms | 8.08 ms | 85.88 ms |
| xberg | 17.65 ms | 13.48 ms | 1.29 ms | 3.70 ms |

"Cold" here is weak, and the table should be read so. The first run of the whole baseline is each
tool's first process in this run, on the pool's first file; every tool had run the whole pool in
the earlier run that day and been run again by hand before this one, and the run dropped no page
cache (that needs root), so
each tool's files were very likely already in memory. The first run of a file is that tool's first
of its 5 on that file, with the file itself already read by the tool before it. A first run differs
from the rest by less than the runs' own spread, except each tool's first process of all, which
took 1.5 times (office), 1.4 times (markitdown), and 1.4 times (xberg) its median file's time.

## Footprint

| Tool | What | Bytes | MiB |
|---|---|---|---|
| office | `spike-ai-pipeline-office`, linux-x64, NativeAOT, as published | 30,197,712 | 28.80 |
| office | its debug symbols, `spike-ai-pipeline-office.dbg`, published beside it, not in the release archive | 84,137,176 | 80.24 |
| markitdown | its virtual environment, every package its install put there | 262,258,350 | 250.11 |
| markitdown | the interpreter it links to, `/usr/bin/python3.14` and libpython | 6,358,880 | 6.06 |
| markitdown | that interpreter's standard library, `/usr/lib/python3.14`, its site-packages left out | 68,988,132 | 65.79 |
| xberg | `xberg`, the binary | 135,432,936 | 129.16 |
| xberg | its install directory: the binary, the shared libraries beside it (libx265, libaom, libheif, libde265, libnuma), and its license files | 159,701,853 | 152.30 |

office's linux-arm64 binary wasn't measured: a NativeAOT publish for linux-arm64 on this x64
laptop fails at the link (the system linker has no aarch64 target), so its size is the
`spike-ai-pipeline-office-vX.Y.Z-linux-arm64.tar.gz` release asset's, which the CI release
workflow builds natively, and this page records it once the release exists.

## Licenses

**office**: Apache-2.0 (`LICENSE`). Every NuGet package its publish restores is MIT, by its
nuspec: DocumentFormat.OpenXml 3.5.1 and DocumentFormat.OpenXml.Framework 3.5.1, System.CommandLine
2.0.14, System.IO.Packaging 10.0.2, and the NativeAOT compiler, Microsoft.DotNet.ILCompiler
10.0.13 (with its linux-x64 runtime package), whose runtime is compiled into the binary, and
Microsoft.NET.ILLink.Tasks 10.0.13.

**markitdown**: MIT. Its environment's packages, each as its metadata states its license:

| Package | Version | License |
|---|---|---|
| beautifulsoup4 | 4.15.0 | MIT |
| certifi | 2026.10.23 | MPL-2.0 |
| charset-normalizer | 3.5.2 | MIT |
| click | 8.5.0 | BSD-3-Clause |
| cobble | 0.1.4 | BSD |
| defusedxml | 0.7.1 | PSFL |
| et_xmlfile | 2.0.0 | MIT |
| flatbuffers | 25.12.19 | Apache-2.0 |
| idna | 3.20 | BSD-3-Clause |
| lxml | 6.1.3 | BSD-3-Clause |
| magika | 1.0.3 | Apache-2.0 |
| mammoth | 1.13.0 | BSD-2-Clause |
| markdownify | 1.2.3 | MIT |
| numpy | 2.5.4 | BSD-3-Clause AND 0BSD AND MIT AND Zlib AND CC0-1.0 |
| onnxruntime | 1.31.1 | MIT |
| openpyxl | 3.1.5 | MIT |
| packaging | 26.3 | Apache-2.0 OR BSD-2-Clause |
| pandas | 3.1.0 | BSD-3-Clause |
| pillow | 13.0.0 | MIT-CMU |
| protobuf | 7.36.2 | BSD-3-Clause |
| python-dateutil | 3.0.0 | "Dual License" (its metadata's words) |
| python-pptx | 1.0.2 | MIT |
| requests | 2.34.2 | Apache-2.0 |
| six | 1.17.0 | MIT |
| soupsieve | 3.0.4 | MIT |
| typing_extensions | 4.16.0 | PSF-2.0 |
| tzdata | 2026.6 | Apache-2.0 |
| urllib3 | 2.8.0 | MIT |
| xlsxwriter | 3.3.1 | BSD-2-Clause |

**xberg**: MIT (its `LICENSE`). Its `THIRD_PARTY_LICENSES.md`, installed beside it, lists the
shared libraries it ships beside the binary and links dynamically: libheif (LGPL), libde265
(LGPL-3.0-or-later), libaom (BSD-2-Clause and a patent grant), and libx265 (GPL-2.0-or-later,
for HEIF output only); and libwpd and librevenge (MPL-2.0), linked statically. libnuma, also in
the install directory, isn't listed there.

## What the run turned up

- **office**: no fault. Its Markdown matched every element of every kind in all 72 files, and it
  exited 0 on each; each tool's output was the same on all 5 runs of every file.
- **markitdown failed on `realworld/xlsx/simple-strict.xlsx`**, an ISO 29500 Strict workbook,
  exit code 1: `XlsxConverter threw ValueError with message: Sheet name is an empty list`. It is
  scored on what it wrote, nothing.
- **markitdown runs a model on every file, and ran as shipped.** Its `MarkItDown` class always
  loads magika (an ONNX file-type classifier, onnxruntime 1.31.1) inside markitdown's own process
  and identifies every input with it, local inference no option of its CLI turns off; the bench
  didn't patch it out, so markitdown's times and memory include it. Its runtime and model account
  for much of markitdown's start; the run didn't separate them. office never loads markitdown,
  xberg, magika, or onnxruntime, and office's rule of no inference is untouched.
- **markitdown's slides lose their lists and paragraphs**: it writes a text box's paragraphs and
  bullets as lines one after another, which Markdown reads as one paragraph, so no list item
  matched in a pptx file; and it orders a slide's shapes by position, not by the shape tree, so
  `groups`' "After the group, placed at the top." came first.
- **xberg wraps note references in `^...^`**, a superscript GFM doesn't have, so each paragraph
  with a note reference keeps two carets in its text, and fails to match.
- **Both merge back-to-back lists.** An ordered list that restarts its count right after another
  (`lists`' "Again one") is one list in both: markitdown numbers on (`3. Again one`), and xberg
  writes `1.` after a blank line with the same delimiter, which Markdown reads as the same list.
- **Neither writes a cell's error as Excel shows it**: markitdown writes `NaN` for `#N/A` and
  `#DIV/0!`, xberg `#ERR: NA` and `#ERR: Div0`. Both drop a 1904 workbook's first-day date,
  showing `12:00:00` for 1904-01-01T12:00:00.

## Rerunning

From this repository's root, on a laptop (never in CI):

```sh
mise run baseline
```

It installs `mise.baseline.toml`'s tools (markitdown and xberg, loaded only with
`MISE_ENV=baseline`, so neither the check nor CI installs them), publishes office for this
machine into `.state/baseline/office/`, builds the bench and the wrapper (`cc`) into
`.state/baseline/`, runs every pool file, and writes a new
`docs/findings/office-cli/runs/<date>-<n>/results.json`, about 4.5 minutes on this laptop (this
run's tools took 4 minutes 32 seconds).
`BASELINE_RUNS` (5) sets the runs per file and tool, `BASELINE_TIMEOUT` (60) the seconds a run may
take, and `BASELINE_OUT` another runs directory. To see the texts the scoring reads in one
Markdown file: `.state/baseline/bench/SpikeAiPipeline.Office.Baseline --dump <file.md>`; to score
one against a truth, each kind's found and expected and its misses:
`.state/baseline/bench/SpikeAiPipeline.Office.Baseline --score <truth.json> <docx|xlsx|pptx> <file.md>`.
`mise run currency` reports markitdown and xberg when a newer release exists.

In `results.json` (schema 2): `fidelity` holds, for each subset (`pooled`, `generated`,
`realworld`), each kind and tool's files, expected, found, and score, `chartValue` the region rule
and `chartValueTable` the table-only rule;
`performance` each tool's summary above; `footprint` the sizes and licenses; `wrapperFloor` the
wrapper's own; and `files` each file's status, exit code, stderr when it failed, Markdown size,
whether its 5 outputs were the same, its runs (wall, user, and system time, peak memory), and each
kind's expected, found, and first ten misses.
