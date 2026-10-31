# Real-world sources

Each file here is copied byte for byte from a permissively licensed repository's test data,
pinned to one commit, and renamed; its SHA-256 below is checked against the blob at that commit.
Every one was written by Word, Excel, or PowerPoint, going by its `docProps/app.xml`
`Application`, and none of its parts carries the byte-order mark the Open XML SDK writes when it
re-serializes one. One was then changed outside Word: `docx/word-tables.docx` is Word's file
with its last two tables added to its XML (they carry none of the `w:rsid` attributes Word gives
every other table, row, and paragraph) and re-zipped by a tool (every entry dated 2026-07-10
08:13, deflated alike). Each source repository's license, and its NOTICE
where it has one, is in `licenses/`.

| Repository | Commit | License | Notices |
| --- | --- | --- | --- |
| [dotnet/Open-XML-SDK](https://github.com/dotnet/Open-XML-SDK) | `431ab05cf160248cc3885a4a766026d4f8243792` | MIT, © .NET Foundation and Contributors | `licenses/Open-XML-SDK.LICENSE`, `licenses/Open-XML-SDK.NOTICE` |
| [apache/poi](https://github.com/apache/poi) | `6327f056ba3cfdae326a0261a236460592b80f5c` | Apache-2.0, © The Apache Software Foundation | `licenses/poi.LICENSE`, `licenses/poi.NOTICE` (its `legal/`) |
| [docling-project/docling](https://github.com/docling-project/docling) | `7b12671e1d324710e70784c18844d1d97522414f` | MIT, © The Docling Contributors | `licenses/docling.LICENSE` |

No poi file here is listed in its `test-data/poi-integration-exceptions.csv`.

## Files

| File | Repository | Path at the commit | Writer (`app.xml`) | Bytes | SHA-256 |
| --- | --- | --- | --- | --- | --- |
| `docx/chart-column.docx` | Open-XML-SDK | `test/DocumentFormat.OpenXml.Tests.Assets/assets/TestDataStorage/v2FxTestFiles/wordprocessing/chart/chart-Column.docx` | Microsoft Office Word 12.0000 | 18619 | `9402cb32b06d08f5a41b60ed2dcbe9dcc279a85a61059869fafa6d4d9540ff15` |
| `docx/complex-numbered-lists.docx` | poi | `test-data/document/ComplexNumberedLists.docx` | Microsoft Office Word 14.0000 | 14458 | `297a085a7d433af2eeee7661e8db21539452cb585096484774a1e9f5f258b0b6` |
| `docx/content-control-rows.docx` | poi | `test-data/document/tika-3816.docx` | Microsoft Office Word 16.0000 | 22374 | `ab5b32d15d2fd14c633452cc5a8b1cd27cd65038cd208a11ede29d6f8fe24259` |
| `docx/delins.docx` | poi | `test-data/document/delins.docx` | Microsoft Office Word 12.0000 | 17720 | `7ecad102602586be7f1371f117fc567ea47f3ed0ca703b353e003002f1ff8d3f` |
| `docx/endnotes.docx` | poi | `test-data/document/endnotes.docx` | Microsoft Office Word 12.0000 | 13553 | `6e2e1b2dabb0ae6b9b26ddd613dca446ba0710ecc8b4008722abc4ed592934e3` |
| `docx/footnote.docx` | Open-XML-SDK | `test/DocumentFormat.OpenXml.Tests.Assets/assets/TestDataStorage/v2FxTestFiles/wordprocessing/footnote & endnote/footnote.docx` | Microsoft Office Word 12.0000 | 13768 | `ae76e87fbdc0e9987b1c62a21b93a49dc7d8687b068aab07008cbebaaa009c3f` |
| `docx/form-footnotes.docx` | poi | `test-data/document/form_footnotes.docx` | Microsoft Office Word 12.0000 | 27824 | `a890294bfbdc63f6d03eab61efc1e98cb51d59f0920541150dd7082f9bdf31f1` |
| `docx/ruby.docx` | poi | `test-data/document/61470.docx` | Microsoft Office Word 16.0000 | 12523 | `abda850b3685027eef32a4c583b2b5f7f20f243026d8c453cdb3315348ed6ff0` |
| `docx/test-comment.docx` | poi | `test-data/document/testComment.docx` | Microsoft Office Word 16.0000 | 65298 | `06b63b13f0b78a7179151af52c2282557c1ed204899feee9fcef4ca575344f97` |
| `docx/unit-test-formatting.docx` | docling | `tests/data/docx/sources/unit_test_formatting.docx` | Microsoft Office Word 16.0000 | 29578 | `23518f8c635264b038b3e1207292f99cb5d40be35dbcd85d56508d3226ad9a11` |
| `docx/word-tables.docx` | docling | `tests/data/docx/sources/word_tables.docx` | Microsoft Office Word 16.0000, then edited and re-zipped (above) | 14247 | `0d69a7ef0e1ba2b3142394ed708227b95cf37de037ee71f434853d30e3ca5a55` |
| `docx/word-with-attachments.docx` | poi | `test-data/document/WordWithAttachments.docx` | Microsoft Office Word 12.0000 | 144959 | `90352263823e6d3d39a4ec88212134b74bcb0f86b603bc3b51d712a3ec3df9f2` |
| `xlsx/chart-sheet.xlsx` | poi | `test-data/spreadsheet/chart_sheet.xlsx` | Microsoft Excel 12.0000 | 10795 | `93e2d8663bc1a6b7d961f2a74440953ecdaadb1a3f171960129b730f7c935d3e` |
| `xlsx/date-format-tests.xlsx` | poi | `test-data/spreadsheet/DateFormatTests.xlsx` | Microsoft Macintosh Excel 16.0300 | 12394 | `16445dc0deba00671ee4315015a970d415ce9886da59c974532d946e99276c66` |
| `xlsx/excel-online-index-space.xlsx` | poi | `test-data/spreadsheet/bug69769.xlsx` | Microsoft Excel Online 16.0300 | 13976 | `098d7379336832d5c286bf93163c760d6595197b970b7a5c1156209169c0480c` |
| `xlsx/simple-monthly-budget.xlsx` | poi | `test-data/spreadsheet/simple-monthly-budget.xlsx` | Microsoft Excel 16.0300 | 18700 | `cae00c6894b95743017aeded4186f15a12c2d8382d71760029df490d58197db6` |
| `xlsx/with-two-charts.xlsx` | poi | `test-data/spreadsheet/WithTwoCharts.xlsx` | Microsoft Excel 12.0000 | 12810 | `7d29c92e9f7968a5bc9ec076458785295c24d13916b02b8a467c0a8fd013cddd` |
| `pptx/chart-2d.pptx` | Open-XML-SDK | `test/DocumentFormat.OpenXml.Tests.Assets/assets/TestDataStorage/v2FxTestFiles/presentation/Chart_2D.pptx` | Microsoft Office PowerPoint 12.0000 | 62118 | `dad57bde3989dbc48e5c52988773ccf74d1cf56af875e5366e4c92992ce258f3` |
| `pptx/layouts.pptx` | poi | `test-data/slideshow/layouts.pptx` | Microsoft Office PowerPoint 14.0000 | 62901 | `9c3d53afa3115de2ba72ec77637a616aeed505a6ff3b30a46db871c4edd32be5` |
| `pptx/powerpoint-sample.pptx` | docling | `tests/data/pptx/sources/powerpoint_sample.pptx` | Microsoft Macintosh PowerPoint 16.0000 | 45849 | `24191d47fda511e11984e875f8676506da5ae1fbcaefcc63d81be377d3a3950c` |
| `pptx/shapes.pptx` | poi | `test-data/slideshow/shapes.pptx` | Microsoft Office PowerPoint 14.0000 | 68822 | `19fde9b87e33dd1a95fdbba0cf6abc2278bf03874f4665c7f8b88b6afe4a2571` |

## What each covers, and what its truth covers

Each file's ground truth is `<name>.truth.json` beside it, in the generated set's format
(`../README.md`). It was written from the file's own XML, unzipped and read part by part, never
from office's output; an xlsx truth's cells were read from the sheet XML by a throwaway script
that knows nothing of office, with which styles are dates decided by hand from each number
format, and the truths of `docx/form-footnotes.docx`, `docx/word-tables.docx`, and
`docx/word-with-attachments.docx` were drafted the same way, by a throwaway script reading
WordprocessingML as the specification describes it, then checked by hand against the XML (all of
the second and third; the first at its smart tags, form fields, picture cell, footnote, and
headings). Every truth is whole: it holds every element of each kind the truth format scores, so
the tests compare all of the model, not a subset.

| File | Covers | Truth covers |
| --- | --- | --- |
| `docx/chart-column.docx` | Word's own chart: a 3-D clustered column chart (`bar3DChart`), titled, three series of cached data over three categories, beside its embedded workbook; a paragraph's text split across runs | all: the paragraph and the chart |
| `docx/complex-numbered-lists.docx` | numbered lists on three abstract definitions, one with a nested lower-letter level; a definition whose level starts at 10, its list interrupted by a plain paragraph and resumed, counting on at 12 (the regression file for list starts and continuation) | all: 17 elements, the starts as `startAt` 10 and 12 |
| `docx/content-control-rows.docx` | a table row inside three nested content controls (`w:sdt` children of `w:tbl`), its first cell inside a drop-down content control showing its placeholder, "Choose an item."; a glossary part (the regression file for rows and cells in content controls, which were skipped with their text) | all: the paragraph and the table of five rows; the placeholder text in `absent` |
| `docx/delins.docx` | Word's real tracked changes: insertions in runs and of whole paragraphs, two list items deleted with their paragraph marks running on into an inserted paragraph, deleted fields, an inserted empty list item; hyperlink fields; bullet lists of three numbering instances on three abstract definitions | all: 22 elements; the deleted text and field instructions in `absent` |
| `docx/endnotes.docx` | an endnote reference, Word's separator and continuation-separator endnotes, a footnotes part of separators only; paragraphs of text with leading spaces | all: 10 paragraphs, the one endnote |
| `docx/footnote.docx` | a footnote reference, Word's separator and continuation-separator notes, an endnotes part of separators only; text with no-break spaces | all: 4 paragraphs, the one footnote, no endnotes |
| `docx/form-footnotes.docx` | a visa application form: six tables of merged cells (spans up to 15 columns), legacy form fields (`FORMTEXT` results of en spaces, which are blank, and `FORMCHECKBOX`), smart tags (`w:smartTag`, nested two deep) around runs (the regression file for smart tags, whose text was dropped), a picture and a footnote reference in a cell, numbered paragraphs and headings in cells, `w:sym` symbols, Heading 4 and Heading 7 paragraphs, a footer of one empty paragraph | all: six tables, two headings, a paragraph, and the footnote. The truth format scores a cell's paragraphs, so the list items and headings inside cells are outside it, and `w:sym` symbols, which office doesn't read, are left out |
| `docx/ruby.docx` | a ruby (`w:ruby`): 東京 with its reading, とうきょう, as the annotation (the regression file for ruby, which was dropped whole) | all: the paragraph of the base text; the annotation in `absent` |
| `docx/test-comment.docx` | a comment Word wrote with `commentsExtended`, `commentsIds`, and `commentsExtensible`, anchored mid-paragraph, holding a picture | all: the paragraph with its mark, the comment with its anchor and picture |
| `docx/unit-test-formatting.docx` | three sections: the first referencing default, first-page, and even-page headers and footers with no title page and even-page headers off, the second inheriting, the third with a title page; a first-page footer of three paragraphs and a picture in an `INCLUDEPICTURE` field; hyperlinks; a nested bullet list | all: 15 elements and every section's shown headers and footers |
| `docx/word-tables.docx` | seven tables: uniform; spans across outer and inner columns; vertical spans; every kind of span with empty cells, a span of three rows and one of a whole row; a row starting late (`w:gridBefore`, the regression file for late rows, whose cells were shifted left); a vertical span over a row starting late | all: 15 elements, the late row's cell with `skipBefore` 1 |
| `docx/word-with-attachments.docx` | three comments in Russian, Uzbek, and English across paragraphs, one whose range ends between paragraphs (a block-level `w:commentRangeEnd`) with its reference in the paragraph after (the regression file for block-level ranges, whose anchor was lost); OLE objects (a package, two Word 97 documents, two Excel workbooks) shown as WMF and EMF previews; a VML text box and WordArt; hyperlinks; a table; even-page and default headers with `PAGE` fields, even-page headers off | all: 23 elements, the five OLE previews among them as media, the default header, and the three comments with their anchors. The text box's and WordArt's text, which office doesn't read, are left out |
| `xlsx/chart-sheet.xlsx` | a chartsheet between worksheets, its bar chart's series with no categories; empty worksheets | all: four sheets, 10 cells, the chart |
| `xlsx/date-format-tests.xlsx` | the 1904 date system; datetimes with milliseconds in a custom format with escaped characters; shared formulas whose ranges are reused and one of a single cell; formula string results (`t="str"`), with Excel's own `TEXT` of each date beside it confirming the dates | all: both sheets, 194 cells |
| `xlsx/excel-online-index-space.xlsx` | a shared-string index Excel Online stores with a trailing space (`<v>2 </v>`, poi's bug 69769) | all: four cells |
| `xlsx/simple-monthly-budget.xlsx` | cached results of formulas over defined names; currency and percent formats that aren't dates; merged ranges with and without a value in their anchor, two starting on one row, one starting the row after another ends; a bar chart with its title deleted; an Excel table | all: 49 cells and the chart |
| `xlsx/with-two-charts.xlsx` | shared formulas with cached values, one cell inside a shared range holding its own formula; a line chart and an area chart on otherwise empty sheets | all: three sheets, 24 cells, two charts |
| `pptx/chart-2d.pptx` | PowerPoint's chart in a graphic frame, cached values as PowerPoint rounds them (`2.3999999999999995`), with notes | all: the slide, its chart, and its notes |
| `pptx/layouts.pptx` | bullets inherited from a real master's body style through every built-in layout, nested nine levels; a title placeholder whose index its layout lacks; text boxes with their own bullets; a picture placeholder; date, footer, and slide-number placeholders with their text; an empty notes page | all: ten slides |
| `pptx/powerpoint-sample.pptx` | a table merged across columns (`gridSpan`, `hMerge`) and rows (`rowSpan`, `vMerge`) with empty cells; a paragraph whose own `buNone` overrides the master's bullets; text boxes of auto-numbered and character bullets; notes of one and two paragraphs | all: three slides and their notes |
| `pptx/shapes.pptx` | connectors, a freeform with text, a group of empty shapes, a picture, tables with merged and empty cells and hyperlinked text, a title last in its shape tree | all: six slides |
