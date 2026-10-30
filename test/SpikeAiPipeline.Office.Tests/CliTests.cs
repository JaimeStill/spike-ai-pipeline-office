using System.Diagnostics;
using System.IO.Compression;
using System.Text;

namespace SpikeAiPipeline.Office.Tests;

// The CLI's process contract, on the NativeAOT binary the check publishes into .state/aot/ before
// it runs the tests: arguments, stdout, stderr, exit code, and the media directory.
public sealed class CliTests : IDisposable
{
    private static readonly string Binary = Path.Combine(Fixtures.Root, ".state", "aot", "spike-ai-pipeline-office");

    private readonly DirectoryInfo scratch = Directory.CreateTempSubdirectory("office-cli-tests-");

    public void Dispose() => scratch.Delete(recursive: true);

    [Fact]
    public void VersionIsTheAssemblysVersion()
    {
        var result = Run("--version");

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(Cli.Version + "\n", result.Stdout);
        Assert.Empty(result.Stderr);
    }

    [Theory]
    [InlineData("json")]
    [InlineData(null)]
    public void JsonIsTheDefaultAndGoesToStdoutAlone(string? format)
    {
        var path = Fixtures.DocxPath("tracked-changes", "libreoffice");
        var result = format is null ? Run(path) : Run(path, "--format", format);

        Assert.Equal(Cli.Converted, result.ExitCode);
        Assert.Equal(Fixtures.Expected("tracked-changes", "json"), result.Stdout);
        Assert.Empty(result.Stderr);
    }

    [Fact]
    public void MarkdownGoesToStdoutAlone()
    {
        var result = Run(Fixtures.DocxPath("tables", "libreoffice"), "--format", "markdown");

        Assert.Equal(Cli.Converted, result.ExitCode);
        Assert.Equal(Fixtures.Expected("tables", "md"), result.Stdout);
        Assert.Empty(result.Stderr);
    }

    [Fact]
    public void MediaDirectoryIsAcceptedAndUntouchedWhenTheBodyHasNoMedia()
    {
        var media = Path.Combine(scratch.FullName, "media");

        var result = Run(Fixtures.DocxPath("headings", "python-docx"), "--media", media);

        Assert.Equal(Cli.Converted, result.ExitCode);
        Assert.Equal(Fixtures.Expected("headings", "json"), result.Stdout);
        Assert.False(Directory.Exists(media));
    }

    [Theory]
    [InlineData]
    [InlineData("--format", "xml", "in.docx")]
    [InlineData("--bogus", "in.docx")]
    [InlineData("one.docx", "two.docx")]
    [InlineData("in.docx", "--media")]
    public void UsageErrorsPrintUsageToStderrAndExitTwo(params string[] args)
    {
        var result = Run(args);

        Assert.Equal(Cli.UsageError, result.ExitCode);
        Assert.Empty(result.Stdout);
        Assert.Contains("Usage:", result.Stderr, StringComparison.Ordinal);
    }

    [Fact]
    public void AMissingFileIsRefused() =>
        AssertRefused(Path.Combine(scratch.FullName, "absent.docx"), "no such file");

    [Fact]
    public void AFileThatIsNoPackageIsRefused() =>
        AssertRefused(Write("notes.docx", Encoding.UTF8.GetBytes("plain text, not a package")), "not a docx, xlsx, or pptx package");

    [Fact]
    public void AZipThatIsNoOfficePackageIsRefused() =>
        AssertRefused(Zip("archive.docx", ("readme.txt", "hello")), "not a docx, xlsx, or pptx package");

    // An encrypted OOXML package is an OLE compound file, as are the legacy binary formats; the
    // signature alone identifies it.
    [Fact]
    public void AnEncryptedOrLegacyFileIsRefused() =>
        AssertRefused(
            Write("secret.docx", [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1, 0, 0, 0, 0]),
            "OLE compound file, which is an encrypted package or a legacy binary format");

    [Fact]
    public void ATruncatedPackageIsRefusedAsMalformed()
    {
        var bytes = File.ReadAllBytes(Fixtures.DocxPath("headings", "python-docx"));
        AssertRefused(Write("truncated.docx", bytes[..(bytes.Length / 2)]), "a malformed package");
    }

    [Fact]
    public void APackageWithBrokenXmlIsRefusedAsMalformed() =>
        AssertRefused(
            Package("broken.docx", "word/document.xml", "application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml", "<w:document"),
            "a malformed package");

    [Theory]
    [InlineData("xlsx", "xl/workbook.xml", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml")]
    [InlineData("pptx", "ppt/presentation.xml", "application/vnd.openxmlformats-officedocument.presentationml.presentation.main+xml")]
    public void XlsxAndPptxAreRefusedAsNotYetSupported(string format, string part, string contentType) =>
        AssertRefused(Package($"book.{format}", part, contentType, "<root/>"), $"{format} is not yet supported");

    private void AssertRefused(string path, string reason)
    {
        var result = Run(path, "--format", "markdown", "--media", Path.Combine(scratch.FullName, "media"));

        Assert.Equal(Cli.Refused, result.ExitCode);
        Assert.Empty(result.Stdout);
        Assert.StartsWith("spike-ai-pipeline-office: refused: ", result.Stderr, StringComparison.Ordinal);
        Assert.Contains(reason, result.Stderr, StringComparison.Ordinal);
    }

    private string Write(string name, byte[] bytes)
    {
        var path = Path.Combine(scratch.FullName, name);
        File.WriteAllBytes(path, bytes);
        return path;
    }

    private string Zip(string name, params (string Entry, string Content)[] entries)
    {
        var path = Path.Combine(scratch.FullName, name);
        using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
        foreach (var (entry, content) in entries)
        {
            using var writer = new StreamWriter(zip.CreateEntry(entry).Open());
            writer.Write(content);
        }
        return path;
    }

    // A package of one main part, with the content types and relationship that name it.
    private string Package(string name, string part, string contentType, string content) =>
        Zip(
            name,
            ("[Content_Types].xml",
                "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">" +
                "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>" +
                $"<Override PartName=\"/{part}\" ContentType=\"{contentType}\"/></Types>"),
            ("_rels/.rels",
                "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
                $"<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"{part}\"/>" +
                "</Relationships>"),
            (part, content));

    private sealed record Result(int ExitCode, string Stdout, string Stderr);

    private static Result Run(params string[] args)
    {
        Assert.True(File.Exists(Binary), $"{Binary} is missing: `mise run check` publishes it before the tests run");
        var start = new ProcessStartInfo(Binary)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        foreach (var arg in args)
        {
            start.ArgumentList.Add(arg);
        }
        using var process = Process.Start(start)!;
        var stderr = process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
        var stdout = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return new Result(process.ExitCode, stdout, stderr.Result);
    }
}
