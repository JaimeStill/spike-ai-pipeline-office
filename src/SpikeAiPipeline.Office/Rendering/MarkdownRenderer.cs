using System.Text;
using SpikeAiPipeline.Office.Model;

namespace SpikeAiPipeline.Office.Rendering;

/// <summary>The model as GitHub-flavored Markdown: a rendering of the model, never a second
/// reading of the document.</summary>
public static class MarkdownRenderer
{
    /// <summary>The document as Markdown: blocks separated by a blank line, ending in a newline,
    /// or empty when the document has no text.</summary>
    public static string Render(Document document)
    {
        var chunks = RenderBlocks(document.Blocks);
        return chunks.Count == 0 ? "" : string.Join("\n\n", chunks.Select(c => c.Text)) + "\n";
    }

    private readonly record struct Chunk(string Text, bool IsList);

    // Markdown joins two adjacent lists of one kind into one, so a list that follows another of
    // its kind switches markers, '-' to '*' or '.' to ')', which starts a new list.
    private static List<Chunk> RenderBlocks(IReadOnlyList<Block> blocks)
    {
        var chunks = new List<Chunk>();
        bool? previousOrdered = null;
        var alternate = false;
        foreach (var block in blocks)
        {
            if (block is ListBlock list)
            {
                alternate = previousOrdered == list.Ordered && !alternate;
                previousOrdered = list.Ordered;
                chunks.Add(new Chunk(RenderList(list, alternate), true));
                continue;
            }
            previousOrdered = null;
            alternate = false;
            var text = block switch
            {
                Heading heading => RenderHeading(heading),
                Paragraph paragraph => RenderParagraph(paragraph),
                Table table => RenderTable(table),
                _ => "",
            };
            if (text.Length > 0)
            {
                chunks.Add(new Chunk(text, false));
            }
        }
        return chunks;
    }

    private static string RenderHeading(Heading heading)
    {
        var text = string.Join(" ", Lines(InlineText.Of(heading.Inlines)));
        return text.Length == 0 ? "" : new string('#', Math.Clamp(heading.Level, 1, 6)) + " " + text;
    }

    // A line break inside a paragraph is a hard break: a backslash at the end of the line.
    private static string RenderParagraph(Paragraph paragraph) =>
        string.Join("\\\n", Lines(InlineText.Of(paragraph.Inlines)));

    // An item's first paragraph follows its marker; its other blocks are indented to the
    // marker's width, a nested list on the next line, keeping the list tight, anything else after
    // a blank line.
    private static string RenderList(ListBlock list, bool alternate)
    {
        var text = new StringBuilder();
        for (var i = 0; i < list.Items.Count; i++)
        {
            var marker = list.Ordered ? $"{i + 1}{(alternate ? ')' : '.')}" : alternate ? "*" : "-";
            var indent = new string(' ', marker.Length + 1);
            var chunks = RenderBlocks(list.Items[i].Blocks);
            if (i > 0)
            {
                text.Append('\n');
            }
            text.Append(marker);
            var start = 0;
            if (chunks.Count > 0 && !chunks[0].IsList)
            {
                text.Append(' ').Append(Indent(chunks[0].Text, indent, firstLine: false));
                start = 1;
            }
            for (var c = start; c < chunks.Count; c++)
            {
                text.Append(chunks[c].IsList || c == 0 ? "\n" : "\n\n").Append(Indent(chunks[c].Text, indent, firstLine: true));
            }
        }
        return text.ToString();
    }

    private static string Indent(string text, string indent, bool firstLine)
    {
        var lines = text.Split('\n');
        for (var i = firstLine ? 0 : 1; i < lines.Length; i++)
        {
            if (lines[i].Length > 0)
            {
                lines[i] = indent + lines[i];
            }
        }
        return string.Join("\n", lines);
    }

