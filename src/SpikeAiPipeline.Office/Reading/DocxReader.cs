using System.Text;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using SpikeAiPipeline.Office.Model;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace SpikeAiPipeline.Office.Reading;

/// <summary>Reads a docx package's body into the model, its tracked changes accepted.</summary>
internal static class DocxReader
{
    public static Document Read(WordprocessingDocument package)
    {
        var main = package.MainDocumentPart ?? throw new OpenXmlPackageException("the package has no main document part");
        var body = main.Document?.Body ?? throw new OpenXmlPackageException("the main document part has no body");
        var reader = new BodyReader(
            new Styles(main.StyleDefinitionsPart?.Styles),
            new Numbering(main.NumberingDefinitionsPart?.Numbering));
        return new Document(Document.CurrentSchema, SourceFormat.Docx, reader.ReadBlocks(body));
    }

    private sealed class BodyReader(Styles styles, Numbering numbering)
    {
        // The blocks of a body or a table cell. Tracked changes are read in their accepted state:
        // a paragraph whose mark was deleted (or moved away) runs on into the next paragraph,
        // which keeps its own properties, as the mark that survives is the next one's.
        public List<Block> ReadBlocks(OpenXmlElement container)
        {
            var blocks = new BlockList();
            InlineBuilder? carried = null;
            foreach (var element in BlockElements(container))
            {
                if (element is W.Table table)
                {
                    blocks.Add(ReadTable(table));
                    continue;
                }
                var paragraph = (W.Paragraph)element;
                var inlines = carried ?? new InlineBuilder();
                carried = null;
                ReadInlines(paragraph, inlines);
                if (MarkRemoved(paragraph))
                {
                    carried = inlines;
                    continue;
                }
                AddParagraph(blocks, paragraph, inlines);
            }
            if (carried is { IsBlank: false })
            {
                blocks.Add(new Paragraph(carried.Build()));
            }
            return blocks.Build();
        }

        private void AddParagraph(BlockList blocks, W.Paragraph paragraph, InlineBuilder inlines)
        {
            var properties = paragraph.ParagraphProperties;
            var styleId = properties?.ParagraphStyleId?.Val?.Value;
            if (ListLevel(properties, styleId) is var (numId, level) && numbering.Resolve(numId, level, styles) is { } numberingLevel)
            {
                // A list item is kept even when empty: it is still an item of its list.
                blocks.AddItem(level, numId, numberingLevel, new Paragraph(inlines.Build()));
                return;
            }
            // Empty paragraphs and headings are spacing, not text.
            if (inlines.IsBlank)
            {
                return;
            }
            blocks.Add(HeadingLevel(properties, styleId) is int headingLevel
                ? new Heading(headingLevel, inlines.Build())
                : new Paragraph(inlines.Build()));
        }

        // The level a paragraph's own outline level gives it, or else its style's: an outline level
        // in the style chain, or a style named "heading N". Outline level 9 is body text.
        private int? HeadingLevel(W.ParagraphProperties? properties, string? styleId)
        {
            if (properties?.OutlineLevel?.Val?.Value is int own)
            {
                return own < 9 ? own + 1 : null;
            }
            foreach (var style in styles.Chain(styleId))
            {
                if (style.StyleParagraphProperties?.OutlineLevel?.Val?.Value is int outline)
                {
                    return outline < 9 ? outline + 1 : null;
                }
                var name = style.StyleName?.Val?.Value;
                if (name is not null
                    && name.StartsWith("heading ", StringComparison.OrdinalIgnoreCase)
                    && int.TryParse(name.AsSpan("heading ".Length), out var level)
                    && level is >= 1 and <= 9)
                {
                    return level;
                }
            }
            return null;
        }

