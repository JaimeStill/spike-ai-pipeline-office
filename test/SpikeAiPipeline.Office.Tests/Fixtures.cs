namespace SpikeAiPipeline.Office.Tests;

/// <summary>The committed fixtures and the files the tests compare office's output with.</summary>
internal static class Fixtures
{
    /// <summary>The repository root, found from the test assembly's directory.</summary>
    public static string Root { get; } = FindRoot();

    /// <summary>The generated docx fixtures, by name.</summary>
    public static readonly string[] Docx = ["headings", "lists", "tables", "tracked-changes"];

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

    public static string TruthPath(string name) =>
        Path.Combine(Root, "fixtures", "generated", "docx", $"{name}.truth.json");

    /// <summary>Office's exact output for a fixture, one file for both variants.</summary>
    public static string Expected(string name, string extension) =>
        File.ReadAllText(Path.Combine(Root, "test", "SpikeAiPipeline.Office.Tests", "Expected", $"{name}.{extension}"));

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
