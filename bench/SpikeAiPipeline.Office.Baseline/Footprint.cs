using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.Json.Nodes;
using System.Xml.Linq;

namespace SpikeAiPipeline.Office.Baseline;

/// <summary>What office takes on disk and the licenses it ships under, read from its publish on
/// this machine, and the host the run measured on.</summary>
internal static class Footprint
{
    /// <summary>office's binary as published, and the licenses of the NuGet packages its publish
    /// restored (from the CLI project's <c>obj/project.assets.json</c>, each package's nuspec), the
    /// NativeAOT compiler's among them, whose runtime is compiled into the binary.</summary>
    public static JsonObject Office(string root, string binary)
    {
        var packages = new JsonArray();
        var assets = JsonNode.Parse(File.ReadAllText(Path.Combine(root, "src", "SpikeAiPipeline.Office", "obj", "project.assets.json")))!;
        var packagesPath = (string)assets["project"]!["restore"]!["packagesPath"]!;
        foreach (var (key, library) in assets["libraries"]!.AsObject())
        {
            if ((string?)library!["type"] != "package")
            {
                continue;
            }
            var path = (string)library["path"]!;
            var nuspec = Directory.GetFiles(Path.Combine(packagesPath, path), "*.nuspec").FirstOrDefault();
            string? license = null;
            if (nuspec is not null)
            {
                var metadata = XDocument.Load(nuspec).Root?.Elements().FirstOrDefault(e => e.Name.LocalName == "metadata");
                license = metadata?.Elements().FirstOrDefault(e => e.Name.LocalName == "license")?.Value
                    ?? metadata?.Elements().FirstOrDefault(e => e.Name.LocalName == "licenseUrl")?.Value;
            }
            packages.Add(new JsonObject { ["package"] = key, ["license"] = license });
        }
        return new JsonObject
        {
            ["binary"] = Path.GetRelativePath(root, binary),
            ["runtimeIdentifier"] = RuntimeIdentifier(),
            ["bytes"] = new FileInfo(binary).Length,
            ["debugSymbolsBytes"] = File.Exists(binary + ".dbg") ? new FileInfo(binary + ".dbg").Length : null,
            ["license"] = FirstLine(Path.Combine(root, "LICENSE")),
            ["packages"] = packages,
        };
    }

    /// <summary>The host: CPU, logical CPUs, memory, kernel, operating system, and the .NET
    /// runtime the bench ran on.</summary>
    public static JsonObject Host() => new()
    {
        ["cpu"] = File.ReadLines("/proc/cpuinfo").Where(l => l.StartsWith("model name", StringComparison.Ordinal)).Select(l => l.Split(':', 2)[1].Trim()).FirstOrDefault(),
        ["logicalCpus"] = Environment.ProcessorCount,
        ["memTotal"] = File.ReadLines("/proc/meminfo").First(l => l.StartsWith("MemTotal:", StringComparison.Ordinal))["MemTotal:".Length..].Trim(),
        ["kernel"] = File.ReadAllText("/proc/sys/kernel/osrelease").Trim(),
        ["os"] = File.ReadLines("/etc/os-release").Where(l => l.StartsWith("PRETTY_NAME=", StringComparison.Ordinal)).Select(l => l["PRETTY_NAME=".Length..].Trim('"')).FirstOrDefault(),
        ["architecture"] = RuntimeInformation.OSArchitecture.ToString().ToLowerInvariant(),
        ["dotnet"] = Environment.Version.ToString(),
    };

    private static string RuntimeIdentifier() => RuntimeInformation.OSArchitecture switch
    {
        Architecture.X64 => "linux-x64",
        Architecture.Arm64 => "linux-arm64",
        var other => "linux-" + other.ToString().ToLowerInvariant(),
    };

    private static string FirstLine(string path) =>
        File.ReadLines(path).Select(l => l.Trim()).FirstOrDefault(l => l.Length > 0) ?? "";

    /// <summary>A byte count in MiB, to two places, for the summary.</summary>
    public static double MiB(long bytes) => Math.Round(bytes / 1048576.0, 2, MidpointRounding.AwayFromZero);
}
