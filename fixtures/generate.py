# /// script
# requires-python = ">=3.12"
# dependencies = ["python-docx==1.2.0"]
# ///
"""Generate the office fixtures and their ground truth.

Each fixture is written twice: by python-docx from the definitions below
(<name>.python-docx.docx), and that file re-saved by LibreOffice headless
(<name>.libreoffice.docx), so the corpus carries a real application's XML as
well as a library's. Both files of a fixture share one ground truth,
<name>.truth.json, which this script writes from the same definitions. The
ground truth is the document's text in its accepted state, element by element,
in a shape of its own, so it never depends on the office model it checks.

python-docx writes no tracked changes, so the w:ins, w:del, w:moveFrom, and
w:moveTo elements and the deleted paragraph marks are written as raw XML.

Run it through `mise run fixtures`; the check never runs it. It rewrites the
generated set and the checksum section of fixtures/README.md.
"""

import hashlib
import json
import pathlib
import shutil
import subprocess
import sys
import tempfile
from xml.sax.saxutils import escape

import docx
from docx.oxml import parse_xml
from docx.oxml.ns import nsdecls

ROOT = pathlib.Path(__file__).resolve().parent
OUT = ROOT / "generated" / "docx"
README = ROOT / "README.md"
AUTHOR = "Fixture Author"
DATE = "2026-01-15T10:00:00Z"
W = nsdecls("w")


