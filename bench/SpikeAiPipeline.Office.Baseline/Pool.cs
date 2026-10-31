namespace SpikeAiPipeline.Office.Baseline;

/// <summary>One document of the pool and its ground truth.</summary>
/// <param name="Path">The document's path, relative to the repository root.</param>
/// <param name="Set">The pool's set it belongs to: <c>generated</c> or <c>realworld</c>.</param>
/// <param name="Format">Its format, its extension: docx, xlsx, or pptx.</param>
/// <param name="TruthPath">Its ground truth's path, relative to the repository root.</param>
internal sealed record PoolFile(string Path, string Set, string Format, string TruthPath);

/// <summary>The committed pool, and nothing else: the documents of <c>fixtures/generated/</c> and
/// <c>fixtures/realworld/</c>, the same files office's tests read.</summary>
internal static class Pool
{
    private static readonly string[] Formats = ["docx", "xlsx", "pptx"];

    /// <summary>Every document of the pool, the generated set then the real-world set, each by
    /// format and then by name.</summary>
    public static List<PoolFile> Files(string root)
    {
        var files = new List<PoolFile>();
        foreach (var set in new[] { "generated", "realworld" })
        {
            foreach (var format in Formats)
            {
                var directory = System.IO.Path.Combine(root, "fixtures", set, format);
                foreach (var path in Directory.GetFiles(directory, "*." + format).Order(StringComparer.Ordinal))
                {
                    files.Add(new PoolFile(Relative(root, path), set, format, Relative(root, TruthOf(path, set, format))));
                }
            }
        }
        return files;
    }

    // A generated file, <name>.<writer>.<format>, has its writer's own truth where the generator
    // wrote one, or else its fixture's; a real-world file, <name>.<format>, has its own. This is the
    // rule of the tests' Fixtures.TruthPath, on file names rather than fixture names.
    private static string TruthOf(string path, string set, string format)
    {
        var directory = System.IO.Path.GetDirectoryName(path)!;
        var stem = System.IO.Path.GetFileNameWithoutExtension(path);
        if (set == "realworld")
        {
            return Existing(System.IO.Path.Combine(directory, stem + ".truth.json"));
        }
        var own = System.IO.Path.Combine(directory, stem + ".truth.json");
        var fixture = System.IO.Path.Combine(directory, stem[..stem.LastIndexOf('.')] + ".truth.json");
        return File.Exists(own) ? own : Existing(fixture);
    }

    private static string Existing(string path) =>
        File.Exists(path) ? path : throw new FileNotFoundException("a pool file has no ground truth", path);

    private static string Relative(string root, string path) => System.IO.Path.GetRelativePath(root, path);
}
