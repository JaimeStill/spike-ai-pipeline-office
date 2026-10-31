using System.Globalization;
using System.Text.Json.Nodes;

namespace SpikeAiPipeline.Office.Baseline;

/// <summary>A list item of the truth: its text, its nesting level, whether its list is ordered,
/// whether it starts its list, and an ordered list's first number when it isn't 1.</summary>
internal sealed record ExpectedListItem(string Text, int Level, bool Ordered, bool Start, int? StartAt);

/// <summary>A table of the truth as a grid: each cell that holds text at its row and column, with
/// its comparison key (<see cref="Text.Key"/>) and its text for the per-file detail.</summary>
internal sealed record ExpectedTable(Dictionary<(int Row, int Column), (string Key, string Text)> Cells);

/// <summary>What a document's ground truth says a rendering of it holds, element kind by element
/// kind, read from the truth file itself (the truth's shape, <c>fixtures/README.md</c>), never
/// from office's model.</summary>
internal sealed class Expected
{
    public List<(int Level, string Text)> Headings { get; } = [];
    public List<string> Paragraphs { get; } = [];
    public List<ExpectedListItem> ListItems { get; } = [];
    public List<ExpectedTable> Tables { get; } = [];
    public List<string> SheetNames { get; } = [];

    /// <summary>Each slide's first text, in presentation order: its first heading, paragraph, or
    /// list item; a slide with none is left out, as nothing in a rendering can place it.</summary>
    public List<string> SlideAnchors { get; } = [];
    public List<string> SlideNotes { get; } = [];

    /// <summary>Each chart's values, the points its series have, in series order.</summary>
    public List<List<double>> Charts { get; } = [];
    public int Media { get; private set; }

    /// <summary>The distinct texts of every section's headers and footers.</summary>
    public List<string> HeaderFooter { get; } = [];

    /// <summary>The texts of the footnotes, then the endnotes.</summary>
    public List<string> Notes { get; } = [];

    /// <summary>The texts of the comments, each with its replies'.</summary>
    public List<string> Comments { get; } = [];

    public static Expected Of(JsonNode truth)
    {
        var expected = new Expected();
        expected.Body(Items(truth["elements"]));
        // A header or footer that sections share, or that repeats another's elements, is one
        // story, its texts and pictures counted once, as a reader sees it once; a picture's
        // section, which the truth records on each copy, doesn't make a copy another story.
        var stories = new HashSet<string>();
        foreach (var section in Items(truth["sections"]))
        {
            foreach (var story in Items(section["headers"]).Concat(Items(section["footers"])))
            {
                if (stories.Add(StoryKey(story["elements"])))
                {
                    expected.Story(Items(story["elements"]), expected.HeaderFooter, distinct: true);
                }
            }
        }
        foreach (var note in Items(truth["footnotes"]).Concat(Items(truth["endnotes"])))
        {
            expected.Story(Items(note["elements"]), expected.Notes, distinct: false);
        }
        foreach (var comment in Items(truth["comments"]))
        {
            expected.Story(Items(comment["elements"]), expected.Comments, distinct: false);
            foreach (var reply in Items(comment["replies"]))
            {
                expected.Story(Items(reply["elements"]), expected.Comments, distinct: false);
            }
        }
        return expected;
    }

    // The body's elements: a sheet's or slide's elements are the body's too, and the cells of a
    // table count as its cells, not as the paragraphs or list items they hold.
    private void Body(IEnumerable<JsonNode> elements)
    {
        foreach (var element in elements)
        {
            switch (Kind(element))
            {
                case "heading" when Norm(element) is { Length: > 0 } text:
                    Headings.Add(((int)element["level"]!, text));
                    break;
                case "paragraph" when Norm(element) is { Length: > 0 } text:
                    Paragraphs.Add(text);
                    break;
                case "listItem" when Norm(element) is { Length: > 0 } text:
                    ListItems.Add(new ExpectedListItem(text, (int)element["level"]!, (bool)element["ordered"]!, (bool)element["start"]!, (int?)element["startAt"]));
                    break;
                case "table":
                    Table(element);
                    break;
                case "sheet":
                    SheetNames.Add(Text.Norm((string?)element["name"]));
                    Body(Items(element["elements"]));
                    break;
                case "slide":
                    var anchor = Items(element["elements"])
                        .Where(e => Kind(e) is "heading" or "paragraph" or "listItem")
                        .Select(Norm).FirstOrDefault(t => t.Length > 0);
                    if (anchor is not null)
                    {
                        SlideAnchors.Add(anchor);
                    }
                    Body(Items(element["elements"]));
                    Story(Items(element["notes"]), SlideNotes, distinct: false);
                    break;
                case "media":
                    Media++;
                    break;
                case "chart":
                    Chart(element);
                    break;
                default:
                    break;
            }
        }
    }

    // A story's texts, one for each heading, paragraph, list item, and table cell; its pictures
    // and charts count among the document's.
    private void Story(IEnumerable<JsonNode> elements, List<string> texts, bool distinct)
    {
        foreach (var text in Flatten(elements))
        {
            if (!distinct || !texts.Contains(text))
            {
                texts.Add(text);
            }
        }
    }

