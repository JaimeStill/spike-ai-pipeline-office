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
import io
import json
import pathlib
import shutil
import struct
import subprocess
import sys
import tempfile
import zlib
from contextlib import contextmanager
from dataclasses import dataclass
from xml.sax.saxutils import escape

import docx
from docx.enum.section import WD_SECTION
from docx.opc.constants import CONTENT_TYPE as CT
from docx.opc.constants import RELATIONSHIP_TYPE as RT
from docx.opc.packuri import PackURI
from docx.opc.part import Part
from docx.oxml import parse_xml
from docx.oxml.ns import nsdecls
from docx.parts.image import ImagePart
from docx.shared import Inches

ROOT = pathlib.Path(__file__).resolve().parent
OUT = ROOT / "generated" / "docx"
README = ROOT / "README.md"
AUTHOR = "Fixture Author"
DATE = "2026-01-15T10:00:00Z"
W = nsdecls("w")
NS = nsdecls("w", "wp", "a", "pic", "r", "c")
W15 = "http://schemas.microsoft.com/office/word/2012/wordml"
CT_COMMENTS_EXTENDED = "application/vnd.openxmlformats-officedocument.wordprocessingml.commentsExtended+xml"
RT_COMMENTS_EXTENDED = "http://schemas.microsoft.com/office/2011/relationships/commentsExtended"
EMU = 457200  # A drawing's side, half an inch.


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
        # The story blocks are written to (the body, or a header or footer) and the section index
        # the blocks are in.
        self.target = self.doc
        self.section = 0
        # Each section's headers and footers in the ground truth, when the fixture declares them.
        self.sections: list[dict] | None = None
        self.notes: dict[str, dict] = {"footnote": {}, "endnote": {}}
        self._note_order: dict[str, list[str]] = {"footnote": [], "endnote": []}
        self.note_truth: dict[str, list[dict]] = {"footnote": [], "endnote": []}
        self.comments: dict[str, dict] = {}
        self.comments_extended = True
        # How the LibreOffice re-save's ground truth differs, when it does: a function that
        # adjusts a copy of the truth, written as <name>.libreoffice.truth.json.
        self.libreoffice = None
        # Comments python-docx writes itself, with their ground truth.
        self.plain_comments: list[dict] = []
        self._raw_parts: dict[str, ImagePart] = {}
        self._charts = 0

    def next_id(self) -> int:
        self._ids += 1
        return self._ids

    # Plain blocks.

    def heading(self, text: str, level: int) -> None:
        self.doc.add_heading(text, level)
        self.elements.append({"kind": "heading", "level": level, "text": text})

    def outline_heading(self, text: str, level: int) -> None:
        """A Normal-style paragraph made a heading by its outline level alone."""
        p = self.target.add_paragraph(text)
        p._p.get_or_add_pPr().append(parse_xml(f'<w:outlineLvl {W} w:val="{level - 1}"/>'))
        self.elements.append({"kind": "heading", "level": level, "text": text})

    def paragraph(self, text: str) -> None:
        self.target.add_paragraph(text)
        self.elements.append({"kind": "paragraph", "text": text})

    def paragraph_with_break(self, first: str, second: str) -> None:
        p = self.target.add_paragraph()
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
        p = self.target.add_paragraph(text, style="List Paragraph")
        self._number(p, num_id, level)
        self._list_item(text, level, ordered, new_list)

    # Tables. Each cell is (text, colSpan, rowSpan); None marks a covered grid position.

    def table(self, grid: list[list[tuple[str, int, int] | None]]) -> None:
        rows, cols = len(grid), len(grid[0])
        table = self._add_table(rows, cols)
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

    # Segments, as raw XML. A segment is (kind, value):
    # - "text", "ins", "del", "moveFrom", "moveTo": text, tracked or not (one move per fixture,
    #   named move1); the accepted text keeps "text", "ins", and "moveTo";
    # - "footnote", "endnote": the key of a note this fixture defines, referenced here;
    #   "delNote": (kind, key), a reference inside a deletion, so the note is never referenced;
    # - "commentStart", "commentEnd": the key of a comment this fixture defines, whose anchored
    #   range (and its replies') starts or ends here;
    # - "picture", "delPicture": a Picture, in a drawing of its own, or inside a deletion;
    # - "chart": a Chart, in a drawing of its own.
    # References, pictures, and charts add no text; a reference marks its offset in the text, and
    # a picture or chart is a block of its own after its paragraph.

    def _run(self, text: str, deleted: bool = False) -> str:
        tag = "w:delText" if deleted else "w:t"
        return f'<w:r><{tag} xml:space="preserve">{escape(text)}</{tag}></w:r>'

    def _segments(self, segments: list[tuple[str, object]], part) -> tuple[str, list[dict], list[dict]]:
        """The XML of segments in the given story part, the marks of the references they hold,
        and the blocks (media and charts) that follow their paragraph."""
        attrs = f'w:author="{AUTHOR}" w:date="{DATE}"'
        xml, marks, after = [], [], []
        offset = 0
        for kind, value in segments:
            if kind == "text":
                xml.append(self._run(value))
            elif kind == "ins":
                xml.append(f'<w:ins w:id="{self.next_id()}" {attrs}>{self._run(value)}</w:ins>')
            elif kind == "del":
                xml.append(f'<w:del w:id="{self.next_id()}" {attrs}>{self._run(value, True)}</w:del>')
            elif kind in ("moveFrom", "moveTo"):
                range_id = self.next_id()
                xml.append(
                    f'<w:{kind}RangeStart w:id="{range_id}" {attrs} w:name="move1"/>'
                    f'<w:{kind} w:id="{self.next_id()}" {attrs}>{self._run(value)}</w:{kind}>'
                    f'<w:{kind}RangeEnd w:id="{range_id}"/>'
                )
            elif kind in ("footnote", "endnote"):
                xml.append(self._note_reference(kind, value))
                marks.append({"at": offset, "kind": kind, "n": self._reference_note(kind, value)})
            elif kind == "delNote":
                note_kind, key = value
                xml.append(f'<w:del w:id="{self.next_id()}" {attrs}>{self._note_reference(note_kind, key)}</w:del>')
            elif kind == "commentStart":
                thread = self.comments[value]
                xml.append("".join(f'<w:commentRangeStart w:id="{i}"/>' for i in thread["ids"]))
            elif kind == "commentEnd":
                thread = self.comments[value]
                xml.append(
                    "".join(
                        f'<w:commentRangeEnd w:id="{i}"/>'
                        f'<w:r><w:rPr><w:rStyle w:val="CommentReference"/></w:rPr><w:commentReference w:id="{i}"/></w:r>'
                        for i in thread["ids"]
                    )
                )
                marks.append({"at": offset, "kind": "comment", "n": list(self.comments).index(value) + 1})
            elif kind == "picture":
                xml.append(f"<w:r>{self._picture_drawing(value, part)}</w:r>")
                after.append(value.truth(self.section))
            elif kind == "delPicture":
                xml.append(f'<w:del w:id="{self.next_id()}" {attrs}><w:r>{self._picture_drawing(value, part)}</w:r></w:del>')
            elif kind == "chart":
                xml.append(f"<w:r>{self._chart_drawing(value, part)}</w:r>")
                after.append(value.truth())
            else:
                raise ValueError(kind)
            if kind in ("text", "ins", "moveTo"):
                offset += len(value)
        return "".join(xml), marks, after

    @staticmethod
    def accepted(segments: list[tuple[str, object]]) -> str:
        return "".join(value for kind, value in segments if kind in ("text", "ins", "moveTo"))

    def _fill(self, p, segments) -> tuple[list[dict], list[dict]]:
        """Appends segments to a python-docx paragraph; returns their marks and the blocks after."""
        xml, marks, after = self._segments(segments, p.part)
        for el in parse_xml(f"<w:body {NS}>{xml}</w:body>"):
            p._p.append(el)
        return marks, after

    def _tracked_paragraph(self, segments, mark: str | None = None, style: str | None = None):
        """A paragraph of segments; mark "del" or "ins" tracks its paragraph mark. Returns the
        paragraph, the marks of its references, and the blocks after it."""
        p = self.target.add_paragraph(style=style)
        marks, after = self._fill(p, segments)
        if mark is not None:
            p._p.get_or_add_pPr().append(
                parse_xml(
                    f'<w:rPr {W}><w:{mark} w:id="{self.next_id()}" w:author="{AUTHOR}" w:date="{DATE}"/></w:rPr>'
                )
            )
        return p, marks, after

    def _paragraph_truth(self, text: str, marks: list[dict], after: list[dict]) -> None:
        """A paragraph's ground truth: the paragraph unless it holds no text and no reference
        (then it is spacing), then the blocks after it."""
        if text.strip() or marks:
            element = {"kind": "paragraph", "text": text}
            if marks:
                element["marks"] = marks
            self.elements.append(element)
        self.elements.extend(after)

    def tracked(self, segments, mark: str | None = None) -> None:
        """A paragraph of segments, its mark tracked or not."""
        _, marks, after = self._tracked_paragraph(segments, mark)
        if mark != "del":
            self._paragraph_truth(self.accepted(segments), marks, after)

    def tracked_joined(self, first, second) -> None:
        """Two paragraphs, the first's mark deleted: accepted, they are one."""
        _, marks1, after1 = self._tracked_paragraph(first, "del")
        _, marks2, after2 = self._tracked_paragraph(second)
        shift = len(self.accepted(first))
        marks = marks1 + [{**m, "at": m["at"] + shift} for m in marks2]
        self._paragraph_truth(self.accepted(first) + self.accepted(second), marks, after1 + after2)

    def tracked_before_table(self, segments) -> None:
        """A paragraph whose mark was deleted, right before a table: accepted, its text stays a
        paragraph of its own before the table, as nothing follows it to join."""
        _, marks, after = self._tracked_paragraph(segments, "del")
        self._paragraph_truth(self.accepted(segments), marks, after)

    def tracked_item(self, segments, num_id: int, level: int, ordered: bool) -> None:
        p, marks, _ = self._tracked_paragraph(segments, style="List Paragraph")
        self._number(p, num_id, level)
        self._list_item(self.accepted(segments), level, ordered, False)
        if marks:
            self.elements[-1]["marks"] = marks

    def tracked_table(self, cells: list[list[tuple[str, object]]]) -> None:
        """A one-row table, a paragraph of segments in each cell. A cell's pictures and charts
        are listed as its media."""
        table = self._add_table(1, len(cells))
        table.style = "Table Grid"
        row = []
        for c, segments in enumerate(cells):
            _, after = self._fill(table.cell(0, c).paragraphs[0], segments)
            cell = {"text": self.accepted(segments), "colSpan": 1, "rowSpan": 1}
            if after:
                cell["media"] = after
            row.append(cell)
        self.elements.append({"kind": "table", "rows": [row]})

    def _add_table(self, rows: int, cols: int):
        # A header's or footer's table needs a width; the body's takes the page's.
        if self.target is self.doc:
            return self.doc.add_table(rows=rows, cols=cols)
        return self.target.add_table(rows, cols, Inches(6))

    # Stories other than the body: headers and footers, by python-docx; the section index their
    # media are placed in is the section they are read for.

    @contextmanager
    def story(self, container, section: int):
        """Builds into a header or footer; yields the ground truth of its blocks."""
        saved = self.target, self.elements, self.section
        self.target, self.elements, self.section = container, [], section
        try:
            yield self.elements
        finally:
            self.target, self.elements, self.section = saved

    def new_section(self):
        """Starts a new section; returns it, its header and footer references inherited."""
        self.section += 1
        return self.doc.add_section(WD_SECTION.NEW_PAGE)

    # Pictures and charts, in drawings written as raw XML.

    def _picture_rid(self, pic: "Picture", part) -> str:
        if pic.link is not None:
            return part.relate_to(pic.link, RT.IMAGE, is_external=True)
        if pic.part is not None:
            # A part of its own, so two parts can hold the same bytes; python-docx would share one.
            if pic.part not in self._raw_parts:
                self._raw_parts[pic.part] = ImagePart(PackURI(pic.part), pic.content_type, pic.data)
            return part.relate_to(self._raw_parts[pic.part], RT.IMAGE)
        rid, _ = part.get_or_add_image(io.BytesIO(pic.data))
        return rid

    def _picture_drawing(self, pic: "Picture", part) -> str:
        rid = self._picture_rid(pic, part)
        blip = f'r:link="{rid}"' if pic.link is not None else f'r:embed="{rid}"'
        shape_id = self.next_id()
        return (
            f"<w:drawing>{self._inline_open(shape_id, pic.descr, pic.title)}"
            f'<a:graphic><a:graphicData uri="http://schemas.openxmlformats.org/drawingml/2006/picture">'
            f'<pic:pic><pic:nvPicPr><pic:cNvPr id="{shape_id}" name="Picture {shape_id}"/><pic:cNvPicPr/></pic:nvPicPr>'
            f"<pic:blipFill><a:blip {blip}/><a:stretch><a:fillRect/></a:stretch></pic:blipFill>"
            f'<pic:spPr><a:xfrm><a:off x="0" y="0"/><a:ext cx="{EMU}" cy="{EMU}"/></a:xfrm>'
            f'<a:prstGeom prst="rect"><a:avLst/></a:prstGeom></pic:spPr></pic:pic>'
            f"</a:graphicData></a:graphic></wp:inline></w:drawing>"
        )

    def _chart_drawing(self, chart: "Chart", part) -> str:
        self._charts += 1
        chart_part = Part(
            PackURI(f"/word/charts/chart{self._charts}.xml"), CT.DML_CHART, chart.xml().encode(), self.doc.part.package
        )
        rid = part.relate_to(chart_part, RT.CHART)
        shape_id = self.next_id()
        return (
            f'<w:drawing>{self._inline_open(shape_id, "", "")}'
            f'<a:graphic><a:graphicData uri="http://schemas.openxmlformats.org/drawingml/2006/chart">'
            f'<c:chart r:id="{rid}"/></a:graphicData></a:graphic></wp:inline></w:drawing>'
        )

    @staticmethod
    def _inline_open(shape_id: int, descr: str, title: str) -> str:
        alt = (f' descr="{escape(descr)}"' if descr else "") + (f' title="{escape(title)}"' if title else "")
        return (
            f'<wp:inline distT="0" distB="0" distL="0" distR="0"><wp:extent cx="{EMU}" cy="{EMU}"/>'
            f'<wp:docPr id="{shape_id}" name="Drawing {shape_id}"{alt}/>'
            f'<wp:cNvGraphicFramePr><a:graphicFrameLocks noChangeAspect="1"/></wp:cNvGraphicFramePr>'
        )

    # Footnotes and endnotes, written as raw parts with Word's separator and continuation notes.
    # A note's ground truth is listed in the order the body first references it.

    def note(self, kind: str, key: str, paragraphs: list[str]) -> None:
        """Defines a footnote or endnote, its ids numbered in definition order."""
        notes = self.notes[kind]
        notes[key] = {"id": len(notes) + 1, "paragraphs": paragraphs}

    def _note_reference(self, kind: str, key: str) -> str:
        return (
            f'<w:r><w:rPr><w:vertAlign w:val="superscript"/></w:rPr>'
            f'<w:{kind}Reference w:id="{self.notes[kind][key]["id"]}"/></w:r>'
        )

    def _reference_note(self, kind: str, key: str) -> int:
        order = self._note_order[kind]
        if key not in order:
            order.append(key)
            # The first paragraph starts with the reference mark and Word's space after it.
            paragraphs = self.notes[kind][key]["paragraphs"]
            texts = [" " + paragraphs[0]] + paragraphs[1:]
            self.note_truth[kind].append({"elements": [{"kind": "paragraph", "text": t} for t in texts]})
        return order.index(key) + 1

    def _notes_part(self, kind: str) -> None:
        notes = self.notes[kind]
        if not notes:
            return
        tag = f"w:{kind}"
        separators = (
            f'<{tag} w:type="separator" w:id="-1"><w:p><w:r><w:separator/></w:r></w:p></{tag}>'
            f'<{tag} w:type="continuationSeparator" w:id="0"><w:p><w:r><w:continuationSeparator/></w:r></w:p></{tag}>'
        )
        body = []
        for note in notes.values():
            paragraphs = []
            for i, text in enumerate(note["paragraphs"]):
                ref = f"<w:r><w:rPr><w:vertAlign w:val=\"superscript\"/></w:rPr><w:{kind}Ref/></w:r>" if i == 0 else ""
                lead = " " if i == 0 else ""
                paragraphs.append(f"<w:p>{ref}{self._run(lead + text)}</w:p>")
            body.append(f'<{tag} w:id="{note["id"]}">{"".join(paragraphs)}</{tag}>')
        xml = f'<?xml version="1.0" encoding="UTF-8" standalone="yes"?>\n<{tag}s {W}>{separators}{"".join(body)}</{tag}s>'
        content_type, reltype = (CT.WML_FOOTNOTES, RT.FOOTNOTES) if kind == "footnote" else (CT.WML_ENDNOTES, RT.ENDNOTES)
        part = Part(PackURI(f"/word/{kind}s.xml"), content_type, xml.encode(), self.doc.part.package)
        self.doc.part.relate_to(part, reltype)

    # Comments, written as a raw comments part and, unless the fixture leaves it out, a
    # commentsExtended part giving each reply its parent and each thread its resolved state. A
    # thread's root and replies share its anchored range, as Word writes them.

    def comment(self, key: str, author: str, paragraphs: list[str], anchor: str, resolved: bool = False,
                replies: list[tuple[str, list[str]]] = ()) -> None:
        start = sum(len(t["ids"]) for t in self.comments.values())
        self.comments[key] = {
            "ids": list(range(start, start + 1 + len(replies))),
            "author": author,
            "paragraphs": paragraphs,
            "anchor": anchor,
            "resolved": resolved,
            "replies": list(replies),
        }

    def _comments_parts(self) -> None:
        if not self.comments:
            return
        comments, extended = [], []
        para = 0
        for thread in self.comments.values():
            entries = [(thread["author"], thread["paragraphs"])] + thread["replies"]
            root_para = None
            for comment_id, (author, texts) in zip(thread["ids"], entries):
                paragraphs = []
                for i, text in enumerate(texts):
                    para += 1
                    ref = "<w:r><w:annotationRef/></w:r>" if i == 0 else ""
                    paragraphs.append(f'<w:p w14:paraId="{para:08X}" w14:textId="77777777">{ref}{self._run(text)}</w:p>')
                initials = "".join(word[0] for word in author.split())
                comments.append(
                    f'<w:comment w:id="{comment_id}" w:author="{escape(author)}" w:date="{DATE}" w:initials="{initials}">'
                    f'{"".join(paragraphs)}</w:comment>'
                )
                parent = f' w15:paraIdParent="{root_para:08X}"' if root_para is not None else ""
                extended.append(f'<w15:commentEx w15:paraId="{para:08X}"{parent} w15:done="{int(thread["resolved"])}"/>')
                root_para = root_para if root_para is not None else para
        xml = f'<?xml version="1.0" encoding="UTF-8" standalone="yes"?>\n<w:comments {nsdecls("w", "w14")}>{"".join(comments)}</w:comments>'
        part = Part(PackURI("/word/comments.xml"), CT.WML_COMMENTS, xml.encode(), self.doc.part.package)
        self.doc.part.relate_to(part, RT.COMMENTS)
        if not self.comments_extended:
            return
        xml = (
            f'<?xml version="1.0" encoding="UTF-8" standalone="yes"?>\n'
            f'<w15:commentsEx xmlns:w15="{W15}">{"".join(extended)}</w15:commentsEx>'
        )
        part = Part(PackURI("/word/commentsExtended.xml"), CT_COMMENTS_EXTENDED, xml.encode(), self.doc.part.package)
        self.doc.part.relate_to(part, RT_COMMENTS_EXTENDED)

    def finish(self) -> None:
        """Writes the parts the definitions add: notes and comments."""
        self._notes_part("footnote")
        self._notes_part("endnote")
        self._comments_parts()

    def truth(self) -> dict:
        truth = {
            "fixture": self.name,
            "format": "docx",
            "elements": self.elements,
            "absent": self.absent,
            "once": self.once,
        }
        if self.sections is not None:
            truth["sections"] = self.sections
        if self.note_truth["footnote"]:
            truth["footnotes"] = self.note_truth["footnote"]
        if self.note_truth["endnote"]:
            truth["endnotes"] = self.note_truth["endnote"]
        if self.comments or self.plain_comments:
            truth["comments"] = self.plain_comments + [
                {
                    "author": t["author"],
                    "anchor": t["anchor"],
                    "resolved": t["resolved"],
                    "elements": [{"kind": "paragraph", "text": p} for p in t["paragraphs"]],
                    "replies": [
                        {"author": a, "elements": [{"kind": "paragraph", "text": p} for p in ps]}
                        for a, ps in t["replies"]
                    ],
                }
                for t in self.comments.values()
            ]
        return truth


