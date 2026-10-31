using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using SpikeAiPipeline.Office.Model;
using SpikeAiPipeline.Office.Reading;
using SpikeAiPipeline.Office.Rendering;

namespace SpikeAiPipeline.Office.Tests;

// The converter's seam, a document in and the model out, and the Markdown renderer over the
// model, against the generated fixtures and the real-world files: the model element by element
// against the ground truth (the generator's, or a real-world file's, written by hand), then, for
// the generated fixtures, the JSON and the Markdown exactly.
public sealed class ConverterTests : IDisposable
{
    private readonly DirectoryInfo scratch = Directory.CreateTempSubdirectory("office-converter-tests-");

    public void Dispose() => scratch.Delete(recursive: true);

    // Every fixture with its ground truth: the generated variants and the real-world files.
    public static TheoryData<string, string> Cases => Fixtures.All;

    // The generated variants, whose exact output the Expected files hold.
    public static TheoryData<string, string> Generated => Fixtures.Variants;

    [Theory]
    [MemberData(nameof(Cases))]
    public void ModelMatchesTheGroundTruth(string name, string variant)
    {
        var document = OfficeReader.Read(Fixtures.DocumentPath(name, variant));
        var truth = JsonNode.Parse(File.ReadAllText(Fixtures.TruthPath(name, variant)))!;

        Assert.Equal(Fixtures.Format(name), document.Format.ToString().ToLowerInvariant());
        Assert.Equal(Fixtures.Format(name), (string?)truth["format"]);
        var expected = truth["elements"]!.AsArray();
        var actual = GroundTruth.Elements(document);
        for (var i = 0; i < Math.Min(expected.Count, actual.Count); i++)
        {
            Assert.True(
                JsonNode.DeepEquals(expected[i], actual[i]),
                $"element {i}: expected {expected[i]!.ToJsonString()}, got {actual[i].ToJsonString()}");
        }
        Assert.Equal(expected.Count, actual.Count);
    }

    // Sections with their headers and footers, footnotes, endnotes, and comments, each compared
    // whole; a part the truth leaves out the model must not have.
    [Theory]
    [MemberData(nameof(Cases))]
    public void OtherPartsMatchTheGroundTruth(string name, string variant)
    {
        var document = OfficeReader.Read(Fixtures.DocumentPath(name, variant));
        var truth = JsonNode.Parse(File.ReadAllText(Fixtures.TruthPath(name, variant)))!;

        foreach (var part in GroundTruth.Parts)
        {
            var expected = truth[part];
            var actual = GroundTruth.Part(document, part);
            Assert.True(
                JsonNode.DeepEquals(expected, actual),
                $"{part}: expected {expected?.ToJsonString() ?? "none"}, got {actual?.ToJsonString() ?? "none"}");
        }
    }

    // Every media file the conversion holds is named by its bytes' SHA-256 and its part's
    // extension, and is exactly the set the media blocks name: one per distinct content.
    [Theory]
    [MemberData(nameof(Cases))]
    public void MediaFilesAreNamedByTheirContent(string name, string variant)
    {
        var conversion = OfficeReader.Convert(Fixtures.DocumentPath(name, variant));
        var truth = JsonNode.Parse(File.ReadAllText(Fixtures.TruthPath(name, variant)))!;

        foreach (var (file, bytes) in conversion.Media)
        {
            Assert.StartsWith(Convert.ToHexStringLower(SHA256.HashData(bytes)) + ".", file, StringComparison.Ordinal);
        }
        Assert.Equal(Names(truth).Order(StringComparer.Ordinal), conversion.Media.Keys.Order(StringComparer.Ordinal));
    }

    // The distinct names of the truth's media elements, wherever they are.
    private static HashSet<string> Names(JsonNode? node)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        void Walk(JsonNode? n)
        {
            switch (n)
            {
                case JsonObject o:
                    if ((string?)o["kind"] == "media" && o["name"] is { } fileName)
                    {
                        names.Add((string)fileName!);
                    }
                    foreach (var (_, value) in o)
                    {
                        Walk(value);
                    }
                    break;
                case JsonArray a:
                    foreach (var value in a)
                    {
                        Walk(value);
                    }
                    break;
                default:
                    break;
            }
        }
        Walk(node);
        return names;
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void DeletedTextIsGoneAndMovedTextAppearsOnce(string name, string variant)
    {
        var document = OfficeReader.Read(Fixtures.DocumentPath(name, variant));
        var truth = JsonNode.Parse(File.ReadAllText(Fixtures.TruthPath(name, variant)))!;
        string[] outputs = [DocumentJson.Serialize(document), MarkdownRenderer.Render(document)];

        foreach (var output in outputs)
        {
            foreach (var absent in truth["absent"]!.AsArray().Select(n => (string)n!))
            {
                Assert.DoesNotContain(absent, output, StringComparison.Ordinal);
            }
            foreach (var once in truth["once"]!.AsArray().Select(n => (string)n!))
            {
                Assert.Equal(2, output.Split(once).Length); // One occurrence splits the output in two.
            }
        }
    }

