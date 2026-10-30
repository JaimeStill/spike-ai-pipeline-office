namespace SpikeAiPipeline.Office.Tests;

/// <summary>The committed fixtures and the files the tests compare office's output with.</summary>
internal static class Fixtures
{
    /// <summary>The repository root, found from the test assembly's directory.</summary>
    public static string Root { get; } = FindRoot();

    /// <summary>The generated docx fixtures, by name.</summary>
    public static readonly string[] Docx =
    [
        "headings", "lists", "tables", "tracked-changes",
        "sections", "headers-even-off", "notes", "comments", "comments-plain", "media", "charts",
    ];

    /// <summary>The writers of each generated fixture: python-docx, and LibreOffice's re-save.</summary>
    public static readonly string[] Variants = ["python-docx", "libreoffice"];

    /// <summary>Every generated docx fixture in each of its variants.</summary>
    public static TheoryData<string, string> DocxVariants
    {
        get
        {
            var data = new TheoryData<string, string>();
            foreach (var name in Docx)
            {
                foreach (var variant in Variants)
                {
                    data.Add(name, variant);
                }
            }
            return data;
        }
    }

    public static string DocxPath(string name, string variant) =>
        Path.Combine(Root, "fixtures", "generated", "docx", $"{name}.{variant}.docx");

    /// <summary>A fixture variant's ground truth: the variant's own where the generator wrote one,
    /// because LibreOffice's re-save changed what the document holds, or else the fixture's.</summary>
    public static string TruthPath(string name, string variant)
    {
        var own = Path.Combine(Root, "fixtures", "generated", "docx", $"{name}.{variant}.truth.json");
        return File.Exists(own) ? own : Path.Combine(Root, "fixtures", "generated", "docx", $"{name}.truth.json");
    }

    /// <summary>Office's exact output for a fixture variant: the variant's own file where the
    /// variants' outputs differ (part names, ids), or else one file for both.</summary>
    public static string Expected(string name, string variant, string extension)
    {
        var directory = Path.Combine(Root, "test", "SpikeAiPipeline.Office.Tests", "Expected");
        var own = Path.Combine(directory, $"{name}.{variant}.{extension}");
        return File.ReadAllText(File.Exists(own) ? own : Path.Combine(directory, $"{name}.{extension}"));
    }

    private static string FindRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "SpikeAiPipeline.Office.slnx")))
            {
                return dir.FullName;
            }
        }
        throw new InvalidOperationException("the repository root, holding SpikeAiPipeline.Office.slnx, is not above the test assembly");
    }
}