@dataclass
class Picture:
    """A picture: embedded, through python-docx's image parts (one per distinct content) or a
    part of its own, or linked to an external target, with no part."""

    descr: str
    title: str = ""
    data: bytes = b""
    ext: str = "png"
    content_type: str = CT.PNG
    part: str | None = None
    link: str | None = None

    def truth(self, section: int) -> dict:
        element = {"kind": "media"}
        if self.link is None:
            element["name"] = hashlib.sha256(self.data).hexdigest() + "." + self.ext
            element["contentType"] = self.content_type
        else:
            element["target"] = self.link
        if self.descr:
            element["alt"] = self.descr
        if self.title:
            element["title"] = self.title
        element["section"] = section
        return element


@dataclass
class Chart:
    """A chart part's data: its type (the plot element's name, less "Chart"), title, and series
    of name, categories, and values, a missing value None. The series are cached in the chart
    part, with no embedded workbook."""

    chart_type: str
    title: str | None
    series: list[tuple[str, list[str], list[float | None]]]

    def truth(self) -> dict:
        element = {"kind": "chart", "chartType": self.chart_type}
        if self.title is not None:
            element["title"] = self.title
        element["series"] = [{"name": n, "categories": c, "values": v} for n, c, v in self.series]
        return element

    def xml(self) -> str:
        def cache(tag: str, values: list) -> str:
            points = "".join(
                f'<c:pt idx="{i}"><c:v>{escape(str(v))}</c:v></c:pt>' for i, v in enumerate(values) if v is not None
            )
            fmt = "<c:formatCode>General</c:formatCode>" if tag == "numCache" else ""
            return f'<c:{tag}>{fmt}<c:ptCount val="{len(values)}"/>{points}</c:{tag}>'

        series = []
        for i, (name, categories, values) in enumerate(self.series):
            column = chr(ord("B") + i)
            series.append(
                f'<c:ser><c:idx val="{i}"/><c:order val="{i}"/>'
                f"<c:tx><c:strRef><c:f>Sheet1!${column}$1</c:f>{cache('strCache', [name])}</c:strRef></c:tx>"
                f"<c:cat><c:strRef><c:f>Sheet1!$A$2:$A${len(categories) + 1}</c:f>{cache('strCache', categories)}</c:strRef></c:cat>"
                f"<c:val><c:numRef><c:f>Sheet1!${column}$2:${column}${len(values) + 1}</c:f>{cache('numCache', values)}</c:numRef></c:val>"
                f"</c:ser>"
            )
        axes = '<c:axId val="1001"/><c:axId val="1002"/>' if self.chart_type != "pie" else ""
        plot = {
            "bar": '<c:barDir val="col"/><c:grouping val="clustered"/><c:varyColors val="0"/>',
            "line": '<c:grouping val="standard"/><c:varyColors val="0"/>',
            "pie": '<c:varyColors val="1"/>',
        }[self.chart_type]
        marker = '<c:marker val="1"/>' if self.chart_type == "line" else ""
        axis_xml = (
            '<c:catAx><c:axId val="1001"/><c:scaling><c:orientation val="minMax"/></c:scaling><c:delete val="0"/>'
            '<c:axPos val="b"/><c:crossAx val="1002"/></c:catAx>'
            '<c:valAx><c:axId val="1002"/><c:scaling><c:orientation val="minMax"/></c:scaling><c:delete val="0"/>'
            '<c:axPos val="l"/><c:crossAx val="1001"/></c:valAx>'
            if self.chart_type != "pie"
            else ""
        )
        title = (
            f'<c:title><c:tx><c:rich><a:bodyPr/><a:p><a:r><a:t>{escape(self.title)}</a:t></a:r></a:p></c:rich></c:tx>'
            f'<c:overlay val="0"/></c:title><c:autoTitleDeleted val="0"/>'
            if self.title is not None
            else '<c:autoTitleDeleted val="1"/>'
        )
        return (
            f'<?xml version="1.0" encoding="UTF-8" standalone="yes"?>\n'
            f'<c:chartSpace {nsdecls("c", "a", "r")}><c:chart>{title}<c:plotArea><c:layout/>'
            f'<c:{self.chart_type}Chart>{plot}{"".join(series)}{marker}{axes}</c:{self.chart_type}Chart>'
            f'{axis_xml}</c:plotArea><c:plotVisOnly val="1"/></c:chart></c:chartSpace>'
        )