    // A macro-enabled document, template, or slide show is read as its base format, its macros
    // ignored and never run: a fixture whose main part is given each such content type, the
    // macro-enabled document with a VBA project part too, converts to the fixture's own model.
    [Theory]
    [InlineData("headings", "python-docx", "application/vnd.ms-word.document.macroEnabled.main+xml")]
    [InlineData("headings", "python-docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.template.main+xml")]
    [InlineData("headings", "python-docx", "application/vnd.ms-word.template.macroEnabledTemplate.main+xml")]
    [InlineData("cells", "openpyxl", "application/vnd.ms-excel.sheet.macroEnabled.main+xml")]
    [InlineData("cells", "openpyxl", "application/vnd.openxmlformats-officedocument.spreadsheetml.template.main+xml")]
    [InlineData("cells", "openpyxl", "application/vnd.ms-excel.template.macroEnabled.main+xml")]
    [InlineData("slides", "python-pptx", "application/vnd.ms-powerpoint.presentation.macroEnabled.main+xml")]
    [InlineData("slides", "python-pptx", "application/vnd.openxmlformats-officedocument.presentationml.template.main+xml")]
    [InlineData("slides", "python-pptx", "application/vnd.ms-powerpoint.template.macroEnabled.main+xml")]
    [InlineData("slides", "python-pptx", "application/vnd.openxmlformats-officedocument.presentationml.slideshow.main+xml")]
    public void AMacroEnabledFileOrTemplateIsReadAsItsBaseFormat(string name, string variant, string contentType)
    {
        var source = Fixtures.DocumentPath(name, variant);
        var path = Path.Combine(scratch.FullName, Path.GetFileName(source));
        File.Copy(source, path);
        using (var zip = ZipFile.Open(path, ZipArchiveMode.Update))
        {
            var baseType = $"application/vnd.openxmlformats-officedocument.{Fixtures.Format(name) switch
            {
                "docx" => "wordprocessingml.document",
                "xlsx" => "spreadsheetml.sheet",
                _ => "presentationml.presentation",
            }}.main+xml";
            var types = Read(zip, "[Content_Types].xml");
            Assert.Contains(baseType, types, StringComparison.Ordinal);
            types = types.Replace(baseType, contentType, StringComparison.Ordinal);
            if (contentType.Contains("ms-word.document.macroEnabled", StringComparison.Ordinal))
            {
                // A VBA project of bytes that are no macro at all: nothing reads, let alone runs, it.
                types = types.Replace("</Types>", "<Override PartName=\"/word/vbaProject.bin\" ContentType=\"application/vnd.ms-office.vbaProject\"/></Types>", StringComparison.Ordinal);
                Write(zip, "word/vbaProject.bin", "not a macro");
                var relationships = Read(zip, "word/_rels/document.xml.rels").Replace(
                    "</Relationships>",
                    "<Relationship Id=\"rIdVba\" Type=\"http://schemas.microsoft.com/office/2006/relationships/vbaProject\" Target=\"vbaProject.bin\"/></Relationships>",
                    StringComparison.Ordinal);
                Write(zip, "word/_rels/document.xml.rels", relationships);
            }
            Write(zip, "[Content_Types].xml", types);
        }

        Assert.Equal(DocumentJson.Serialize(OfficeReader.Read(source)), DocumentJson.Serialize(OfficeReader.Read(path)));
    }

    // An auto-number's startAt is its list's start, and a startAt of 1 is the default's: the
    // bullets fixture's first numbered list given startAt 3, its second one 1.
    [Fact]
    public void APptxAutoNumberStartsItsListAtItsStartAt()
    {
        var path = Path.Combine(scratch.FullName, "bullets.pptx");
        File.Copy(Fixtures.DocumentPath("bullets", "python-pptx"), path);
        using (var zip = ZipFile.Open(path, ZipArchiveMode.Update))
        {
            foreach (var (slide, startAt) in new[] { ("ppt/slides/slide1.xml", 3), ("ppt/slides/slide2.xml", 1) })
            {
                var xml = Read(zip, slide);
                var at = xml.IndexOf("<a:buAutoNum type=\"arabicPeriod\"/>", StringComparison.Ordinal);
                Write(zip, slide, xml[..at] + $"<a:buAutoNum type=\"arabicPeriod\" startAt=\"{startAt}\"/>" + xml[(at + "<a:buAutoNum type=\"arabicPeriod\"/>".Length)..]);
            }
        }

        var lists = OfficeReader.Read(path).Blocks.OfType<Slide>()
            .Select(slide => slide.Blocks.OfType<ListBlock>().First(list => list.Ordered))
            .ToList();
        Assert.Equal([3, null], lists.Select(list => list.Start));
    }

    private static string Read(ZipArchive zip, string entry)
    {
        using var reader = new StreamReader(zip.GetEntry(entry)!.Open());
        return reader.ReadToEnd();
    }

    private static void Write(ZipArchive zip, string entry, string content)
    {
        zip.GetEntry(entry)?.Delete();
        using var writer = new StreamWriter(zip.CreateEntry(entry).Open());
        writer.Write(content);
    }

    [Theory]
    [MemberData(nameof(Generated))]
    public void JsonIsExact(string name, string variant) =>
        Assert.Equal(Fixtures.Expected(name, variant, "json"), DocumentJson.Serialize(OfficeReader.Read(Fixtures.DocumentPath(name, variant))));

    [Theory]
    [MemberData(nameof(Generated))]
    public void MarkdownIsExact(string name, string variant) =>
        Assert.Equal(Fixtures.Expected(name, variant, "md"), MarkdownRenderer.Render(OfficeReader.Read(Fixtures.DocumentPath(name, variant))));
}
