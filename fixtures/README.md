# Fixtures

The documents office's tests read, committed and never generated in the check.

## Generated set

`generate.py` writes each fixture of `generated/` from the definitions in the script itself, then
re-saves it through LibreOffice headless:

- `<name>.python-docx.docx`: written by python-docx, with what python-docx can't write added as
  raw XML or raw package parts: the tracked changes (`w:ins`, `w:del`, `w:moveFrom`, `w:moveTo`,
  a deleted paragraph mark), the drawings of pictures and charts, the chart parts (cached series
  data, no embedded workbook), a picture part of its own and a linked picture's external
  relationship, the footnotes and endnotes parts (with Word's separator and continuation notes),
  and the comments and commentsExtended parts.
- `<name>.libreoffice.docx`: that file opened and saved again by LibreOffice, so the set also
  carries a real application's XML. Both are kept: the first is the XML the script means to
  write, the second what an editor makes of it, and office must read both the same way.
- `<name>.truth.json`: the ground truth both files share, written by the script from the same
  definitions: the accepted-state text of each element in document order (`heading` with its
  level, `paragraph`, `listItem` with its nesting level, whether its level is ordered, and
  whether it starts a list rather than continuing the one before it, `table` with each cell's
  text, its paragraphs joined by a newline, its spans, and its pictures and charts as `media`,
  covered cells left out, `media` with its file name (the SHA-256 of its bytes and its
  extension) and content type or its linked `target`, its `alt` and `title`, and its `section`,
  and `chart` with its type, title, and series), the strings that must appear nowhere in office's
  output (`absent`: deleted text), and the strings that must appear exactly once (`once`: moved
  text). A paragraph's or list item's note and comment references are its `marks`: each one's
  offset in the text, its kind, and its note's or comment's place, from 1, in the truth's list
  of them. Beside the elements, `sections` holds each section's headers and footers as its
  pages show them (kind and elements), `footnotes` and `endnotes` the notes in the order the text
  first references them, and `comments` each thread's author, anchored text, resolved state,
  elements, and replies. Its shape is its own, not office's model, so the tests compare the model
  against it element by element.
- `<name>.libreoffice.truth.json`: the re-save's own ground truth, where LibreOffice changes what
  the document holds; the script writes it from the same definitions, with the change it knows
  LibreOffice makes.

| Fixture | Covers |
| --- | --- |
| `headings` | heading levels 1 to 4 from heading styles, a level-2 heading from a paragraph's outline level alone, paragraphs, a line break, and text Markdown would misread |
| `lists` | bullet lists nested three deep, an ordered list with a nested bullet list, a second ordered list, ordered under bullet under ordered, two ordered lists back to back (the second restarting its count) followed directly by a bullet list |
| `tables` | a header row, a cell of two paragraphs, a horizontally merged cell, a vertically merged cell, a pipe in a cell |
| `tracked-changes` | an insertion and a deletion in one paragraph, a move, a deleted paragraph mark joining two paragraphs, a deleted paragraph, an inserted paragraph, tracked changes in list items and in a table cell, a deleted paragraph mark right before a table |
| `sections` | three sections with even-page headers on: the first with its own default, first-page, and even-page headers and footers and a title page; the second with its own default header (holding a picture) and a first-page header but no title page, inheriting the rest; the third with a title page, inheriting everything; a tracked change, a table, and a list item in headers and footers |
| `headers-even-off` | first-page and even-page headers written but not shown: no title page, no even-page headers in the settings |
| `notes` | footnotes and endnotes referenced out of their id order, a note of two paragraphs, a reference in a list item, a footnote whose only reference was deleted |
| `comments` | a thread of a comment and two replies, a resolved comment anchored across two paragraphs with two paragraphs of text, a comment anchored to tracked text, with commentsExtended |
| `comments-plain` | a comment python-docx writes, with no commentsExtended part: no replies, unresolved |
| `media` | a picture on its own line, one between text, the first picture's bytes again through the same part and through a part of its own, an EMF, a linked picture, a deleted picture, one in a table cell, one in a second section |
| `charts` | a clustered bar chart of two series with a missing value, an untitled pie chart, a line chart |

What LibreOffice's re-save changes, checked by unzipping it:

- It writes a picture's title and description as one description, the title's line first, and
  no title (`media`, `sections`: their LibreOffice truth says so).
- It gives a section the default header and footer as its first-page ones where it should
  inherit a first-page variant through a section without a title page (`sections`), and writes
  an inherited header's copies with their deletions as plain text, so the fixture keeps its
  tracked header change in a header no other section inherits.
- It pairs footnotes with their references by their order in the notes part, not by id, so its
  first reference shows the first-defined note (`notes`).
- It keeps one part for the bytes the python-docx file stores twice, so only the python-docx
  file has two parts of the same picture; the bytes themselves, the EMF among them, it keeps as
  they are.
- It drops the deleted paragraph mark before the table, writing that text as a plain paragraph:
  the truth is the same, but only the python-docx file has the case.
- It keeps the charts' cached data, the notes (renumbering them, the separators too), the
  comments' replies and resolved state (in its own commentsExtended), and the linked picture.

## Regenerating

From the repository root, with LibreOffice's `soffice` on the path and no LibreOffice window open
on the same profile (the script uses a throwaway one):

```sh
mise run fixtures
```

That runs `uv run --script fixtures/generate.py`; the script's inline metadata pins python-docx
and `generate.py.lock` pins the rest. It rewrites every file of `generated/`, or with fixture
names as arguments (`uv run --script fixtures/generate.py notes media`) only those fixtures'
files, and the section below, which lists every fixture's files as they are on disk. Both
writers stamp the time into the package, so a rerun changes a `.docx` file's bytes and checksum
even when nothing else changed; regenerate only the fixtures a change needs.

<!-- generated: begin -->
LibreOffice: `LibreOffice 26.8.1.1 680(Build:1)`

| File | SHA-256 |
| --- | --- |
| `generated/docx/headings.python-docx.docx` | `e7ab7a872d563c7ed0d0d51902738643e5ddb705c9dd364b8f5d0aa54f366095` |
| `generated/docx/headings.libreoffice.docx` | `63586ce4480810a81c05c9b0109062be4c76dfce9e62a39641d31ddf58f22aa4` |
| `generated/docx/headings.truth.json` | `41b138b81b26fda92e03039891c3c0cde4d6b7119ecd27055e7782c4ff312050` |
| `generated/docx/lists.python-docx.docx` | `5d61af165027d3cad297a0dbaf2cc8b51ec2274aee648dd2398d2fdc9367e99b` |
| `generated/docx/lists.libreoffice.docx` | `261277388b4c59a885f5060a9db58ba96c3c128ea528a6a26dd0747870725ad6` |
| `generated/docx/lists.truth.json` | `9906d980e45f375953b730d772a325d3e3ec0b3ac7e2643915c6d795f19ead4c` |
| `generated/docx/tables.python-docx.docx` | `4bcc3b658b559c2d5c58a8a518ad5d6578056412218569b000f26aec9e5860b1` |
| `generated/docx/tables.libreoffice.docx` | `01116cf56ffbb4ad1f360272e2fabd8cd6e60d25e8081202f4b2d0aad91b7d2c` |
| `generated/docx/tables.truth.json` | `81d60edfcb56b9764ce093db5bce5ff148c61374e9b4cd674e33ae0d104feac6` |
| `generated/docx/tracked-changes.python-docx.docx` | `d0e21769efbb2f8a83db3171e0443e8550023b415464459b9e0ab58672f63084` |
| `generated/docx/tracked-changes.libreoffice.docx` | `9378482803e88c832f61848c56f53b935ef336f3c443977c78800ddfb3bb73a3` |
| `generated/docx/tracked-changes.truth.json` | `6ca7dfda1125c6339783f162cae12eac42e0975d0fca841cf1cd71dee66b4353` |
| `generated/docx/sections.python-docx.docx` | `ce66fb89cc609a7be34e066d038ded68a2c366242963c6f3d1e49f110b31f4aa` |
| `generated/docx/sections.libreoffice.docx` | `dbe71997cd80eec4639a4974a9694d4c658f2f11d5165f93ca9ce4c6b909df5b` |
| `generated/docx/sections.truth.json` | `b1b87eca268df2c2b2017e47e8ab7421b280c8bcd809a84de06c82bf6b42a09e` |
| `generated/docx/sections.libreoffice.truth.json` | `1e1835e27cc0e27640ef58fc004464717b54f358c18b8e9bd0f2d7e4082b9407` |
| `generated/docx/headers-even-off.python-docx.docx` | `b1fb197b0b18968bec8b386c6c6c29b29446d7ed952e36a21314c6947ab3fb0c` |
| `generated/docx/headers-even-off.libreoffice.docx` | `2650103f4ce06530fd1b0e305d3c1ba1a7071633a3019ba0d442a959db5a2c3c` |
| `generated/docx/headers-even-off.truth.json` | `89f9f6313cbbec642faab90969fca2f9e2a474f97453c00a518f4c9ac9215712` |
| `generated/docx/notes.python-docx.docx` | `12482a309c45f1e871728f6fe9db5744a61dfca146f7ee60ebf0a263a12b397b` |
| `generated/docx/notes.libreoffice.docx` | `f9cafb26909d822de42cd4357b6b207719429fbafa301c405dc695f3457f61c6` |
| `generated/docx/notes.truth.json` | `f87b3fa9a7176774fb87d25c6ce30d6b72b36cdb81fe35af1989f053be9f667c` |
| `generated/docx/notes.libreoffice.truth.json` | `ebdee61e95c96e6f7b83d0d97ddc227a987d4b88acfa6db4ce24d11d675f5ee5` |
| `generated/docx/comments.python-docx.docx` | `afae352365b03bbdd0e90fbe0fb6804c1d2dde0d4d5de416f3fbdd3b33b87bcb` |
| `generated/docx/comments.libreoffice.docx` | `e683efab836f1e27e1abd65c58d34df37cdabb52d1214f2fabba28f5a0f5ef43` |
| `generated/docx/comments.truth.json` | `8aa467240b95417bc2beb097881d0d11266ffb88a5d8448d2de4f1a223d8efcb` |
| `generated/docx/comments-plain.python-docx.docx` | `f2fc1d900935d6882976a7e41506ee4abd339bd58c047902f96e6f8252fe0111` |
| `generated/docx/comments-plain.libreoffice.docx` | `c1d1f034522856494c7f9ee99cb3cdfe65011232a8fccde3a276db3b85836ee4` |
| `generated/docx/comments-plain.truth.json` | `b93eaf88d0ee175238b88aa1a75afc5df74f04d5784dcd0b6f313ba869a7f9cb` |
| `generated/docx/media.python-docx.docx` | `6cee5bc1fa49db7dbcf277282e3748c55f2f76828bcd846afb4c25eb0765d552` |
| `generated/docx/media.libreoffice.docx` | `07f3a8c5d58d9345fcbbf42b725ee32c92ed722bc9de31d5fad5f84169159af7` |
| `generated/docx/media.truth.json` | `cf7b087fecd6278fcce545d4503dbe80c967e2385b4359b3de22d5d23e2d292c` |
| `generated/docx/media.libreoffice.truth.json` | `d45096bc7b166f7ea84fd852176a0263cae77a63cf9eda35d5614ebd18f49b69` |
| `generated/docx/charts.python-docx.docx` | `adda58a2b466af5c31018928b3e89517575244296f16df9b201d91b549312118` |
| `generated/docx/charts.libreoffice.docx` | `c49582d00fc63052e4c2a0bb04a64d8fd45933c39539a82434f9bbc7e24c0f0d` |
| `generated/docx/charts.truth.json` | `8c3df2d8aa2f02bfde11bdbbbf82a9642662d025119aaf40a45f282048af3ca1` |
<!-- generated: end -->