def png(rgb: tuple[int, int, int]) -> bytes:
    """A 2x2 PNG of one color, written by hand so the script needs no imaging library."""

    def chunk(kind: bytes, data: bytes) -> bytes:
        return struct.pack(">I", len(data)) + kind + data + struct.pack(">I", zlib.crc32(kind + data))

    rows = b"".join(b"\x00" + bytes(rgb) * 2 for _ in range(2))
    header = struct.pack(">IIBBBBB", 2, 2, 8, 2, 0, 0, 0)
    return b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", header) + chunk(b"IDAT", zlib.compress(rows)) + chunk(b"IEND", b"")


def emf() -> bytes:
    """The smallest EMF: a header record and an end-of-file record, 1 mm square."""
    header = struct.pack(
        "<II4i4iIIIIHHIII4i",
        1, 88,  # EMR_HEADER, its size
        0, 0, 10, 10,  # bounds, device units
        0, 0, 100, 100,  # frame, 0.01 mm
        0x464D4520, 0x10000, 108, 2,  # " EMF", version, file size, records
        1, 0, 0, 0, 0,  # handles, reserved, no description, no palette
        1024, 768, 320, 240,  # reference device, pixels and millimeters
    )
    eof = struct.pack("<IIIII", 14, 20, 0, 16, 20)
    return header + eof


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
    f.tracked_before_table([("text", "A paragraph whose mark was deleted, right before a table.")])
    f.tracked_table(
        [
            [("text", "Cell "), ("del", "stale"), ("ins", "fresh"), ("text", " value")],
            [("text", "Plain cell")],
        ]
    )
    f.absent = ["obsolete appendix", "withdrawn", "stale"]
    f.once = ["This sentence moves down"]
    return f


