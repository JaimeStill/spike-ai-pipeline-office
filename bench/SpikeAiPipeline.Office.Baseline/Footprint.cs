using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.Json.Nodes;
using System.Xml.Linq;

namespace SpikeAiPipeline.Office.Baseline;

/// <summary>What each tool takes on disk and the licenses it ships under, read from the files
/// installed on this machine, and the host the run measured on.</summary>
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

    /// <summary>markitdown's virtual environment, everything its install put on disk (its
    /// packages, the extras' among them), and the Python interpreter it runs on, which the
    /// environment links to rather than holds; then each installed package's license, from its
    /// metadata.</summary>
    public static JsonObject Markitdown(string executable)
    {
        var venv = Path.GetDirectoryName(Path.GetDirectoryName(Resolve(executable)))!;
        var python = Path.Combine(venv, "bin", "python");
        var stdlib = Runner.Output(python, "-I", "-c", "import sysconfig; print(sysconfig.get_paths()['stdlib'])");
        var libpython = Runner.Output(python, "-I", "-c", "import os, sysconfig; print(os.path.join(sysconfig.get_config_var('LIBDIR'), sysconfig.get_config_var('LDLIBRARY')))");
        var interpreter = Resolve(python);
        var stdlibBytes = Size(stdlib, skip: ["site-packages", "dist-packages"]);
        var interpreterBytes = new FileInfo(interpreter).Length + (File.Exists(libpython) ? new FileInfo(Resolve(libpython)).Length : 0);
        var packages = new JsonArray();
        var sitePackages = Directory.GetDirectories(Path.Combine(venv, "lib")).Select(d => Path.Combine(d, "site-packages")).First(Directory.Exists);
        foreach (var info in Directory.GetDirectories(sitePackages, "*.dist-info").Order(StringComparer.Ordinal))
        {
            var metadata = File.ReadAllLines(Path.Combine(info, "METADATA")).TakeWhile(l => l.Length > 0).ToList();
            string? Field(string name) => metadata.Where(l => l.StartsWith(name + ": ", StringComparison.Ordinal)).Select(l => l[(name.Length + 2)..].Trim()).FirstOrDefault();
            var license = Field("License-Expression")
                ?? (Field("License") is { Length: > 0 and <= 60 } brief ? brief : null)
                ?? metadata.Where(l => l.StartsWith("Classifier: License :: ", StringComparison.Ordinal)).Select(l => l["Classifier: License :: ".Length..]).FirstOrDefault()
                ?? (Field("License") is { Length: > 0 } longer ? longer.Split('\n')[0] : null);
            packages.Add(new JsonObject { ["package"] = Field("Name"), ["version"] = Field("Version"), ["license"] = license });
        }
        return new JsonObject
        {
            ["venv"] = venv,
            ["venvBytes"] = Size(venv),
            ["interpreter"] = interpreter,
            ["interpreterBytes"] = interpreterBytes,
            ["stdlib"] = stdlib,
            ["stdlibBytes"] = stdlibBytes,
            ["packages"] = packages,
        };
    }

    /// <summary>xberg's binary, and its install directory, which holds the shared libraries the
    /// binary loads beside it and its license files.</summary>
    public static JsonObject Xberg(string executable)
    {
        var binary = Resolve(executable);
        var directory = Path.GetDirectoryName(binary)!;
        return new JsonObject
        {
            ["binary"] = binary,
            ["binaryBytes"] = new FileInfo(binary).Length,
            ["installDirectory"] = directory,
            ["installBytes"] = Size(directory),
            ["installFiles"] = new JsonArray([.. Directory.GetFiles(directory).Order(StringComparer.Ordinal).Select(f => (JsonNode)new JsonObject
            {
                ["file"] = Path.GetFileName(f),
                ["bytes"] = new FileInfo(f).Length,
            })]),
            ["license"] = File.Exists(Path.Combine(directory, "LICENSE")) ? FirstLine(Path.Combine(directory, "LICENSE")) : null,
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

    // The bytes of the regular files under a directory, symbolic links not followed, so a file
    // a link names elsewhere isn't counted; the named subdirectories skipped at any depth.
    private static long Size(string directory, string[]? skip = null)
    {
        long bytes = 0;
        var pending = new Stack<DirectoryInfo>([new DirectoryInfo(directory)]);
        while (pending.TryPop(out var dir))
        {
            foreach (var entry in dir.EnumerateFileSystemInfos())
            {
                if (entry.LinkTarget is not null)
                {
                    continue;
                }
                if (entry is DirectoryInfo sub)
                {
                    if (skip is null || !skip.Contains(sub.Name))
                    {
                        pending.Push(sub);
                    }
                }
                else if (entry is FileInfo file)
                {
                    bytes += file.Length;
                }
            }
        }
        return bytes;
    }

    private static string Resolve(string path) =>
        new FileInfo(path).ResolveLinkTarget(returnFinalTarget: true)?.FullName ?? Path.GetFullPath(path);

    private static string FirstLine(string path) =>
        File.ReadLines(path).Select(l => l.Trim()).FirstOrDefault(l => l.Length > 0) ?? "";

    /// <summary>A byte count in MiB, to two places, for the summary.</summary>
    public static double MiB(long bytes) => Math.Round(bytes / 1048576.0, 2, MidpointRounding.AwayFromZero);

    /// <summary>A date as the run's directory names it.</summary>
    public static string Day(DateTimeOffset time) => time.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