class Fixture:
    """A python-docx document and its ground truth, built side by side."""

    def __init__(self, name: str):
        self.name = name
        self.doc = docx.Document()
        self.elements: list[dict] = []
        self.absent: list[str] = []
        self.once: list[str] = []
        self._ids = 100
        self._numbering_ready = False

    def next_id(self) -> int:
        self._ids += 1
        return self._ids

    # Plain blocks.

    def heading(self, text: str, level: int) -> None:
        self.doc.add_heading(text, level)
        self.elements.append({"kind": "heading", "level": level, "text": text})

    def outline_heading(self, text: str, level: int) -> None:
        """A Normal-style paragraph made a heading by its outline level alone."""
        p = self.doc.add_paragraph(text)
        p._p.get_or_add_pPr().append(parse_xml(f'<w:outlineLvl {W} w:val="{level - 1}"/>'))
        self.elements.append({"kind": "heading", "level": level, "text": text})

    def paragraph(self, text: str) -> None:
        self.doc.add_paragraph(text)
        self.elements.append({"kind": "paragraph", "text": text})

    def paragraph_with_break(self, first: str, second: str) -> None:
        p = self.doc.add_paragraph()
        run = p.add_run(first)
        run.add_break()
        p.add_run(second)
        self.elements.append({"kind": "paragraph", "text": f"{first}\n{second}"})

    # Lists, numbered through definitions this script adds to the numbering part.

    def _numbering(self) -> None:
        if self._numbering_ready:
            return
        numbering = self.doc.part.numbering_part.element

        def lvl(ilvl: int, fmt: str, text: str) -> str:
            indent = 720 * (ilvl + 1)
            return (
                f'<w:lvl w:ilvl="{ilvl}"><w:start w:val="1"/><w:numFmt w:val="{fmt}"/>'
                f'<w:lvlText w:val="{text}"/><w:lvlJc w:val="left"/>'
                f'<w:pPr><w:ind w:left="{indent}" w:hanging="360"/></w:pPr></w:lvl>'
            )

        bullets = lvl(0, "bullet", "•") + lvl(1, "bullet", "◦") + lvl(2, "bullet", "▪")
        mixed = lvl(0, "decimal", "%1.") + lvl(1, "bullet", "•") + lvl(2, "lowerLetter", "%3)")
        abstracts = [
            f'<w:abstractNum {W} w:abstractNumId="10"><w:multiLevelType w:val="hybridMultilevel"/>{bullets}</w:abstractNum>',
            f'<w:abstractNum {W} w:abstractNumId="11"><w:multiLevelType w:val="hybridMultilevel"/>{mixed}</w:abstractNum>',
        ]
        nums = [
            f'<w:num {W} w:numId="10"><w:abstractNumId w:val="10"/></w:num>',
            f'<w:num {W} w:numId="11"><w:abstractNumId w:val="11"/></w:num>',
        ] + [
            # Instances of one abstract definition share its count unless they restart it.
            f'<w:num {W} w:numId="{num_id}"><w:abstractNumId w:val="11"/>'
            f'<w:lvlOverride w:ilvl="0"><w:startOverride w:val="1"/></w:lvlOverride></w:num>'
            for num_id in (12, 13, 14)
        ]
        # The schema orders every w:abstractNum before every w:num.
        first_num = numbering.find(f"{{{numbering.nsmap['w']}}}num")
        for xml in abstracts:
            el = parse_xml(xml)
            if first_num is None:
                numbering.append(el)
            else:
                first_num.addprevious(el)
        for xml in nums:
            numbering.append(parse_xml(xml))
        self._numbering_ready = True

    def _number(self, paragraph, num_id: int, level: int) -> None:
        self._numbering()
        num_pr = paragraph._p.get_or_add_pPr().get_or_add_numPr()
        num_pr.get_or_add_ilvl().val = level
        num_pr.get_or_add_numId().val = num_id

    def _list_item(self, text: str, level: int, ordered: bool, new_list: bool) -> None:
        """The ground truth of a list item. It starts a list when it follows anything but a
        list item, when it is deeper than the item before it, or when the definition says it
        starts a new list at its level (new_list)."""
        previous = self.elements[-1] if self.elements else None
        start = new_list or previous is None or previous["kind"] != "listItem" or previous["level"] < level
        self.elements.append(
            {"kind": "listItem", "level": level, "ordered": ordered, "start": start, "text": text}
        )

    def item(self, text: str, num_id: int, level: int, ordered: bool, new_list: bool = False) -> None:
        p = self.doc.add_paragraph(text, style="List Paragraph")
        self._number(p, num_id, level)
        self._list_item(text, level, ordered, new_list)

    # Tables. Each cell is (text, colSpan, rowSpan); None marks a covered grid position.

    def table(self, grid: list[list[tuple[str, int, int] | None]]) -> None:
        rows, cols = len(grid), len(grid[0])
        table = self.doc.add_table(rows=rows, cols=cols)
        table.style = "Table Grid"
        truth_rows = []
        for r, row in enumerate(grid):
            truth_row = []
            for c, spec in enumerate(row):
                if spec is None:
                    continue
                text, col_span, row_span = spec
                cell = table.cell(r, c)
                if col_span > 1 or row_span > 1:
                    cell = cell.merge(table.cell(r + row_span - 1, c + col_span - 1))
                paragraphs = text.split("\n")
                cell.paragraphs[0].text = paragraphs[0]
                for extra in paragraphs[1:]:
                    cell.add_paragraph(extra)
                truth_row.append({"text": text, "colSpan": col_span, "rowSpan": row_span})
            truth_rows.append(truth_row)
        self.elements.append({"kind": "table", "rows": truth_rows})

    # Tracked changes, as raw XML. A segment is (kind, text), kind one of
    # "text", "ins", "del", "moveFrom", "moveTo" (one move per fixture, named
    # move1); the accepted text keeps "text", "ins", and "moveTo".

    def _run(self, text: str, deleted: bool = False) -> str:
        tag = "w:delText" if deleted else "w:t"
        return f'<w:r><{tag} xml:space="preserve">{escape(text)}</{tag}></w:r>'

    def _segments(self, segments: list[tuple[str, str]]) -> str:
        attrs = f'w:author="{AUTHOR}" w:date="{DATE}"'
        xml = []
        for kind, text in segments:
            if kind == "text":
                xml.append(self._run(text))
            elif kind == "ins":
                xml.append(f'<w:ins w:id="{self.next_id()}" {attrs}>{self._run(text)}</w:ins>')
            elif kind == "del":
                xml.append(f'<w:del w:id="{self.next_id()}" {attrs}>{self._run(text, True)}</w:del>')
            elif kind in ("moveFrom", "moveTo"):
                range_id = self.next_id()
                xml.append(
                    f'<w:{kind}RangeStart w:id="{range_id}" {attrs} w:name="move1"/>'
                    f'<w:{kind} w:id="{self.next_id()}" {attrs}>{self._run(text)}</w:{kind}>'
                    f'<w:{kind}RangeEnd w:id="{range_id}"/>'
                )
            else:
                raise ValueError(kind)
        return "".join(xml)

    @staticmethod
    def accepted(segments: list[tuple[str, str]]) -> str:
        return "".join(text for kind, text in segments if kind in ("text", "ins", "moveTo"))

    def _tracked_paragraph(self, segments, mark: str | None = None, style: str | None = None):
        """A paragraph of segments; mark "del" or "ins" tracks its paragraph mark."""
        p = self.doc.add_paragraph(style=style)
        for el in parse_xml(f"<w:body {W}>{self._segments(segments)}</w:body>"):
            p._p.append(el)
        if mark is not None:
            p._p.get_or_add_pPr().append(
                parse_xml(
                    f'<w:rPr {W}><w:{mark} w:id="{self.next_id()}" w:author="{AUTHOR}" w:date="{DATE}"/></w:rPr>'
                )
            )
        return p

    def tracked(self, segments, mark: str | None = None) -> None:
        self._tracked_paragraph(segments, mark)
        if mark != "del":
            self.elements.append({"kind": "paragraph", "text": self.accepted(segments)})

    def tracked_joined(self, first, second) -> None:
        """Two paragraphs, the first's mark deleted: accepted, they are one."""
        self._tracked_paragraph(first, "del")
        self._tracked_paragraph(second)
        text = self.accepted(first) + self.accepted(second)
        if text.strip():
            self.elements.append({"kind": "paragraph", "text": text})

    def tracked_item(self, segments, num_id: int, level: int, ordered: bool) -> None:
        p = self._tracked_paragraph(segments, style="List Paragraph")
        self._number(p, num_id, level)
        self._list_item(self.accepted(segments), level, ordered, False)

    def tracked_table(self, cells: list[list[tuple[str, str]]]) -> None:
        table = self.doc.add_table(rows=1, cols=len(cells))
        table.style = "Table Grid"
        row = []
        for c, segments in enumerate(cells):
            p = table.cell(0, c).paragraphs[0]._p
            for el in parse_xml(f"<w:body {W}>{self._segments(segments)}</w:body>"):
                p.append(el)
            row.append({"text": self.accepted(segments), "colSpan": 1, "rowSpan": 1})
        self.elements.append({"kind": "table", "rows": [row]})

    def truth(self) -> dict:
        return {
            "fixture": self.name,
            "format": "docx",
            "elements": self.elements,
            "absent": self.absent,
            "once": self.once,
        }