def libreoffice_alt(truth: dict) -> None:
    """LibreOffice writes a picture's title and description as one description, the title's line
    first, and no title."""

    def walk(node) -> None:
        if isinstance(node, dict):
            if node.get("kind") == "media" and "title" in node:
                title = node.pop("title")
                node["alt"] = f"{title}\n{node['alt']}" if "alt" in node else title
                # Keep the key order of the python-docx truth: alt before section.
                node["section"] = node.pop("section")
            for value in node.values():
                walk(value)
        elif isinstance(node, list):
            for value in node:
                walk(value)

    walk(truth)


def resection(elements: list[dict], section: int) -> list[dict]:
    """A copy of a story's ground truth read for another section: its media placed there."""
    copy = json.loads(json.dumps(elements))
    for element in copy:
        if element["kind"] == "media":
            element["section"] = section
    return copy


def story_truth(**kinds: list[dict]) -> list[dict]:
    return [{"kind": kind, "elements": elements} for kind, elements in kinds.items()]


def sections() -> Fixture:
    """Three sections, with even-page headers on for the document. The first has its own default,
    first-page, and even-page headers and footers and a title page; the second its own default
    and first-page headers, no title page (so its first-page header doesn't count), and inherits
    the rest; the third has a title page and inherits everything, the first-page header the
    second section's."""
    f = Fixture("sections")
    f.doc.settings.odd_and_even_pages_header_footer = True
    logo = Picture("Header logo", "Logo", png((200, 30, 30)))

    s0 = f.doc.sections[0]
    s0.different_first_page_header_footer = True
    with f.story(s0.header, 0) as h0_default:
        f.paragraph("Default header one")
    # The tracked change is in a header no other section inherits: LibreOffice writes an
    # inherited header's copies with its deletions as plain text.
    with f.story(s0.first_page_header, 0) as h0_first:
        f.tracked([("text", "First header "), ("ins", "one"), ("del", " withdrawn")])
    with f.story(s0.even_page_header, 0) as h0_even:
        f.paragraph("Even header one")
    with f.story(s0.footer, 0) as f0_default:
        f.table([[("Footer cell A", 1, 1), ("Footer cell B", 1, 1)]])
    with f.story(s0.first_page_footer, 0) as f0_first:
        f.paragraph("First footer one")
    with f.story(s0.even_page_footer, 0) as f0_even:
        f.item("Even footer item", 10, 0, False)
    f.heading("Section one", 1)
    f.paragraph("Body of section one.")

    s1 = f.new_section()
    s1.different_first_page_header_footer = False
    # python-docx writes a linked header's content into the section it inherits from.
    s1.header.is_linked_to_previous = False
    s1.first_page_header.is_linked_to_previous = False
    with f.story(s1.header, 1) as h1_default:
        f.paragraph("Default header two")
        f.tracked([("picture", logo)])
    with f.story(s1.first_page_header, 1) as h1_first:
        f.paragraph("First header two")
    f.heading("Section two", 1)
    f.paragraph("Body of section two.")

    s2 = f.new_section()
    s2.different_first_page_header_footer = True
    f.heading("Section three", 1)
    f.paragraph("Body of section three.")

    f.sections = [
        {
            "headers": story_truth(default=h0_default, first=h0_first, even=h0_even),
            "footers": story_truth(default=f0_default, first=f0_first, even=f0_even),
        },
        {
            "headers": story_truth(default=h1_default, even=resection(h0_even, 1)),
            "footers": story_truth(default=resection(f0_default, 1), even=resection(f0_even, 1)),
        },
        {
            "headers": story_truth(default=resection(h1_default, 2), first=resection(h1_first, 2), even=resection(h0_even, 2)),
            "footers": story_truth(default=resection(f0_default, 2), first=resection(f0_first, 2), even=resection(f0_even, 2)),
        },
    ]
    f.absent = ["withdrawn"]

    def libreoffice(truth: dict) -> None:
        # LibreOffice gives the third section the default header and footer as its first-page
        # ones: it drops the first-page variants the second section, with no title page, passes on.
        libreoffice_alt(truth)
        for story in ("headers", "footers"):
            kinds = {s["kind"]: s for s in truth["sections"][2][story]}
            kinds["first"]["elements"] = kinds["default"]["elements"]

    f.libreoffice = libreoffice
    return f


