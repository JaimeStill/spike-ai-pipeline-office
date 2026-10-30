using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SpikeAiPipeline.Office.Model;

/// <summary>The model's JSON, written through source-generated serializers, so it needs no
/// reflection under NativeAOT.</summary>
public static class DocumentJson
{
    // Non-ASCII text is written as itself rather than as \u escapes: the JSON is a data file for
    // the pipeline, never embedded in HTML, so the relaxed encoder's one risk doesn't apply.
    private static readonly DocumentJsonContext Context = new(
        new JsonSerializerOptions(DocumentJsonContext.Default.Options)
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        });

    /// <summary>The document as JSON: camelCase, indented two spaces, null properties left out,
    /// and ending in a newline. The same model always gives the same bytes.</summary>
    public static string Serialize(Document document) =>
        JsonSerializer.Serialize(document, Context.Document) + "\n";
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = true,
    IndentSize = 2,
    NewLine = "\n")]
[JsonSerializable(typeof(Document))]
internal sealed partial class DocumentJsonContext : JsonSerializerContext;
