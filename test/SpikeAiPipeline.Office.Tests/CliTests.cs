using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using SpikeAiPipeline.Office.Model;
using SpikeAiPipeline.Office.Reading;
using SpikeAiPipeline.Office.Rendering;

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
        var path = Fixtures.DocumentPath("tracked-changes", "libreoffice");
        var result = format is null ? Run(path) : Run(path, "--format", format);

        Assert.Equal(Cli.Converted, result.ExitCode);
        Assert.Equal(Fixtures.Expected("tracked-changes", "libreoffice", "json"), result.Stdout);
        Assert.Empty(result.Stderr);
    }

    [Fact]
    public void MarkdownGoesToStdoutAlone()
    {
        var result = Run(Fixtures.DocumentPath("tables", "libreoffice"), "--format", "markdown");

        Assert.Equal(Cli.Converted, result.ExitCode);
        Assert.Equal(Fixtures.Expected("tables", "libreoffice", "md"), result.Stdout);
        Assert.Empty(result.Stderr);
    }

    // Each distinct picture once, named by its bytes, however many parts and blocks hold it: the
    // media fixture's python-docx variant stores the red square in two parts and shows it three
    // times, and links one picture, which is never written. The directory and its missing parent
    // are created.
    [Theory]
    [InlineData("json")]
    [InlineData("markdown")]
    public void MediaAreWrittenOnceEachNamedByTheirContent(string format)
    {
        var media = Path.Combine(scratch.FullName, "out", "media");
        var path = Fixtures.DocumentPath("media", "python-docx");

        var result = Run(path, "--format", format, "--media", media);

        Assert.Equal(Cli.Converted, result.ExitCode);
        Assert.Equal(Fixtures.Expected("media", "python-docx", format == "json" ? "json" : "md"), result.Stdout);
        var files = Directory.GetFiles(media).Select(Path.GetFileName).Order(StringComparer.Ordinal).ToList();
        Assert.Equal(
            [
                "0fcca33bb0baf1a0669bd2fcdcaeb34e33d27196f7ffb5294b27aea8a9e46446.png",
                "10a86bd779a80e60e5d3314ec1edeab643cb454a4c7dfee671132090878d7aea.png",
                "670be6349ed84b27d6ca43d66ba6e9454c632f68de8d9b8fea78a480e90afb31.emf",
                "df026100fd6fe38e22923c5152a33ea6f6879e38073fb47f022ed9d90dec65a8.png",
            ],
            files);
        using var package = ZipFile.OpenRead(path);
        var stored = package.Entries.Where(e => e.FullName.StartsWith("word/media/", StringComparison.Ordinal)).ToList();
        Assert.Equal(5, stored.Count); // The red square's copy is a fifth part, written as the same file.
        foreach (var entry in stored)
        {
            using var stream = entry.Open();
            using var bytes = new MemoryStream();
            stream.CopyTo(bytes);
            var name = Convert.ToHexStringLower(SHA256.HashData(bytes.ToArray())) + Path.GetExtension(entry.Name);
            Assert.Equal(bytes.ToArray(), File.ReadAllBytes(Path.Combine(media, name)));
        }
    }

    [Theory]
    [InlineData("json")]
    [InlineData("markdown")]
    public void AnXlsxConvertsToStdoutAlone(string format)
    {
        var result = Run(Fixtures.DocumentPath("formulas", "libreoffice"), "--format", format);

        Assert.Equal(Cli.Converted, result.ExitCode);
        Assert.Equal(Fixtures.Expected("formulas", "libreoffice", format == "json" ? "json" : "md"), result.Stdout);
        Assert.Empty(result.Stderr);
    }

    // The drawings fixture's openpyxl variant stores the red square twice, as two parts of one
    // sheet's drawing, and the blue square on a second sheet: two files.
    [Fact]
    public void SheetPicturesAreWrittenOnceEachNamedByTheirContent()
    {
        var media = Path.Combine(scratch.FullName, "media");
        var path = Fixtures.DocumentPath("drawings", "openpyxl");

        var result = Run(path, "--format", "markdown", "--media", media);

        Assert.Equal(Cli.Converted, result.ExitCode);
        Assert.Equal(Fixtures.Expected("drawings", "openpyxl", "md"), result.Stdout);
        var files = Directory.GetFiles(media).Select(Path.GetFileName).Order(StringComparer.Ordinal).ToList();
        Assert.Equal(
            [
                "0fcca33bb0baf1a0669bd2fcdcaeb34e33d27196f7ffb5294b27aea8a9e46446.png",
                "10a86bd779a80e60e5d3314ec1edeab643cb454a4c7dfee671132090878d7aea.png",
            ],
            files);
        using var package = ZipFile.OpenRead(path);
        var stored = package.Entries.Where(e => e.FullName.StartsWith("xl/media/", StringComparison.Ordinal)).ToList();
        Assert.Equal(3, stored.Count);
        foreach (var entry in stored)
        {
            using var stream = entry.Open();
            using var bytes = new MemoryStream();
            stream.CopyTo(bytes);
            var name = Convert.ToHexStringLower(SHA256.HashData(bytes.ToArray())) + Path.GetExtension(entry.Name);
            Assert.Equal(bytes.ToArray(), File.ReadAllBytes(Path.Combine(media, name)));
        }
    }

    // The slides fixture's python-pptx variant presents its slides in an order that isn't their
    // parts', with a hidden slide and notes.
    [Theory]
    [InlineData("json")]
    [InlineData("markdown")]
    public void APptxConvertsToStdoutAlone(string format)
    {
        var result = Run(Fixtures.DocumentPath("slides", "python-pptx"), "--format", format);

        Assert.Equal(Cli.Converted, result.ExitCode);
        Assert.Equal(Fixtures.Expected("slides", "python-pptx", format == "json" ? "json" : "md"), result.Stdout);
        Assert.Empty(result.Stderr);
    }

    // The slide-objects fixture shows the red square on two slides, one part, and the blue
    // square on one: two files, each the bytes of its part.
    [Theory]
    [InlineData("json")]
    [InlineData("markdown")]
    public void SlidePicturesAreWrittenOnceEachNamedByTheirContent(string format)
    {
        var media = Path.Combine(scratch.FullName, "media");
        var path = Fixtures.DocumentPath("slide-objects", "python-pptx");

        var result = Run(path, "--format", format, "--media", media);

        Assert.Equal(Cli.Converted, result.ExitCode);
        Assert.Equal(Fixtures.Expected("slide-objects", "python-pptx", format == "json" ? "json" : "md"), result.Stdout);
        var files = Directory.GetFiles(media).Select(Path.GetFileName).Order(StringComparer.Ordinal).ToList();
        Assert.Equal(
            [
                "0fcca33bb0baf1a0669bd2fcdcaeb34e33d27196f7ffb5294b27aea8a9e46446.png",
                "10a86bd779a80e60e5d3314ec1edeab643cb454a4c7dfee671132090878d7aea.png",
            ],
            files);
        using var package = ZipFile.OpenRead(path);
        var stored = package.Entries.Where(e => e.FullName.StartsWith("ppt/media/", StringComparison.Ordinal)).ToList();
        Assert.Equal(2, stored.Count);
        foreach (var entry in stored)
        {
            using var stream = entry.Open();
            using var bytes = new MemoryStream();
            stream.CopyTo(bytes);
            var name = Convert.ToHexStringLower(SHA256.HashData(bytes.ToArray())) + Path.GetExtension(entry.Name);
            Assert.Equal(bytes.ToArray(), File.ReadAllBytes(Path.Combine(media, name)));
        }
    }

    public static TheoryData<string, string> RealWorldFiles => Fixtures.RealWorldFiles;

    // Each real-world file, written by Word, Excel, or PowerPoint, converts on the published binary
    // to what the converter's seam gives, whose model the file's ground truth checks.
    [Theory]
    [MemberData(nameof(RealWorldFiles))]
    public void ARealWorldFileConvertsToStdoutAlone(string name, string variant)
    {
        var path = Fixtures.DocumentPath(name, variant);
        var document = OfficeReader.Read(path);

        foreach (var (format, expected) in new[] { ("json", DocumentJson.Serialize(document)), ("markdown", MarkdownRenderer.Render(document)) })
        {
            var result = Run(path, "--format", format);

            Assert.Equal(Cli.Converted, result.ExitCode);
            Assert.Equal(expected, result.Stdout);
            Assert.Empty(result.Stderr);
        }
    }

    [Fact]
    public void NothingIsWrittenWithoutTheMediaOption()
    {
        var before = Directory.GetFileSystemEntries(Path.GetDirectoryName(Fixtures.DocumentPath("media", "python-docx"))!);

        var result = RunIn(scratch.FullName, Fixtures.DocumentPath("media", "python-docx"));

        Assert.Equal(Cli.Converted, result.ExitCode);
        Assert.Equal(Fixtures.Expected("media", "python-docx", "json"), result.Stdout);
        Assert.Empty(Directory.GetFileSystemEntries(scratch.FullName));
        Assert.Equal(before, Directory.GetFileSystemEntries(Path.GetDirectoryName(Fixtures.DocumentPath("media", "python-docx"))!));
    }

    // The media directory is created on every conversion it is given for, so it existing says the
    // conversion succeeded, even when the document has no media.
    [Fact]
    public void TheMediaDirectoryIsCreatedEvenWithoutMedia()
    {
        var media = Path.Combine(scratch.FullName, "media");

        var result = Run(Fixtures.DocumentPath("headings", "python-docx"), "--media", media);

        Assert.Equal(Cli.Converted, result.ExitCode);
        Assert.Equal(Fixtures.Expected("headings", "python-docx", "json"), result.Stdout);
        Assert.Empty(Directory.GetFileSystemEntries(media));
    }

    [Fact]
    public void MediaThatCannotBeWrittenLeaveStdoutEmptyAndExitOne()
    {
        var media = Write("media", [1, 2, 3]); // A file where the directory would be.

        var result = Run(Fixtures.DocumentPath("media", "python-docx"), "--media", media);

        Assert.Equal(Cli.MediaNotWritten, result.ExitCode);
        Assert.Empty(result.Stdout);
        Assert.StartsWith("spike-ai-pipeline-office: media not written: ", result.Stderr, StringComparison.Ordinal);
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
        var bytes = File.ReadAllBytes(Fixtures.DocumentPath("headings", "python-docx"));
        AssertRefused(Write("truncated.docx", bytes[..(bytes.Length / 2)]), "a malformed package");
    }

    [Fact]
    public void APackageWithBrokenXmlIsRefusedAsMalformed() =>
        AssertRefused(
            Package("broken.docx", "word/document.xml", "application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml", "<w:document"),
            "a malformed package");

    // The sections fixture's third section reads its default header, which holds a picture,
    // before its first-page header, broken here: the picture was read, and still nothing is
    // written, as media are written only once the whole conversion has succeeded.
    [Fact]
    public void APackageMalformedAfterItsMediaIsRefusedAndWritesNoMedia()
    {
        var path = Path.Combine(scratch.FullName, "late.docx");
        File.Copy(Fixtures.DocumentPath("sections", "python-docx"), path);
        using (var zip = ZipFile.Open(path, ZipArchiveMode.Update))
        {
            zip.GetEntry("word/header5.xml")!.Delete();
            using var writer = new StreamWriter(zip.CreateEntry("word/header5.xml").Open());
            writer.Write("<w:hdr");
        }

        AssertRefused(path, "a malformed package");
    }

    [Fact]
    public void AnXlsxWithBrokenXmlIsRefusedAsMalformed() =>
        AssertRefused(
            Package("broken.xlsx", "xl/workbook.xml", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml", "<workbook"),
            "a malformed package");

    // The drawings fixture's first sheet holds both red squares; its second sheet's part, read
    // after them, is broken here, and still no media are written.
    [Fact]
    public void AnXlsxMalformedAfterItsMediaIsRefusedAndWritesNoMedia()
    {
        var path = Path.Combine(scratch.FullName, "late.xlsx");
        File.Copy(Fixtures.DocumentPath("drawings", "openpyxl"), path);
        using (var zip = ZipFile.Open(path, ZipArchiveMode.Update))
        {
            zip.GetEntry("xl/worksheets/sheet2.xml")!.Delete();
            using var writer = new StreamWriter(zip.CreateEntry("xl/worksheets/sheet2.xml").Open());
            writer.Write("<worksheet");
        }

        AssertRefused(path, "a malformed package");
    }

    [Fact]
    public void APptxWithBrokenXmlIsRefusedAsMalformed() =>
        AssertRefused(
            Package("broken.pptx", "ppt/presentation.xml", "application/vnd.openxmlformats-officedocument.presentationml.presentation.main+xml", "<p:presentation"),
            "a malformed package");

    // An attribute outside its type's values, as poi's test file for its bug 60293 has: a header
    // reference of type "odd", which no header kind is.
    [Fact]
    public void AnAttributeOutsideItsValuesIsRefusedAsMalformed() =>
        AssertRefused(
            Package(
                "odd.docx",
                "word/document.xml",
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml",
                "<w:document xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\" " +
                "xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><w:body><w:p/>" +
                "<w:sectPr><w:headerReference w:type=\"odd\" r:id=\"rId9\"/></w:sectPr></w:body></w:document>"),
            "a malformed package");

    // A relationship to a part the package lacks, as poi's picture.xlsx has (its connections
    // part), fails the SDK when it first loads the package's parts.
    [Fact]
    public void ARelationshipToAMissingPartIsRefusedAsMalformed() =>
        AssertRefused(
            Zip(
                "missing.xlsx",
                ("[Content_Types].xml",
                    "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">" +
                    "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>" +
                    "<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/></Types>"),
                ("_rels/.rels",
                    "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
                    "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/>" +
                    "</Relationships>"),
                ("xl/workbook.xml", "<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><sheets/></workbook>"),
                ("xl/_rels/workbook.xml.rels",
                    "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
                    "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/connections\" Target=\"connections.xml\"/>" +
                    "</Relationships>")),
            "doesn't exist in the package");

    private void AssertRefused(string path, string reason)
    {
        var media = Path.Combine(scratch.FullName, "media");
        var result = Run(path, "--format", "markdown", "--media", media);

        Assert.Equal(Cli.Refused, result.ExitCode);
        Assert.Empty(result.Stdout);
        Assert.False(Directory.Exists(media));
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

    private static Result Run(params string[] args) => RunIn(Environment.CurrentDirectory, args);

    private static Result RunIn(string directory, params string[] args)
    {
        Assert.True(File.Exists(Binary), $"{Binary} is missing: `mise run check` publishes it before the tests run");
        var start = new ProcessStartInfo(Binary)
        {
            WorkingDirectory = directory,
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