def headers_even_off() -> Fixture:
    """First-page and even-page headers written, but no title page and no even-page headers in
    the settings, so only the default header and footer count."""
    f = Fixture("headers-even-off")
    s0 = f.doc.sections[0]
    with f.story(s0.header, 0) as header:
        f.paragraph("Plain header")
    s0.first_page_header.add_paragraph("Unused first header")
    s0.even_page_header.add_paragraph("Unused even header")
    with f.story(s0.footer, 0) as footer:
        f.paragraph("Plain footer")
    f.paragraph("Body text without even-page or first-page variants.")
    f.sections = [{"headers": story_truth(default=header), "footers": story_truth(default=footer)}]
    f.absent = ["Unused first header", "Unused even header"]
    return f


def notes() -> Fixture:
    """Footnotes and endnotes, referenced out of their id order, one note of two paragraphs, a
    reference in a list item, and a footnote whose only reference was deleted."""
    f = Fixture("notes")
    f.note("footnote", "basis", ["Figures are unaudited."])
    f.note("footnote", "method", ["The method has two parts.", "Its second part is a second paragraph."])
    f.note("footnote", "withdrawn", ["A withdrawn footnote."])
    f.note("endnote", "source", ["Source: the regional ledgers."])
    f.note("endnote", "item", ["An endnote from a list item."])
    f.heading("Notes", 1)
    f.tracked(
        [
            ("text", "Revenue rose sharply"),
            ("footnote", "method"),
            ("text", " in the third quarter."),
            ("endnote", "source"),
        ]
    )
    f.tracked([("text", "Costs held steady"), ("footnote", "basis"), ("delNote", ("footnote", "withdrawn")), ("text", ".")])
    f.tracked_item([("text", "An item with an endnote"), ("endnote", "item")], 10, 0, False)
    f.absent = ["A withdrawn footnote"]

    def libreoffice(truth: dict) -> None:
        # LibreOffice pairs footnotes with their references by their order in the notes part, not
        # by id, so in its re-save the first reference shows the first-defined note.
        method, basis = truth["footnotes"]
        truth["footnotes"] = [basis, method]

    f.libreoffice = libreoffice
    return f