        // The numbering instance and level of a list paragraph, from its own w:numPr or its
        // style's. Numbering instance 0 removes numbering.
        private (int NumId, int Level)? ListLevel(W.ParagraphProperties? properties, string? styleId)
        {
            var own = properties?.NumberingProperties;
            int? numId = own?.NumberingId?.Val?.Value;
            int? level = own?.NumberingLevelReference?.Val?.Value;
            if (numId is null)
            {
                foreach (var style in styles.Chain(styleId))
                {
                    if (style.StyleParagraphProperties?.NumberingProperties is { NumberingId.Val.Value: var styleNumId } styleNumbering)
                    {
                        numId = styleNumId;
                        level ??= styleNumbering.NumberingLevelReference?.Val?.Value;
                        break;
                    }
                }
            }
            return numId is null or 0 ? null : (numId.Value, level ?? 0);
        }

        // A table's rows and cells. A horizontal merge is a cell's w:gridSpan; a vertical merge is
        // a w:vMerge restart cell and the continue cells below it, which fold into its row span.
        private Table ReadTable(W.Table table)
        {
            var rows = new List<List<CellBuilder>>();
            var mergeAnchors = new Dictionary<int, CellBuilder>();
            foreach (var row in table.Elements<W.TableRow>())
            {
                var cells = new List<CellBuilder>();
                var column = row.TableRowProperties?.GetFirstChild<W.GridBefore>()?.Val?.Value ?? 0;
                foreach (var cell in row.Elements<W.TableCell>())
                {
                    var cellProperties = cell.TableCellProperties;
                    var span = cellProperties?.GridSpan?.Val?.Value ?? 1;
                    var merge = cellProperties?.VerticalMerge;
                    var continues = merge is not null && (merge.Val is null || merge.Val.Value == W.MergedCellValues.Continue);
                    if (continues && mergeAnchors.TryGetValue(column, out var anchor))
                    {
                        anchor.RowSpan++;
                    }
                    else
                    {
                        var builder = new CellBuilder(ReadBlocks(cell), span);
                        cells.Add(builder);
                        if (merge?.Val?.Value == W.MergedCellValues.Restart)
                        {
                            mergeAnchors[column] = builder;
                        }
                        else
                        {
                            mergeAnchors.Remove(column);
                        }
                    }
                    column += span;
                }
                rows.Add(cells);
            }
            return new Table([.. rows.Select(cells => new TableRow([.. cells.Select(c => c.Build())]))]);
        }
    }

    // The paragraphs and tables of a container in document order, through the content controls
    // and custom XML that can wrap them.
    private static IEnumerable<OpenXmlElement> BlockElements(OpenXmlElement container)
    {
        foreach (var child in container.ChildElements)
        {
            switch (child)
            {
                case W.Paragraph or W.Table:
                    yield return child;
                    break;
                case W.SdtBlock { SdtContentBlock: { } content }:
                    foreach (var element in BlockElements(content))
                    {
                        yield return element;
                    }
                    break;
                case W.CustomXmlBlock custom:
                    foreach (var element in BlockElements(custom))
                    {
                        yield return element;
                    }
                    break;
                default:
                    break;
            }
        }
    }

    private static bool MarkRemoved(W.Paragraph paragraph) =>
        paragraph.ParagraphProperties?.ParagraphMarkRunProperties is { } mark
        && (mark.GetFirstChild<W.Deleted>() is not null || mark.GetFirstChild<W.MoveFrom>() is not null);

    // A paragraph's text in its accepted state. The runs are walked explicitly: the SDK's
    // InnerText includes w:delText, and every w:t would include a move's source, so insertions
    // and a move's destination are read through, deletions and a move's source skipped.
    private static void ReadInlines(OpenXmlElement container, InlineBuilder inlines)
    {
        foreach (var child in container.ChildElements)
        {
            switch (child)
            {
                case W.Run run:
                    ReadRun(run, inlines);
                    break;
                case W.DeletedRun or W.MoveFromRun:
                    break;
                case W.InsertedRun or W.MoveToRun or W.Hyperlink or W.SimpleField or W.CustomXmlRun:
                    ReadInlines(child, inlines);
                    break;
                case W.SdtRun { SdtContentRun: { } content }:
                    ReadInlines(content, inlines);
                    break;
                default:
                    break;
            }
        }
    }

