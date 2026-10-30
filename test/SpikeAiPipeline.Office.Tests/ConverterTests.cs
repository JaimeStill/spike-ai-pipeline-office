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
    public static TheoryData<string, string> Cases => Fixtures.DocxVariants;

    [Theory]
    [MemberData(nameof(Cases))]
    public void ModelMatchesTheGroundTruth(string name, string variant)
    {
        var document = OfficeReader.Read(Fixtures.DocxPath(name, variant));
        var truth = JsonNode.Parse(File.ReadAllText(Fixtures.TruthPath(name)))!;

        Assert.Equal(SourceFormat.Docx, document.Format);
        Assert.Equal("docx", (string?)truth["format"]);
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

    [Theory]
    [MemberData(nameof(Cases))]
    public void DeletedTextIsGoneAndMovedTextAppearsOnce(string name, string variant)
    {
        var document = OfficeReader.Read(Fixtures.DocxPath(name, variant));
        var truth = JsonNode.Parse(File.ReadAllText(Fixtures.TruthPath(name)))!;
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
        Assert.Equal(Fixtures.Expected(name, "json"), DocumentJson.Serialize(OfficeReader.Read(Fixtures.DocxPath(name, variant))));

    [Theory]
    [MemberData(nameof(Cases))]
    public void MarkdownIsExact(string name, string variant) =>
        Assert.Equal(Fixtures.Expected(name, "md"), MarkdownRenderer.Render(OfficeReader.Read(Fixtures.DocxPath(name, variant))));
}
