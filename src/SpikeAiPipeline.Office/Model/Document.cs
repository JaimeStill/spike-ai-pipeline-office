using System.Text.Json.Serialization;

namespace SpikeAiPipeline.Office.Model;

// The document model: one tree for docx, xlsx, and pptx, and office's versioned output contract.
// A document is an ordered list of blocks; a block's "type" names its kind in the JSON. Every
// block but a media block is the document's text, complete without inference.
//
// The kinds this schema version holds are heading, paragraph, list, and table. The model grows
// by adding kinds and optional properties, never by reshaping these:
// - chart (type, title, series of name, categories, values) and media (name relative to the
//   media directory, content type, alt text, source part path) are further Block kinds, placed
//   where they occur in the section, sheet, or slide that holds them;
// - sheet and slide are Block kinds that hold their own blocks (a slide's notes beside them);
// - a docx section's headers and footers (kind default, first, or even, each holding blocks),
//   its footnotes and endnotes, and its comments (text, author, anchored range, replies,
//   resolved) are optional Document properties, referred to from the text by Inline kinds.

/// <summary>The formats office reads; the JSON names them by their file extension.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<SourceFormat>))]
public enum SourceFormat
{
    /// <summary>A WordprocessingML package.</summary>
    [JsonStringEnumMemberName("docx")]
    Docx,

    /// <summary>A SpreadsheetML package.</summary>
    [JsonStringEnumMemberName("xlsx")]
    Xlsx,

    /// <summary>A PresentationML package.</summary>
    [JsonStringEnumMemberName("pptx")]
    Pptx,
}

/// <summary>A converted document: its schema version, its source format, and its blocks in
/// document order.</summary>
public sealed record Document(string Schema, SourceFormat Format, IReadOnlyList<Block> Blocks)
{
    /// <summary>The schema version this build writes. It changes whenever the JSON's shape does,
    /// so a reader can tell which contract a document follows.</summary>
    public const string CurrentSchema = "1";
}

/// <summary>A block of the document, in document order.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(Heading), "heading")]
[JsonDerivedType(typeof(Paragraph), "paragraph")]
[JsonDerivedType(typeof(ListBlock), "list")]
[JsonDerivedType(typeof(Table), "table")]
public abstract record Block;

/// <summary>A heading, level 1 the outermost.</summary>
public sealed record Heading(int Level, IReadOnlyList<Inline> Inlines) : Block;

/// <summary>A paragraph of body text.</summary>
public sealed record Paragraph(IReadOnlyList<Inline> Inlines) : Block;

/// <summary>A list, ordered or not. A nested list is a block of the item it belongs to.</summary>
public sealed record ListBlock(bool Ordered, IReadOnlyList<ListItem> Items) : Block;

/// <summary>A list item: its own blocks, usually a paragraph, then any nested list.</summary>
public sealed record ListItem(IReadOnlyList<Block> Blocks);

/// <summary>A table: rows of cells, the first row first.</summary>
public sealed record Table(IReadOnlyList<TableRow> Rows) : Block;

/// <summary>A table row. A grid position covered by a merged cell from the left or above has
/// no cell of its own, so a row can hold fewer cells than the table has columns.</summary>
public sealed record TableRow(IReadOnlyList<TableCell> Cells);

/// <summary>A table cell and the blocks it holds. A merged cell spans more than one column or
/// row; a span of one is left out of the JSON.</summary>
public sealed record TableCell(IReadOnlyList<Block> Blocks)
{
    /// <summary>The columns the cell spans, or null for one.</summary>
    public int? ColSpan { get; init; }

    /// <summary>The rows the cell spans, or null for one.</summary>
    public int? RowSpan { get; init; }
}

/// <summary>A piece of a block's text. Text is the only kind in this schema version; note and
/// comment references join it as kinds of their own, at their position in the text.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(TextRun), "text")]
public abstract record Inline;

/// <summary>A run of text, with tabs as <c>\t</c> and line breaks as <c>\n</c>. Adjacent runs
/// are merged, so a block's text is one run between other inlines.</summary>
public sealed record TextRun(string Text) : Inline;