def headings() -> Fixture:
    f = Fixture("headings")
    f.heading("Quarterly Operations Report", 1)
    f.paragraph("This report summarizes operations for the third quarter.")
    f.heading("Scope", 2)
    f.paragraph("The scope covers three regional offices.")
    f.heading("Regional detail", 3)
    f.paragraph_with_break("North office: stable.", "South office: expanding.")
    f.heading("Notes on method", 4)
    f.paragraph("Figures marked * are estimates; see [appendix] for the _raw_ data | 2024 & later.")
    f.outline_heading("Outline-level heading", 2)
    f.paragraph("1. This paragraph starts like a numbered item but is plain text.")
    f.paragraph("# Not a heading either.")
    return f


def lists() -> Fixture:
    f = Fixture("lists")
    f.paragraph("Bulleted, nested three deep:")
    f.item("Fruit", 10, 0, False)
    f.item("Apples", 10, 1, False)
    f.item("Granny Smith", 10, 2, False)
    f.item("Pears", 10, 1, False)
    f.item("Vegetables", 10, 0, False)
    f.paragraph("Ordered, with a nested bullet list:")
    f.item("Prepare the site", 11, 0, True)
    f.item("Clear debris", 11, 1, False)
    f.item("Mark boundaries", 11, 1, False)
    f.item("Pour the foundation", 11, 0, True)
    f.item("Cure for seven days", 11, 0, True)
    f.paragraph("A second ordered list restarts its numbering:")
    f.item("Restart one", 12, 0, True)
    f.item("Restart two", 12, 0, True)
    f.item("Bullet under restart", 12, 1, False)
    f.item("Lettered detail", 12, 2, True)
    f.paragraph("Two ordered lists back to back, the second restarting, then a bullet list:")
    f.item("Back one", 13, 0, True)
    f.item("Back two", 13, 0, True)
    f.item("Again one", 14, 0, True, new_list=True)
    f.item("Again two", 14, 0, True)
    f.item("Bullet right after", 10, 0, False, new_list=True)
    f.paragraph("End of lists.")
    return f


