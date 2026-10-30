using System.Text.Json.Serialization;

namespace SpikeAiPipeline.Office.Model;

// The document model: one tree for docx, xlsx, and pptx, and office's versioned output contract.
// A document is an ordered list of blocks; a block's "type" names its kind in the JSON. Every
// block but a media block is the document's text, complete without inference.
//
// The kinds this schema version holds are heading, paragraph, list, table, chart, and media; a
// docx document also holds its sections' headers and footers, its footnotes and endnotes, and
// its comments, referred to from the text by the footnote, endnote, and comment inlines. The
// model grows by adding kinds and optional properties, never by reshaping these: sheet and slide
// are Block kinds to come, holding their own blocks (a slide's notes beside them), and a media
// block's sheet or slide joins its section.

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
/// document order. The other parts of a document are left out when it has none.</summary>
public sealed record Document(string Schema, SourceFormat Format, IReadOnlyList<Block> Blocks)
{
    /// <summary>The schema version this build writes. It changes whenever the JSON's shape does,
    /// so a reader can tell which contract a document follows.</summary>
    public const string CurrentSchema = "1";

    /// <summary>A docx document's sections in body order, each with the headers and footers it
    /// shows, or null when no section has any. A media block's section is an index into the
    /// document's sections, which it counts even when this is null.</summary>
    public IReadOnlyList<Section>? Sections { get; init; }

    /// <summary>The footnotes the text references, in the order it first references them.</summary>
    public IReadOnlyList<Note>? Footnotes { get; init; }

    /// <summary>The endnotes the text references, in the order it first references them.</summary>
    public IReadOnlyList<Note>? Endnotes { get; init; }

    /// <summary>The comments, in the order the document stores them, each with its replies.</summary>
    public IReadOnlyList<Comment>? Comments { get; init; }
}

/// <summary>A docx section's headers and footers, as its pages show them: its own, or the
/// previous section's where it has none of a kind. A first-page one is kept only when the
/// section has a title page, an even-page one only when the document turns even-page headers on,
/// and one with no blocks is left out.</summary>
public sealed record Section(IReadOnlyList<HeaderFooter>? Headers, IReadOnlyList<HeaderFooter>? Footers);

/// <summary>A header or footer and its blocks.</summary>
public sealed record HeaderFooter(HeaderFooterKind Kind, IReadOnlyList<Block> Blocks);

/// <summary>The pages a header or footer is shown on.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<HeaderFooterKind>))]
public enum HeaderFooterKind
{
    /// <summary>Every page the other kinds don't cover; with even-page headers, the odd pages.</summary>
    [JsonStringEnumMemberName("default")]
    Default,

    /// <summary>The section's first page.</summary>
    [JsonStringEnumMemberName("first")]
    First,

    /// <summary>The even pages.</summary>
    [JsonStringEnumMemberName("even")]
    Even,
}

/// <summary>A footnote or endnote: its id in the document, which its references give, and its
/// blocks.</summary>
public sealed record Note(int Id, IReadOnlyList<Block> Blocks);

/// <summary>A comment: its id, which its reference in the text gives, its author, the text it is
/// anchored to (the accepted text of its range, paragraphs joined by a newline), whether its
/// thread is resolved, its blocks, and its replies in order.</summary>
public sealed record Comment(int Id, string? Author, string Anchor, bool Resolved, IReadOnlyList<Block> Blocks, IReadOnlyList<CommentReply> Replies);

/// <summary>A reply in a comment's thread.</summary>
public sealed record CommentReply(int Id, string? Author, IReadOnlyList<Block> Blocks);

/// <summary>A block of the document, in document order.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(Heading), "heading")]
[JsonDerivedType(typeof(Paragraph), "paragraph")]
[JsonDerivedType(typeof(ListBlock), "list")]
[JsonDerivedType(typeof(Table), "table")]
[JsonDerivedType(typeof(Chart), "chart")]
[JsonDerivedType(typeof(Media), "media")]
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

/// <summary>A chart, from the data its chart part caches, never an image of it: its type (the
/// plot's kind, such as bar, line, or pie; the first plot's when it has several), its title, and
/// its series.</summary>
public sealed record Chart(string ChartType, string? Title, IReadOnlyList<ChartSeries> Series) : Block;

/// <summary>A chart series: its name, its categories (or x values), and its values, a point the
/// cache lacks null. The two lists are as long as the cache says.</summary>
public sealed record ChartSeries(string? Name, IReadOnlyList<string?> Categories, IReadOnlyList<double?> Values);

/// <summary>A picture. An embedded one has its file's name in the media directory (the
/// lowercase hex SHA-256 of its bytes and its stored extension, the same for every block that
/// shows those bytes), its content type, and the package part it is stored in; a linked one has
/// its target instead, and no file. Its alternative text and title are its drawing's.</summary>
public sealed record Media : Block
{
    /// <summary>The file's name relative to the media directory, or null for a linked picture.</summary>
    public string? Name { get; init; }

    /// <summary>The stored content type, or null for a linked picture.</summary>
    public string? ContentType { get; init; }

    /// <summary>The alternative text, the drawing's description.</summary>
    public string? Alt { get; init; }

    /// <summary>The drawing's title.</summary>
    public string? Title { get; init; }

    /// <summary>The part the picture is stored in, or null for a linked picture.</summary>
    public string? Source { get; init; }

    /// <summary>A linked picture's target, which office never fetches.</summary>
    public string? Target { get; init; }

    /// <summary>The index of the docx section the picture is shown in: the section whose body,
    /// header, or footer holds it, or for a note the section of its first reference; null in a
    /// comment.</summary>
    public int? Section { get; init; }
}

/// <summary>A piece of a block's text: text, or a reference to a note or comment at its position
/// in the text.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(TextRun), "text")]
[JsonDerivedType(typeof(FootnoteReference), "footnote")]
[JsonDerivedType(typeof(EndnoteReference), "endnote")]
[JsonDerivedType(typeof(CommentReference), "comment")]
public abstract record Inline;

/// <summary>A run of text, with tabs as <c>\t</c> and line breaks as <c>\n</c>. Adjacent runs
/// are merged, so a block's text is one run between other inlines.</summary>
public sealed record TextRun(string Text) : Inline;

/// <summary>A footnote's reference, by the note's id.</summary>
public sealed record FootnoteReference(int Id) : Inline;

/// <summary>An endnote's reference, by the note's id.</summary>
public sealed record EndnoteReference(int Id) : Inline;

/// <summary>The end of a comment's anchored range, by the comment's id.</summary>
public sealed record CommentReference(int Id) : Inline;
