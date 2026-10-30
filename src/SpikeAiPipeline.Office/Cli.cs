using System.CommandLine;
using System.Reflection;
using SpikeAiPipeline.Office.Model;
using SpikeAiPipeline.Office.Reading;
using SpikeAiPipeline.Office.Rendering;

namespace SpikeAiPipeline.Office;

/// <summary>The CLI: one document in, its JSON model or Markdown on stdout.</summary>
public static class Cli
{
    /// <summary>The document was converted and written to stdout.</summary>
    public const int Converted = 0;

    /// <summary>The arguments were wrong; usage went to stderr.</summary>
    public const int UsageError = 2;

    /// <summary>The document was refused (missing, unreadable, not docx, xlsx, or pptx, not yet
    /// supported, encrypted or legacy, or malformed); the reason went to stderr and nothing to
    /// stdout.</summary>
    public const int Refused = 3;

    /// <summary>The CLI's version, from its assembly.</summary>
    public static string Version =>
        typeof(Cli).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown";

    /// <summary>Runs the CLI on the given arguments and returns its exit code.</summary>
    public static int Run(string[] args, TextWriter stdout, TextWriter stderr)
    {
        var input = new Argument<string>("input") { Description = "The docx, xlsx, or pptx file to convert." };
        var format = new Option<string>("--format")
        {
            Description = "The rendering written to stdout.",
            DefaultValueFactory = _ => "json",
            HelpName = "json|markdown",
        };
        format.AcceptOnlyFromAmong("json", "markdown");
        var media = new Option<string?>("--media")
        {
            Description = "The directory embedded media are written to, each once, named by the SHA-256 of its bytes.",
            HelpName = "dir",
        };
        var root = new RootCommand("Converts a docx, xlsx, or pptx document into office's JSON document model, or its Markdown rendering.")
        {
            input,
            format,
            media,
        };
        root.SetAction(parsed => Convert(parsed.GetValue(input)!, parsed.GetValue(format)!, stdout, stderr));

        var result = root.Parse(args);
        if (result.Errors.Count > 0)
        {
            foreach (var error in result.Errors)
            {
                stderr.WriteLine(error.Message);
            }
            stderr.WriteLine();
            root.Parse(["--help"]).Invoke(new InvocationConfiguration { Output = stderr, Error = stderr });
            return UsageError;
        }
        return result.Invoke(new InvocationConfiguration { Output = stdout, Error = stderr });
    }

    // The whole rendering is made before any of it is written, so a refusal leaves stdout empty.
    // Media are written to the --media directory as they are found, and the body has none yet,
    // so the directory is neither written nor created.
    private static int Convert(string path, string format, TextWriter stdout, TextWriter stderr)
    {
        Document document;
        try
        {
            document = OfficeReader.Read(path);
        }
        catch (RefusalException e)
        {
            stderr.WriteLine($"spike-ai-pipeline-office: refused: {e.Message}");
            return Refused;
        }
        stdout.Write(format == "markdown" ? MarkdownRenderer.Render(document) : DocumentJson.Serialize(document));
        stdout.Flush();
        return Converted;
    }
}