    // A run's text. Field codes are instructions, not text; a field's result is ordinary runs.
    private static void ReadRun(W.Run run, InlineBuilder inlines)
    {
        foreach (var child in run.ChildElements)
        {
            switch (child)
            {
                case W.Text text:
                    inlines.Append(text.Text);
                    break;
                case W.TabChar or W.PositionalTab:
                    inlines.Append("\t");
                    break;
                case W.Break or W.CarriageReturn:
                    inlines.Append("\n");
                    break;
                case W.NoBreakHyphen:
                    inlines.Append("-");
                    break;
                default:
                    break;
            }
        }
    }

    /// <summary>A block's inlines as they are read, adjacent text merged into one run.</summary>
    private sealed class InlineBuilder
    {
        private readonly List<Inline> inlines = [];
        private readonly StringBuilder text = new();

        public bool IsBlank => inlines.Count == 0 && string.IsNullOrWhiteSpace(text.ToString());

        public void Append(string value) => text.Append(value);

        public List<Inline> Build()
        {
            var result = new List<Inline>(inlines);
            if (text.Length > 0)
            {
                result.Add(new TextRun(text.ToString()));
            }
            return result;
        }
    }

    private sealed class CellBuilder(IReadOnlyList<Block> blocks, int colSpan)
    {
        public int RowSpan { get; set; } = 1;

        public TableCell Build() => new(blocks)
        {
            ColSpan = colSpan > 1 ? colSpan : null,
            RowSpan = RowSpan > 1 ? RowSpan : null,
        };
    }

    // A container's blocks as they are read. Consecutive list paragraphs make up lists: a deeper
    // level opens a list inside the last item, and at the same level an item continues the list
    // unless its level is ordered where the list's isn't (or the reverse), it belongs to another
    // abstract numbering definition, or it restarts the count, which is a change to a numbering
    // instance that overrides the level's start. Instances of one abstract definition otherwise
    // share its count, so they continue one list, as Word numbers them.
    private sealed class BlockList
    {
        private readonly List<Block> blocks = [];
        private readonly List<OpenList> open = [];

        public void Add(Block block)
        {
            while (open.Count > 0)
            {
                Close();
            }
            blocks.Add(block);
        }

        public void AddItem(int level, int numId, NumberingLevel numbering, Paragraph paragraph)
        {
            while (open.Count > 0 && open[^1].Level > level)
            {
                Close();
            }
            if (open.Count > 0 && open[^1] is var top && top.Level == level
                && (top.Ordered != numbering.Ordered
                    || top.AbstractId != numbering.AbstractId
                    || (numbering.Restarts && top.NumId != numId)))
            {
                Close();
            }
            if (open.Count == 0 || open[^1].Level < level)
            {
                open.Add(new OpenList(level, numbering.Ordered, numbering.AbstractId));
            }
            open[^1].NumId = numId;
            open[^1].Items.Add([paragraph]);
        }

        public List<Block> Build()
        {
            while (open.Count > 0)
            {
                Close();
            }
            return blocks;
        }

        private void Close()
        {
            var list = open[^1];
            open.RemoveAt(open.Count - 1);
            var block = new ListBlock(list.Ordered, [.. list.Items.Select(item => new ListItem(item))]);
            if (open.Count > 0)
            {
                open[^1].Items[^1].Add(block);
            }
            else
            {
                blocks.Add(block);
            }
        }

        private sealed record OpenList(int Level, bool Ordered, int AbstractId)
        {
            // The numbering instance of the list's last item.
            public int NumId { get; set; }

            public List<List<Block>> Items { get; } = [];
        }
    }

    /// <summary>What a list paragraph's numbering level says about its list.</summary>
    /// <param name="Ordered">Whether the level counts.</param>
    /// <param name="AbstractId">The abstract numbering definition the level belongs to.</param>
    /// <param name="Restarts">Whether the numbering instance overrides the level's start.</param>
    private readonly record struct NumberingLevel(bool Ordered, int AbstractId, bool Restarts);