def comments() -> Fixture:
    """Comments with commentsExtended: a thread of two replies, a resolved comment spanning two
    paragraphs with two paragraphs of text, and a comment over tracked text."""
    f = Fixture("comments")
    f.comment(
        "budget",
        "Ana Reviewer",
        ["Please check these figures."],
        "budget figures",
        replies=[("Ben Author", ["Checked against the ledger."]), ("Ana Reviewer", ["Thanks."])],
    )
    f.comment(
        "span",
        "Ben Author",
        ["This comment spans two paragraphs.", "Its text has two paragraphs as well."],
        "spans two\nparagraphs",
        resolved=True,
    )
    f.comment("tracked", "Cleo Editor", ["An open comment over tracked text."], "timeline")
    f.heading("Comments", 1)
    f.tracked(
        [
            ("text", "The "),
            ("commentStart", "budget"),
            ("text", "budget figures"),
            ("commentEnd", "budget"),
            ("text", " need review."),
        ]
    )
    f.tracked([("text", "A comment that "), ("commentStart", "span"), ("text", "spans two")])
    f.tracked([("text", "paragraphs"), ("commentEnd", "span"), ("text", " of text.")])
    f.tracked(
        [
            ("text", "The "),
            ("commentStart", "tracked"),
            ("text", "time"),
            ("del", " "),
            ("ins", "line"),
            ("commentEnd", "tracked"),
            ("text", " is new."),
        ]
    )
    return f