    // A GFM table, its first row the header. GFM has no merged cells, so a merged cell's text sits
    // in its top-left grid position and the positions it covers are empty.
    private static string RenderTable(Table table)
    {
        if (table.Rows.Count == 0)
        {
            return "";
        }
        var grid = new List<string?[]>();
        for (var r = 0; r < table.Rows.Count; r++)
        {
            var column = 0;
            foreach (var cell in table.Rows[r].Cells)
            {
                while (At(grid, r, column) is not null)
                {
                    column++;
                }
                var colSpan = cell.ColSpan ?? 1;
                var rowSpan = Math.Min(cell.RowSpan ?? 1, table.Rows.Count - r);
                for (var dr = 0; dr < rowSpan; dr++)
                {
                    for (var dc = 0; dc < colSpan; dc++)
                    {
                        Set(grid, r + dr, column + dc, dr == 0 && dc == 0 ? CellText(cell.Blocks) : "");
                    }
                }
                column += colSpan;
            }
        }
        while (grid.Count < table.Rows.Count)
        {
            grid.Add([]);
        }
        var columns = grid.Max(row => row.Length);
        if (columns == 0)
        {
            return "";
        }
        var lines = new List<string> { Row(grid[0], columns), "|" + string.Concat(Enumerable.Repeat(" --- |", columns)) };
        lines.AddRange(grid.Skip(1).Select(row => Row(row, columns)));
        return string.Join("\n", lines);
    }

    private static string Row(string?[] cells, int columns) =>
        "|" + string.Concat(Enumerable.Range(0, columns).Select(c => " " + (c < cells.Length ? cells[c] ?? "" : "") + " |"));

    private static string? At(List<string?[]> grid, int row, int column) =>
        row < grid.Count && column < grid[row].Length ? grid[row][column] : null;

    private static void Set(List<string?[]> grid, int row, int column, string value)
    {
        while (grid.Count <= row)
        {
            grid.Add([]);
        }
        if (grid[row].Length <= column)
        {
            var wider = new string?[column + 1];
            grid[row].CopyTo(wider, 0);
            grid[row] = wider;
        }
        grid[row][column] = value;
    }

    // A cell's text on one line, its lines and blocks joined by <br>, the one line break a GFM
    // cell has.
    private static string CellText(IReadOnlyList<Block> blocks)
    {
        var lines = new List<string>();
        CollectLines(blocks, lines);
        return string.Join("<br>", lines);
    }

    private static void CollectLines(IReadOnlyList<Block> blocks, List<string> lines)
    {
        foreach (var block in blocks)
        {
            switch (block)
            {
                case Heading heading:
                    lines.AddRange(Lines(InlineText.Of(heading.Inlines)));
                    break;
                case Paragraph paragraph:
                    lines.AddRange(Lines(InlineText.Of(paragraph.Inlines)));
                    break;
                case ListBlock list:
                    foreach (var item in list.Items)
                    {
                        CollectLines(item.Blocks, lines);
                    }
                    break;
                case Table table:
                    foreach (var cell in table.Rows.SelectMany(row => row.Cells))
                    {
                        CollectLines(cell.Blocks, lines);
                    }
                    break;
                default:
                    break;
            }
        }
    }

    // The non-empty lines of a text, trimmed (leading spaces would make an indented code block)
    // and escaped, tabs as spaces.
    private static IEnumerable<string> Lines(string text) =>
        text.Replace('\t', ' ').Split('\n').Select(line => line.Trim()).Where(line => line.Length > 0).Select(Escape);

    // Backslash-escapes what Markdown would read as markup: emphasis, code, links, HTML,
    // entities, strikethrough, and table pipes anywhere, and at a line's start what would open
    // a heading, a block quote, a list, or a setext underline.
    private static string Escape(string line)
    {
        var text = new StringBuilder(line.Length + 8);
        foreach (var c in line)
        {
            if (c is '\\' or '`' or '*' or '_' or '[' or ']' or '<' or '>' or '~' or '|' or '&')
            {
                text.Append('\\');
            }
            text.Append(c);
        }
        if (text.Length > 0 && text[0] is '#' or '+' or '-' or '=')
        {
            text.Insert(0, '\\');
        }
        var digits = 0;
        while (digits < text.Length && char.IsAsciiDigit(text[digits]))
        {
            digits++;
        }
        if (digits is > 0 and <= 9 && digits < text.Length && text[digits] is '.' or ')')
        {
            text.Insert(digits, '\\');
        }
        return text.ToString();
    }
}