    // The texts of elements, a table's cells' each, its pictures counted and its charts kept.
    private List<string> Flatten(IEnumerable<JsonNode> elements)
    {
        var texts = new List<string>();
        foreach (var element in elements)
        {
            switch (Kind(element))
            {
                case "heading" or "paragraph" or "listItem" when Norm(element) is { Length: > 0 } text:
                    texts.Add(text);
                    break;
                case "table":
                    foreach (var row in Items(element["rows"]))
                    {
                        foreach (var cell in row is JsonArray cells ? cells.OfType<JsonNode>() : Items(row["cells"]))
                        {
                            if (CellText(cell) is { Length: > 0 } text)
                            {
                                texts.Add(text);
                            }
                        }
                    }
                    break;
                case "media":
                    Media++;
                    break;
                case "chart":
                    Chart(element);
                    if (Text.Norm((string?)element["title"]) is { Length: > 0 } title)
                    {
                        texts.Add(title);
                    }
                    break;
                default:
                    break;
            }
        }
        return texts;
    }

    // A table's grid. A document's table places each cell as office's Markdown does, after the
    // grid columns its row skips and past the positions earlier rows' merges cover; a sheet's
    // places each cell at its reference.
    private void Table(JsonNode table)
    {
        var cells = new Dictionary<(int, int), (string, string)>();
        var rows = Items(table["rows"]).ToList();
        if (rows.Count > 0 && rows[0] is not JsonArray)
        {
            foreach (var row in rows)
            {
                foreach (var cell in Items(row["cells"]))
                {
                    if (CellText(cell) is { Length: > 0 } text)
                    {
                        cells[((int)row["row"]!, ColumnOf((string)cell["ref"]!))] = (Text.Key(text), text);
                    }
                }
            }
            Tables.Add(new ExpectedTable(cells));
            return;
        }
        var taken = new HashSet<(int, int)>();
        for (var r = 0; r < rows.Count; r++)
        {
            var column = 0;
            foreach (var cell in ((JsonArray)rows[r]).OfType<JsonNode>())
            {
                column += (int?)cell["skipBefore"] ?? 0;
                while (taken.Contains((r, column)))
                {
                    column++;
                }
                var colSpan = (int?)cell["colSpan"] ?? 1;
                var rowSpan = Math.Min((int?)cell["rowSpan"] ?? 1, rows.Count - r);
                for (var dr = 0; dr < rowSpan; dr++)
                {
                    for (var dc = 0; dc < colSpan; dc++)
                    {
                        taken.Add((r + dr, column + dc));
                    }
                }
                if (CellText(cell) is { Length: > 0 } text)
                {
                    cells[(r, column)] = (Text.Key(text), text);
                }
                column += colSpan;
            }
        }
        Tables.Add(new ExpectedTable(cells));
    }

    // A document cell's text is its elements' texts joined by spaces, its pictures and charts
    // counted among the document's. A sheet cell's is its value: a number in its shortest form,
    // a boolean as TRUE or FALSE, the rest as their text; with no value, its formula after =, as
    // no tool evaluates formulas; with neither, none.
    private string CellText(JsonNode cell)
    {
        if (cell["elements"] is JsonArray elements)
        {
            return Text.Norm(string.Join(" ", Flatten(elements.OfType<JsonNode>())));
        }
        return cell["value"] switch
        {
            JsonValue value when value.TryGetValue<double>(out var number) => number.ToString("R", CultureInfo.InvariantCulture),
            JsonValue value when value.TryGetValue<bool>(out var boolean) => boolean ? "TRUE" : "FALSE",
            JsonValue value => Text.Norm(value.ToString()),
            _ => cell["formula"] is { } formula ? Text.Norm("=" + (string?)formula) : "",
        };
    }

    private void Chart(JsonNode chart)
    {
        var values = new List<double>();
        foreach (var series in Items(chart["series"]))
        {
            foreach (var value in Items(series["values"]))
            {
                if (value is JsonValue v && v.TryGetValue<double>(out var number))
                {
                    values.Add(number);
                }
            }
        }
        Charts.Add(values);
    }

    // A cell reference's column, from 0: A is 0, Z 25, AA 26.
    private static int ColumnOf(string reference)
    {
        var column = 0;
        foreach (var c in reference.TakeWhile(char.IsAsciiLetter))
        {
            column = (column * 26) + (char.ToUpperInvariant(c) - 'A' + 1);
        }
        return column - 1;
    }

    private static string StoryKey(JsonNode? elements)
    {
        var copy = elements?.DeepClone();
        foreach (var media in Items(copy).Where(e => Kind(e) == "media"))
        {
            media.AsObject().Remove("section");
        }
        return copy?.ToJsonString() ?? "";
    }

    private static string Kind(JsonNode element) => (string?)element["kind"] ?? "";

    private static string Norm(JsonNode element) => Text.Norm((string?)element["text"]);

    private static IEnumerable<JsonNode> Items(JsonNode? array) => array is JsonArray items ? items.OfType<JsonNode>() : [];
}