def comments_plain() -> Fixture:
    """A comment written by python-docx, with no commentsExtended part: no replies, unresolved."""
    f = Fixture("comments-plain")
    p = f.doc.add_paragraph("Before ")
    anchor = p.add_run("the anchor")
    p.add_run(" and after.")
    f.doc.add_comment(anchor, text="A comment without commentsExtended.", author="Dee Writer", initials="DW")
    text = "Before the anchor and after."
    f.elements.append({"kind": "paragraph", "text": text, "marks": [{"at": len("Before the anchor"), "kind": "comment", "n": 1}]})
    f.plain_comments.append(
        {
            "author": "Dee Writer",
            "anchor": "the anchor",
            "resolved": False,
            "elements": [{"kind": "paragraph", "text": "A comment without commentsExtended."}],
            "replies": [],
        }
    )
    return f


def media() -> Fixture:
    """Pictures: one on its own line, one between text, the first's bytes again through the same
    part and through a part of its own (one file either way), a metafile, a linked picture, a
    deleted picture, one in a table cell, and one in a second section."""
    f = Fixture("media")
    red, green, blue = png((220, 40, 40)), png((40, 160, 60)), png((40, 60, 200))
    f.heading("Media", 1)
    f.paragraph("A picture on its own line follows.")
    f.tracked([("picture", Picture("A red square", "Red", red))])
    f.tracked([("text", "Text before "), ("picture", Picture("A blue square", "", blue)), ("text", "and after.")])
    f.tracked([("picture", Picture("The red square again", "", red))])
    f.tracked([("picture", Picture("The red square in a part of its own", "", red, part="/word/media/copy-of-red.png"))])
    f.tracked([("picture", Picture("A metafile", "", emf(), "emf", CT.X_EMF, part="/word/media/vector.emf"))])
    f.tracked([("picture", Picture("A linked picture", "Linked", link="https://example.invalid/linked.png"))])
    f.tracked([("text", "A deleted picture follows."), ("delPicture", Picture("Withdrawn picture", "", green))])
    f.tracked_table(
        [
            [("text", "Cell with a picture"), ("picture", Picture("A green square", "", green))],
            [("text", "Plain cell")],
        ]
    )
    f.new_section()
    f.tracked([("picture", Picture("Blue in section two", "", blue))])
    f.absent = ["Withdrawn picture"]
    f.libreoffice = libreoffice_alt
    return f