def tables() -> Fixture:
    f = Fixture("tables")
    f.heading("Tables", 1)
    f.table(
        [
            [("Region", 1, 1), ("Q3", 1, 1), ("Q4", 1, 1)],
            [("North\n(provisional)", 1, 1), ("Pending audit", 2, 1), None],
            [("West", 1, 2), ("12", 1, 1), ("14", 1, 1)],
            [None, ("9", 1, 1), ("11 | revised", 1, 1)],
        ]
    )
    f.paragraph("Text after the table.")
    return f


def tracked_changes() -> Fixture:
    f = Fixture("tracked-changes")
    f.heading("Tracked changes", 1)
    f.tracked(
        [
            ("text", "The committee "),
            ("ins", "carefully "),
            ("text", "reviewed the proposal"),
            ("del", " and the obsolete appendix"),
            ("text", "."),
        ]
    )
    f.tracked(
        [
            ("text", "Budget figures follow. "),
            ("moveFrom", "This sentence moves down. "),
            ("text", "Staffing is unchanged."),
        ]
    )
    f.tracked([("text", "Closing remarks. "), ("moveTo", "This sentence moves down.")])
    f.tracked_joined([("text", "This sentence was split ")], [("text", "across two paragraphs.")])
    f.tracked_joined(
        [("del", "An entirely withdrawn paragraph.")],
        [("text", "The paragraph after the removed one.")],
    )
    f.tracked([("ins", "A newly inserted paragraph.")], mark="ins")
    f.tracked_item([("text", "Kept item"), ("del", " with withdrawn words")], 10, 0, False)
    f.tracked_item([("ins", "Inserted item")], 10, 0, False)
    f.tracked_table(
        [
            [("text", "Cell "), ("del", "stale"), ("ins", "fresh"), ("text", " value")],
            [("text", "Plain cell")],
        ]
    )
    f.absent = ["obsolete appendix", "withdrawn", "stale"]
    f.once = ["This sentence moves down"]
    return f


FIXTURES = [headings, lists, tables, tracked_changes]


def soffice_version() -> str:
    out = subprocess.run(["soffice", "--version"], check=True, capture_output=True, text=True)
    return out.stdout.strip()


def resave(source: pathlib.Path, target: pathlib.Path) -> None:
    """Re-save source as target through LibreOffice headless, in a throwaway profile."""
    with tempfile.TemporaryDirectory() as tmp:
        tmpdir = pathlib.Path(tmp)
        subprocess.run(
            [
                "soffice",
                f"-env:UserInstallation={(tmpdir / 'profile').as_uri()}",
                "--headless",
                "--convert-to",
                "docx:MS Word 2007 XML",
                "--outdir",
                str(tmpdir / "out"),
                str(source),
            ],
            check=True,
            capture_output=True,
        )
        shutil.move(tmpdir / "out" / source.name, target)


def sha256(path: pathlib.Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def update_readme(version: str, files: list[pathlib.Path]) -> None:
    begin, end = "<!-- generated: begin -->", "<!-- generated: end -->"
    text = README.read_text()
    head, rest = text.split(begin, 1)
    _, tail = rest.split(end, 1)
    lines = [f"LibreOffice: `{version}`", "", "| File | SHA-256 |", "| --- | --- |"]
    lines += [f"| `{p.relative_to(ROOT)}` | `{sha256(p)}` |" for p in files]
    README.write_text(head + begin + "\n" + "\n".join(lines) + "\n" + end + tail)


def main() -> int:
    if shutil.which("soffice") is None:
        print("generate: soffice (LibreOffice) is not on PATH", file=sys.stderr)
        return 1
    OUT.mkdir(parents=True, exist_ok=True)
    files = []
    for build in FIXTURES:
        fixture = build()
        original = OUT / f"{fixture.name}.python-docx.docx"
        resaved = OUT / f"{fixture.name}.libreoffice.docx"
        truth = OUT / f"{fixture.name}.truth.json"
        fixture.doc.save(original)
        resave(original, resaved)
        truth.write_text(json.dumps(fixture.truth(), indent=2, ensure_ascii=False) + "\n")
        files += [original, resaved, truth]
    update_readme(soffice_version(), files)
    for path in files:
        print(f"{sha256(path)}  {path.relative_to(ROOT)}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