    /// <summary>The paragraph styles, by id, with their basedOn chains.</summary>
    private sealed class Styles
    {
        private readonly Dictionary<string, W.Style> byId = [];
        private readonly Dictionary<string, W.Style> anyType = [];
        private readonly string? defaultParagraph;

        public Styles(W.Styles? styles)
        {
            foreach (var style in styles?.Elements<W.Style>() ?? [])
            {
                if (style.StyleId?.Value is not { } id)
                {
                    continue;
                }
                anyType.TryAdd(id, style);
                if (style.Type?.Value == W.StyleValues.Paragraph)
                {
                    byId.TryAdd(id, style);
                    if (style.Default?.Value == true)
                    {
                        defaultParagraph ??= id;
                    }
                }
            }
        }

        // A style of any type by id, numbering styles among them.
        public W.Style? Find(string id) => anyType.GetValueOrDefault(id);

        // The style and the styles it is based on, nearest first; a paragraph with no style has
        // the default paragraph style. A cycle ends the chain.
        public IEnumerable<W.Style> Chain(string? styleId)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var id = styleId ?? defaultParagraph;
            while (id is not null && seen.Add(id) && byId.TryGetValue(id, out var style))
            {
                yield return style;
                id = style.BasedOn?.Val?.Value;
            }
        }
    }

    /// <summary>The numbering part's instances and abstract definitions.</summary>
    private sealed class Numbering
    {
        private readonly Dictionary<int, W.NumberingInstance> instances = [];
        private readonly Dictionary<int, W.AbstractNum> abstracts = [];

        public Numbering(W.Numbering? numbering)
        {
            foreach (var instance in numbering?.Elements<W.NumberingInstance>() ?? [])
            {
                if (instance.NumberID?.Value is int id)
                {
                    instances.TryAdd(id, instance);
                }
            }
            foreach (var definition in numbering?.Elements<W.AbstractNum>() ?? [])
            {
                if (definition.AbstractNumberId?.Value is int id)
                {
                    abstracts.TryAdd(id, definition);
                }
            }
        }

        // The level of a numbering instance, or null when the instance or its definition doesn't
        // exist, which leaves the paragraph out of any list. A definition with only a numStyleLink
        // takes its levels from the numbering its style names.
        public NumberingLevel? Resolve(int numId, int level, Styles styles) => Resolve(numId, level, styles, 0, false);

        private NumberingLevel? Resolve(int numId, int level, Styles styles, int depth, bool restarts)
        {
            if (depth > 8 || !instances.TryGetValue(numId, out var instance))
            {
                return null;
            }
            var levelOverride = instance.Elements<W.LevelOverride>().FirstOrDefault(o => o.LevelIndex?.Value == level);
            restarts |= levelOverride?.StartOverrideNumberingValue is not null;
            if (instance.AbstractNumId?.Val?.Value is not int abstractId || !abstracts.TryGetValue(abstractId, out var definition))
            {
                return null;
            }
            var levels = definition.Elements<W.Level>().ToList();
            if (levels.Count == 0
                && definition.NumberingStyleLink?.Val?.Value is { } link
                && styles.Find(link)?.StyleParagraphProperties?.NumberingProperties?.NumberingId?.Val?.Value is int linked)
            {
                return Resolve(linked, level, styles, depth + 1, restarts);
            }
            var definitionLevel = levelOverride?.GetFirstChild<W.Level>() ?? levels.FirstOrDefault(l => l.LevelIndex?.Value == level);
            return new NumberingLevel(IsOrdered(definitionLevel), abstractId, restarts);
        }

        // A level formatted as a bullet or as nothing is unordered, as is a level the definition
        // lacks; a level without a format is decimal, so ordered.
        private static bool IsOrdered(W.Level? level)
        {
            if (level is null)
            {
                return false;
            }
            var format = level.NumberingFormat?.Val?.Value;
            return format is null || (format != W.NumberFormatValues.Bullet && format != W.NumberFormatValues.None);
        }
    }
}