def charts() -> Fixture:
    """Charts as cached data: a clustered bar chart of two series with a missing value, an
    untitled pie chart, and a line chart."""
    f = Fixture("charts")
    f.heading("Charts", 1)
    f.paragraph("Revenue by region:")
    f.tracked(
        [
            (
                "chart",
                Chart(
                    "bar",
                    "Revenue by region",
                    [("North", ["Q1", "Q2", "Q3"], [12.5, 14, 15.25]), ("South", ["Q1", "Q2", "Q3"], [9, None, 11])],
                ),
            )
        ]
    )
    f.paragraph("Share of costs:")
    f.tracked([("chart", Chart("pie", None, [("Share", ["Staff", "Rent", "Other"], [60, 25, 15])]))])
    f.tracked([("chart", Chart("line", "Trend", [("Index", ["Jan", "Feb", "Mar", "Apr"], [1.5, 2, 2.75, 3.25])]))])
    return f


FIXTURES = [headings, lists, tables, tracked_changes, sections, headers_even_off, notes, comments, comments_plain, media, charts]


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


def main(names: list[str]) -> int:
    if shutil.which("soffice") is None:
        print("generate: soffice (LibreOffice) is not on PATH", file=sys.stderr)
        return 1
    builds = {build().name: build for build in FIXTURES}
    unknown = [name for name in names if name not in builds]
    if unknown:
        print(f"generate: no such fixture: {', '.join(unknown)}; the fixtures are {', '.join(builds)}", file=sys.stderr)
        return 1
    OUT.mkdir(parents=True, exist_ok=True)
    written = []
    for name in names or builds:
        fixture = builds[name]()
        original = OUT / f"{fixture.name}.python-docx.docx"
        resaved = OUT / f"{fixture.name}.libreoffice.docx"
        truth = OUT / f"{fixture.name}.truth.json"
        fixture.finish()
        fixture.doc.save(original)
        resave(original, resaved)
        truth.write_text(json.dumps(fixture.truth(), indent=2, ensure_ascii=False) + "\n")
        written += [original, resaved, truth]
        variant = OUT / f"{fixture.name}.libreoffice.truth.json"
        if fixture.libreoffice is None:
            variant.unlink(missing_ok=True)
        else:
            adjusted = json.loads(json.dumps(fixture.truth()))
            fixture.libreoffice(adjusted)
            variant.write_text(json.dumps(adjusted, indent=2, ensure_ascii=False) + "\n")
            written.append(variant)
    # The checksums cover every fixture's files, regenerated now or not.
    suffixes = ("python-docx.docx", "libreoffice.docx", "truth.json", "libreoffice.truth.json")
    files = [OUT / f"{name}.{suffix}" for name in builds for suffix in suffixes]
    update_readme(soffice_version(), [path for path in files if path.exists()])
    for path in written:
        print(f"{sha256(path)}  {path.relative_to(ROOT)}")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
