using System.Security.Cryptography;
using System.Text.Json.Nodes;
using SpikeAiPipeline.Office.Model;
using SpikeAiPipeline.Office.Reading;
using SpikeAiPipeline.Office.Rendering;

namespace SpikeAiPipeline.Office.Tests;

// The converter's seam, a document in and the model out, and the Markdown renderer over the
// model, against the generated fixtures: the model element by element against the generator's
// ground truth, then the JSON and the Markdown exactly.
public sealed class ConverterTests
{
    public static TheoryData<string, string> Cases => Fixtures.Variants;

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

    [Theory]
    [MemberData(nameof(Cases))]
    public void JsonIsExact(string name, string variant) =>
        Assert.Equal(Fixtures.Expected(name, variant, "json"), DocumentJson.Serialize(OfficeReader.Read(Fixtures.DocumentPath(name, variant))));

    [Theory]
    [MemberData(nameof(Cases))]
    public void MarkdownIsExact(string name, string variant) =>
        Assert.Equal(Fixtures.Expected(name, variant, "md"), MarkdownRenderer.Render(OfficeReader.Read(Fixtures.DocumentPath(name, variant))));
}
