using System.Text.Json.Nodes;
using SpikeAiPipeline.Office.Model;

namespace SpikeAiPipeline.Office.Tests;

/// <summary>A model in the ground truth's shape, element by element, so the two compare
/// directly: headings, paragraphs, list items with their nesting level and whether each starts its
/// list, and tables with each cell's paragraphs joined by a newline.</summary>
internal static class GroundTruth
{
    public static List<JsonObject> Elements(Document document)
    {
        var elements = new List<JsonObject>();
        AddBlocks(document.Blocks, elements);
        return elements;
    }

    private static void AddBlocks(IReadOnlyList<Block> blocks, List<JsonObject> elements, int listLevel = 0)
    {
        foreach (var block in blocks)
        {
            switch (block)
            {
                case Heading heading:
                    elements.Add(new JsonObject { ["kind"] = "heading", ["level"] = heading.Level, ["text"] = InlineText.Of(heading.Inlines) });
                    break;
                case Paragraph paragraph:
                    elements.Add(new JsonObject { ["kind"] = "paragraph", ["text"] = InlineText.Of(paragraph.Inlines) });
                    break;
                case ListBlock list:
                    for (var i = 0; i < list.Items.Count; i++)
                    {
                        var item = list.Items[i];
                        var rest = item.Blocks;
                        if (item.Blocks is [Paragraph first, ..])
                        {
                            elements.Add(new JsonObject
                            {
                                ["kind"] = "listItem",
                                ["level"] = listLevel,
                                ["ordered"] = list.Ordered,
                                ["start"] = i == 0,
                                ["text"] = InlineText.Of(first.Inlines),
                            });
                            rest = [.. item.Blocks.Skip(1)];
                        }
                        AddBlocks(rest, elements, listLevel + 1);
                    }
                    break;
                case Table table:
                    var rows = new JsonArray();
                    foreach (var row in table.Rows)
                    {
                        var cells = new JsonArray();
                        foreach (var cell in row.Cells)
                        {
                            cells.Add(new JsonObject
                            {
                                ["text"] = string.Join("\n", cell.Blocks.OfType<Paragraph>().Select(p => InlineText.Of(p.Inlines))),
                                ["colSpan"] = cell.ColSpan ?? 1,
                                ["rowSpan"] = cell.RowSpan ?? 1,
                            });
                        }
                        rows.Add(cells);
                    }
                    elements.Add(new JsonObject { ["kind"] = "table", ["rows"] = rows });
                    break;
                default:
                    elements.Add(new JsonObject { ["kind"] = block.GetType().Name });
                    break;
            }
        }
    }
}
