using System.Text;
using Markdig;
using Markdig.Extensions.Footnotes;
using Markdig.Extensions.Tables;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace SpikeAiPipeline.Office.Baseline;

/// <summary>A list item of a rendering: its first paragraph's text, its nesting level, whether
/// its list is ordered, whether it is its list's first item, and an ordered list's first
/// number.</summary>
internal sealed record RenderedListItem(string Text, int Level, bool Ordered, bool First, int? Start);

/// <summary>What one tool's Markdown holds, as Markdig parses it, the same parse for every tool:
/// CommonMark with GitHub's extensions that change what text reads as: pipe tables, footnotes,
/// and strikethrough (<c>~~struck~~</c>). Each text is its inlines' plain text, normalized as the
/// truth's is (<see cref="Text.Norm"/>): emphasis and code keep their text, a line break or a
/// <c>&lt;br&gt;</c> is a space, an entity its character, and a link its text, but Markdown's
/// reference marks are left out, a footnote reference (<c>[^1]</c>) and a link into the document
/// itself (<c>[[1]](#footnote-1)</c>, <c>[↑](#footnote-ref-1)</c>), as is an image, which is
/// counted instead.</summary>
internal sealed class Rendered
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UsePipeTables()
        .UseFootnotes()
        .UseEmphasisExtras(Markdig.Extensions.EmphasisExtras.EmphasisExtraOptions.Strikethrough)
        .Build();

    public List<(int Level, string Text)> Headings { get; } = [];

    /// <summary>Every paragraph outside a table, but a list item's first, which is the item's
    /// text.</summary>
    public List<string> Paragraphs { get; } = [];
    public List<RenderedListItem> ListItems { get; } = [];

    /// <summary>Each table's rows of cells' comparison keys (<see cref="Text.Key"/>), its header
    /// row first.</summary>
    public List<List<List<string>>> Tables { get; } = [];
    public int Images { get; private set; }

    /// <summary>Every text in reading order: headings, paragraphs (a list item's too), table cells,
    /// and code blocks.</summary>
    public List<string> Leaves { get; } = [];

    /// <summary>For each text of <see cref="Leaves"/>, the indexes of its block's first and last
    /// texts: a table cell's are its table's, any other text's its own.</summary>
    public List<(int First, int Last)> LeafBlocks { get; } = [];

    public static Rendered Of(string markdown)
    {
        var rendered = new Rendered();
        rendered.Blocks(Markdown.Parse(markdown, Pipeline), 0);
        return rendered;
    }

    private void Blocks(ContainerBlock container, int listDepth)
    {
        foreach (var block in container)
        {
            Block(block, listDepth);
        }
    }

    // A list item's blocks after its first paragraph are read at the next list level, so a list
    // nested in it is one level deeper.
    private void Block(Block block, int listDepth)
    {
        switch (block)
        {
            case HeadingBlock heading:
                var headingText = Inlines(heading.Inline);
                Headings.Add((heading.Level, headingText));
                Leaf(headingText);
                break;
            case ParagraphBlock paragraph:
                var text = Inlines(paragraph.Inline);
                Paragraphs.Add(text);
                Leaf(text);
                break;
            case ListBlock list:
                var first = true;
                foreach (var item in list.OfType<ListItemBlock>())
                {
                    var rest = (IEnumerable<Block>)item;
                    if (item.Count > 0 && item[0] is ParagraphBlock itemParagraph)
                    {
                        var itemText = Inlines(itemParagraph.Inline);
                        int? start = list.IsOrdered && int.TryParse(list.OrderedStart, out var n) ? n : list.IsOrdered ? 1 : null;
                        ListItems.Add(new RenderedListItem(itemText, listDepth, list.IsOrdered, first, start));
                        Leaf(itemText);
                        rest = item.Skip(1);
                    }
                    first = false;
                    foreach (var child in rest)
                    {
                        Block(child, listDepth + 1);
                    }
                }
                break;
            case Table table:
                var firstLeaf = Leaves.Count;
                var rows = new List<List<string>>();
                foreach (var row in table.OfType<TableRow>())
                {
                    var cells = new List<string>();
                    foreach (var cell in row.OfType<TableCell>())
                    {
                        var cellText = Text.Norm(string.Join(" ", cell.OfType<LeafBlock>().Select(b => Inlines(b.Inline))));
                        cells.Add(Text.Key(cellText));
                        Leaves.Add(cellText);
                    }
                    rows.Add(cells);
                }
                Tables.Add(rows);
                for (var i = firstLeaf; i < Leaves.Count; i++)
                {
                    LeafBlocks.Add((firstLeaf, Leaves.Count - 1));
                }
                break;
            case CodeBlock code:
                Leaf(Text.Norm(code.Lines.ToString()));
                break;
            case HtmlBlock html:
                Images += Count(html.Lines.ToString(), "<img");
                break;
            case ContainerBlock other:
                // Block quotes, footnote groups, and footnotes: their blocks are the document's.
                Blocks(other, listDepth);
                break;
            default:
                break;
        }
    }

    private void Leaf(string text)
    {
        LeafBlocks.Add((Leaves.Count, Leaves.Count));
        Leaves.Add(text);
    }

    private string Inlines(ContainerInline? inlines)
    {
        var text = new StringBuilder();
        Append(inlines, text);
        return Text.Norm(text.ToString());
    }

    private void Append(Inline? inline, StringBuilder text)
    {
        switch (inline)
        {
            case null:
                return;
            case LiteralInline literal:
                text.Append(literal.Content.ToString());
                break;
            case CodeInline code:
                text.Append(code.Content);
                break;
            case LineBreakInline:
                text.Append(' ');
                break;
            case HtmlEntityInline entity:
                text.Append(entity.Transcoded.ToString());
                break;
            case HtmlInline html:
                if (html.Tag.StartsWith("<br", StringComparison.OrdinalIgnoreCase))
                {
                    text.Append(' ');
                }
                Images += Count(html.Tag, "<img");
                break;
            case AutolinkInline autolink:
                text.Append(autolink.Url);
                break;
            case FootnoteLink:
                break;
            case LinkInline { IsImage: true }:
                Images++;
                break;
            case LinkInline link when link.Url?.StartsWith('#') == true:
                break;
            case ContainerInline container:
                foreach (var child in container)
                {
                    Append(child, text);
                }
                break;
            default:
                break;
        }
    }

    private static int Count(string text, string tag)
    {
        var count = 0;
        for (var i = text.IndexOf(tag, StringComparison.OrdinalIgnoreCase); i >= 0; i = text.IndexOf(tag, i + tag.Length, StringComparison.OrdinalIgnoreCase))
        {
            count++;
        }
        return count;
    }
}
