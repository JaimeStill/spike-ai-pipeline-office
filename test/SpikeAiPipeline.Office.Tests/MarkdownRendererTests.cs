using SpikeAiPipeline.Office.Model;
using SpikeAiPipeline.Office.Rendering;

namespace SpikeAiPipeline.Office.Tests;

// The Markdown renderer's seam, independent of the reader: hand-built models against Markdown
// written by hand from GFM's rules, never a snapshot of office's own output.
public sealed class MarkdownRendererTests
{
    [Fact]
    public void TextMarkdownWouldMisreadIsEscaped()
    {
        var markdown = Render(
            Para("Stars *a* and _b_, [link](x), <tag>, a | pipe, & amp, ~strike~, `code`, back\\slash"),
            Para("# not a heading"),
            Para("1. not a list"),
            Para("- not a bullet"),
            Para("  indented text\twith a tab"),
            Para("line one\nline two"));

        Assert.Equal(
            """
            Stars \*a\* and \_b\_, \[link\](x), \<tag\>, a \| pipe, \& amp, \~strike\~, \`code\`, back\\slash

            \# not a heading

            1\. not a list

            \- not a bullet

            indented text with a tab

            line one\
            line two

            """,
            markdown);
    }

    [Fact]
    public void AdjacentListsOfOneKindSwitchMarkers()
    {
        var markdown = Render(
            List(false, "a"),
            List(false, "b"),
            List(false, "c"),
            List(true, "d"),
            List(true, "e"),
            Para("text"),
            List(false, "f"));

        Assert.Equal(
            """
            - a

            * b

            - c

            1. d

            1) e

            text

            - f

            """,
            markdown);
    }

    [Fact]
    public void AMergedCellsTextIsInItsTopLeftPositionAndTheRestAreEmpty()
    {
        var markdown = Render(new Table(
        [
            new TableRow([new TableCell([Para("A")]) { ColSpan = 2 }, new TableCell([Para("C")])]),
            new TableRow([new TableCell([Para("D")]) { RowSpan = 2 }, new TableCell([Para("E")]), new TableCell([Para("F")])]),
            new TableRow([new TableCell([Para("G")]), new TableCell([Para("H")])]),
        ]));

        Assert.Equal(
            """
            | A |  | C |
            | --- | --- | --- |
            | D | E | F |
            |  | G | H |

            """,
            markdown);
    }

    // A footnote and an endnote share id 1 in the document; their labels count both kinds in
    // reference order, so they never collide, and a second reference reuses its note's label.
    [Fact]
    public void NotesAreFootnotesNumberedInReferenceOrder()
    {
        var document = Doc(
            new Paragraph([new TextRun("Alpha"), new FootnoteReference(1), new TextRun(" beta"), new EndnoteReference(1), new TextRun(".")]),
            new Paragraph([new TextRun("Again"), new FootnoteReference(1), new TextRun(".")])) with
        {
            Footnotes = [new Note(1, [Para(" First paragraph."), Para("Second paragraph.")])],
            Endnotes = [new Note(1, [Para("End note.")])],
        };

        Assert.Equal(
            """
            Alpha[^1] beta[^2].

            Again[^1].

            [^1]: First paragraph.

                Second paragraph.

            [^2]: End note.

            """,
            MarkdownRenderer.Render(document));
    }

    [Fact]
    public void CommentsAreFootnotesAfterTheirAnchorWithAuthorStateAndReplies()
    {
        var document = Doc(new Paragraph([new TextRun("The anchor"), new CommentReference(7), new TextRun(" text.")])) with
        {
            Comments =
            [
                new Comment(7, "Ana", "anchor", false, [Para("Check this.")], [new CommentReply(8, "Ben", [Para("Done.")])]),
                new Comment(9, null, "", true, [Para("Unanchored.")], []),
            ],
        };

        Assert.Equal(
            """
            The anchor[^c1] text.

            [^c1]: Comment by Ana (open) on "anchor": Check this.

                Reply by Ben: Done.

            [^c2]: Comment by an unknown author (resolved): Unanchored.

            """,
            MarkdownRenderer.Render(document));
    }

    [Fact]
    public void EachDistinctHeaderComesFirstAndEachDistinctFooterLast()
    {
        var document = Doc(Para("Body")) with
        {
            Sections =
            [
                new Section(
                    [new HeaderFooter(HeaderFooterKind.Default, [Para("Top")]), new HeaderFooter(HeaderFooterKind.First, [Para("First top")])],
                    [new HeaderFooter(HeaderFooterKind.Default, [Para("Bottom")])]),
                new Section(
                    [new HeaderFooter(HeaderFooterKind.Default, [Para("Top")])],
                    [new HeaderFooter(HeaderFooterKind.Default, [Para("Bottom 2")]), new HeaderFooter(HeaderFooterKind.Even, [Para("Bottom")])]),
            ],
        };

        Assert.Equal(
            """
            Top

            First top

            Body

            Bottom

            Bottom 2

            """,
            MarkdownRenderer.Render(document));
    }

    [Fact]
    public void AChartIsItsTitleAndATableOfItsSeries()
    {
        var markdown = Render(
            new Chart("bar", "Sales", [new ChartSeries("North", ["Q1", "Q2"], [1.5, null]), new ChartSeries("South", ["Q1", "Q2"], [2, 3])]),
            new Chart("pie", null, [new ChartSeries(null, [], [10, 20])]));

        Assert.Equal(
            """
            Sales

            |  | North | South |
            | --- | --- | --- |
            | Q1 | 1.5 | 2 |
            | Q2 |  | 3 |

            |  |  |
            | --- | --- |
            |  | 10 |
            |  | 20 |

            """,
            markdown);
    }

    [Fact]
    public void MediaAreImageLinksToTheirFilesOrTargets()
    {
        var markdown = Render(
            new Media { Name = "abc.png", ContentType = "image/png", Alt = "A [bracketed] picture", Title = "Unused" },
            new Media { Target = "https://example.com/a b.png", Title = "Titled" },
            new Table([new TableRow([new TableCell([Para("Cell"), new Media { Name = "x.emf" }])])]));

        Assert.Equal(
            """
            ![A \[bracketed\] picture](abc.png)

            ![Titled](<https://example.com/a b.png>)

            | Cell<br>![](x.emf) |
            | --- |

            """,
            markdown);
    }

    private static Paragraph Para(string text) => new([new TextRun(text)]);

    private static ListBlock List(bool ordered, string text) => new(ordered, [new ListItem([Para(text)])]);

    private static Document Doc(params Block[] blocks) => new(Document.CurrentSchema, SourceFormat.Docx, blocks);

    private static string Render(params Block[] blocks) => MarkdownRenderer.Render(Doc(blocks));
}
