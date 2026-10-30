# Fixtures

The documents office's tests read, committed and never generated in the check.

## Generated set

`generate.py` writes each fixture of `generated/` from the definitions in the script itself, then
re-saves it through LibreOffice headless:

- `<name>.python-docx.docx`: written by python-docx, with the tracked changes python-docx can't
  write (`w:ins`, `w:del`, `w:moveFrom`, `w:moveTo`, a deleted paragraph mark) added as raw XML.
- `<name>.libreoffice.docx`: that file opened and saved again by LibreOffice, so the set also
  carries a real application's XML. Both are kept: the first is the XML the script means to
  write, the second what an editor makes of it, and office must read both the same way.
- `<name>.truth.json`: the ground truth both files share, written by the script from the same
  definitions: the accepted-state text of each element in document order (`heading` with its
  level, `paragraph`, `listItem` with its nesting level, whether its level is ordered, and
  whether it starts a list rather than continuing the one before it, and `table` with each
  cell's text, its paragraphs joined by a newline, and its spans, covered cells left out), the
  strings that must appear nowhere in office's output (`absent`: deleted text), and the strings
  that must appear exactly once (`once`: moved text). Its shape is its own, not office's model,
  so the tests compare the model against it element by element.

| Fixture | Covers |
| --- | --- |
| `headings` | heading levels 1 to 4 from heading styles, a level-2 heading from a paragraph's outline level alone, paragraphs, a line break, and text Markdown would misread |
| `lists` | bullet lists nested three deep, an ordered list with a nested bullet list, a second ordered list, ordered under bullet under ordered, two ordered lists back to back (the second restarting its count) followed directly by a bullet list |
| `tables` | a header row, a cell of two paragraphs, a horizontally merged cell, a vertically merged cell, a pipe in a cell |
| `tracked-changes` | an insertion and a deletion in one paragraph, a move, a deleted paragraph mark joining two paragraphs, a deleted paragraph, an inserted paragraph, tracked changes in list items and in a table cell |

## Regenerating

From the repository root, with LibreOffice's `soffice` on the path and no LibreOffice window open
on the same profile (the script uses a throwaway one):

```sh
mise run fixtures
```

That runs `uv run --script fixtures/generate.py`; the script's inline metadata pins python-docx
and `generate.py.lock` pins the rest. It rewrites every file of `generated/` and the section
below. Both writers stamp the time into the package, so a rerun changes every `.docx` file's
bytes and checksum even when nothing else changed; commit a regenerated set only with the change
that needed it.

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
| `generated/docx/tracked-changes.python-docx.docx` | `f87eee0aaf4aaa7b94c1d5f83fe6700de32a85f288cc787303f3f7499f258351` |
| `generated/docx/tracked-changes.libreoffice.docx` | `03118b6a831ce7a83b802e9e3998b9c93a7e8e0c66a74b79c805bc1264497b18` |
| `generated/docx/tracked-changes.truth.json` | `65998c12c054712c7de181ebda34d1ee6efbdb44d6931e64474af9331070bf4a` |
<!-- generated: end -->
