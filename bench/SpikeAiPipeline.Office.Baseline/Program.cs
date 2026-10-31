using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using SpikeAiPipeline.Office.Baseline;

// The baseline tier: office over the committed pool, nothing else, its Markdown scored against the
// ground truth and each run measured. `mise run baseline` (scripts/baseline.sh) builds and runs it
// on the laptop, never in CI, and the results go to a new directory of
// docs/findings/office-cli/runs/. office calls no model. The runs before schema 3 also ran
// markitdown and xberg; that comparison is closed, its bench in the history at 1f209b5.
//
// Usage: SpikeAiPipeline.Office.Baseline --root <repo> --office <binary> --rusage <wrapper>
//   --out <runs-dir> [--runs 5] [--timeout 60]
// or, to see the texts the scoring reads in one Markdown file: SpikeAiPipeline.Office.Baseline
//   --dump <file.md>
// or, to score one Markdown file against a truth: SpikeAiPipeline.Office.Baseline --score
//   <truth.json> <docx|xlsx|pptx> <file.md>

if (args is ["--dump", var dumpPath])
{
    var dumped = Rendered.Of(File.ReadAllText(dumpPath));
    Console.WriteLine(string.Join("\n", dumped.Leaves.Select(l => "leaf: " + l)));
    return;
}
if (args is ["--score", var truthPath, var format, var markdownPath])
{
    var scored = Fidelity.Score(Expected.Of(JsonNode.Parse(File.ReadAllText(truthPath))!), Rendered.Of(File.ReadAllText(markdownPath)), format);
    foreach (var (kind, score) in scored.Where(s => s.Value.Expected > 0))
    {
        Console.WriteLine($"{kind}: {score.Found} / {score.Expected}{(score.Missed.Count > 0 ? "; missed " + string.Join("; ", score.Missed) : "")}");
    }
    return;
}
var options = new Dictionary<string, string>();
for (var i = 0; i + 1 < args.Length; i += 2)
{
    options[args[i].TrimStart('-')] = args[i + 1];
}
string Option(string name) => options.TryGetValue(name, out var value) ? value : throw new ArgumentException($"--{name} is required");
var root = Path.GetFullPath(Option("root"));
var runs = int.Parse(options.GetValueOrDefault("runs", "5"), CultureInfo.InvariantCulture);
var timeout = int.Parse(options.GetValueOrDefault("timeout", "60"), CultureInfo.InvariantCulture);
var office = Path.GetFullPath(Option("office"));
var rusage = Path.GetFullPath(Option("rusage"));
var outRoot = Path.GetFullPath(Option("out"));

var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
string Tilde(string path) => path.StartsWith(home + "/", StringComparison.Ordinal) ? "~" + path[home.Length..] : path;

var version = Runner.Output(office, "--version");
string[] Arguments(string file) => [file, "--format", "markdown"];

var started = DateTimeOffset.Now;
var work = Directory.CreateTempSubdirectory("office-baseline-");
var runner = new Runner(rusage, work.FullName, timeout);
var pool = Pool.Files(root);
Console.Error.WriteLine($"baseline: {pool.Count} pool files, {runs} runs each, timeout {timeout} s");

// The wrapper's own floor: /bin/true through it, the least a measured peak can be.
var floor = Enumerable.Range(0, runs).Select(_ => runner.Once("/bin/true", []).Run).ToList();

var fileResults = new JsonArray();
// The fidelity is summed three ways: over the whole pool, over the generated set, and over the
// real-world set, the files Word, Excel, and PowerPoint wrote, which office's Markdown wasn't
// built around as it was around the generated set's features.
string[] subsets = ["pooled", "generated", "realworld"];
var totals = subsets.ToDictionary(s => s, _ => Fidelity.Kinds.ToDictionary(k => k, _ => (Files: 0, Expected: 0, Found: 0)));
var measuredFiles = new List<(PoolFile File, List<Run> Runs, string Status)>();
foreach (var file in pool)
{
    var truth = JsonNode.Parse(File.ReadAllText(Path.Combine(root, file.TruthPath)))!;
    var expected = Expected.Of(truth);
    var path = Path.Combine(root, file.Path);
    var measured = new List<Run>();
    string? first = null;
    string firstStderr = "";
    var outputs = new HashSet<string>();
    for (var r = 0; r < runs; r++)
    {
        var (run, stdout, stderr) = runner.Once(office, Arguments(path));
        measured.Add(run);
        outputs.Add(Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(stdout))));
        if (r == 0)
        {
            first = stdout;
            firstStderr = stderr;
        }
    }
    var status = measured[0] switch
    {
        { Status: "timeout" } => "timeout",
        { Status: "exited", Code: 0 } => "ok",
        { Status: "exited" } => "failed",
        _ => "signaled",
    };
    measuredFiles.Add((file, measured, status));
    // The first run's Markdown is scored; a run that failed or timed out is scored on what it
    // wrote, usually nothing.
    var scores = Fidelity.Score(expected, Rendered.Of(first ?? ""), file.Format);
    var kinds = new JsonObject();
    foreach (var (kind, score) in scores)
    {
        if (score.Expected == 0)
        {
            continue;
        }
        foreach (var subset in new[] { "pooled", file.Set })
        {
            var total = totals[subset][kind];
            totals[subset][kind] = (total.Files + 1, total.Expected + score.Expected, total.Found + score.Found);
        }
        kinds[kind] = new JsonObject
        {
            ["expected"] = score.Expected,
            ["found"] = score.Found,
            ["missed"] = new JsonArray([.. score.Missed.Select(m => (JsonNode)m)]),
        };
    }
    var walls = measured.Select(m => m.WallMs).ToList();
    fileResults.Add(new JsonObject
    {
        ["path"] = file.Path,
        ["set"] = file.Set,
        ["format"] = file.Format,
        ["truth"] = file.TruthPath,
        ["status"] = status,
        ["exitCode"] = measured[0].Code,
        ["stderr"] = status == "ok" ? null : Truncate(firstStderr.Replace(home, "~", StringComparison.Ordinal), 600),
        ["markdownBytes"] = Encoding.UTF8.GetByteCount(first ?? ""),
        ["sameOutputEveryRun"] = outputs.Count == 1,
        ["firstMs"] = Stats.Round(walls[0]),
        ["medianMs"] = Stats.Round(Stats.Median(walls)),
        ["minMs"] = Stats.Round(walls.Min()),
        ["maxMs"] = Stats.Round(walls.Max()),
        ["peakRssKiB"] = measured.Max(m => m.MaxRssKiB),
        ["medianRssKiB"] = Stats.Round(Stats.Median(measured.Select(m => (double)m.MaxRssKiB)), 0),
        ["runs"] = new JsonArray([.. measured.Select(m => (JsonNode)new JsonObject
        {
            ["status"] = m.Status,
            ["code"] = m.Code,
            ["wallMs"] = Stats.Round(m.WallMs, 3),
            ["userMs"] = Stats.Round(m.UserMs, 3),
            ["sysMs"] = Stats.Round(m.SysMs, 3),
            ["maxRssKiB"] = m.MaxRssKiB,
        })]),
        ["fidelity"] = kinds,
    });
    Console.Error.WriteLine($"baseline: {file.Path} {status} median {Stats.Median(walls):F1} ms");
}
var leftBehind = work.EnumerateFileSystemInfos().Select(f => f.Name).Where(n => n is not "stdout" and not "stderr").Order(StringComparer.Ordinal).ToList();
work.Delete(recursive: true);

var fidelity = new JsonObject();
foreach (var subset in subsets)
{
    var rows = new JsonArray();
    foreach (var kind in Fidelity.Kinds)
    {
        var (files, expectedCount, found) = totals[subset][kind];
        rows.Add(new JsonObject
        {
            ["kind"] = kind,
            ["files"] = files,
            ["expected"] = expectedCount,
            ["found"] = found,
            ["score"] = expectedCount == 0 ? null : Stats.Round((double)found / expectedCount, 3),
        });
    }
    fidelity[subset] = rows;
}

var medians = measuredFiles.Select(r => Stats.Median(r.Runs.Select(m => m.WallMs))).ToList();
var firsts = measuredFiles.Select(r => r.Runs[0].WallMs).ToList();
var colds = measuredFiles.Select(r => r.Runs[0].WallMs - Stats.Median(r.Runs.Skip(1).Select(m => m.WallMs))).ToList();
var spreads = measuredFiles.Select(r => r.Runs.Max(m => m.WallMs) - r.Runs.Min(m => m.WallMs)).ToList();
var peaks = measuredFiles.Select(r => (double)r.Runs.Max(m => m.MaxRssKiB)).ToList();
var performance = new JsonObject
{
    ["files"] = measuredFiles.Count,
    ["ok"] = measuredFiles.Count(r => r.Status == "ok"),
    ["failed"] = measuredFiles.Count(r => r.Status == "failed"),
    ["timedOut"] = measuredFiles.Count(r => r.Status == "timeout"),
    ["signaled"] = measuredFiles.Count(r => r.Status == "signaled"),
    ["firstInvocationMs"] = Stats.Round(measuredFiles[0].Runs[0].WallMs),
    ["medianOfMediansMs"] = Stats.Round(Stats.Median(medians)),
    ["minMedianMs"] = Stats.Round(medians.Min()),
    ["maxMedianMs"] = Stats.Round(medians.Max()),
    ["sumOfMediansMs"] = Stats.Round(medians.Sum()),
    ["medianFirstRunMs"] = Stats.Round(Stats.Median(firsts)),
    ["medianFirstRunOverRestMs"] = runs > 1 ? Stats.Round(Stats.Median(colds)) : null,
    ["medianSpreadMs"] = Stats.Round(Stats.Median(spreads)),
    ["medianPeakRssMiB"] = Stats.Round(Stats.Median(peaks) / 1024),
    ["minPeakRssMiB"] = Stats.Round(peaks.Min() / 1024),
    ["maxPeakRssMiB"] = Stats.Round(peaks.Max() / 1024),
    ["maxPeakRssFile"] = measuredFiles[peaks.IndexOf(peaks.Max())].File.Path,
};

var finished = DateTimeOffset.Now;
var results = new JsonObject
{
    // Schema 3: office alone, so its tool, performance, and footprint are one object each, and
    // each file's results are on the file. Schemas 1 and 2 (runs 2026-10-31-1 and -2) held each
    // of three tools'.
    ["schema"] = 3,
    ["started"] = started.ToString("o", CultureInfo.InvariantCulture),
    ["finished"] = finished.ToString("o", CultureInfo.InvariantCulture),
    ["host"] = Footprint.Host(),
    ["tool"] = new JsonObject
    {
        ["name"] = "office",
        ["version"] = version,
        ["command"] = string.Join(" ", new[] { Tilde(office) }.Concat(Arguments("<file>"))),
    },
    ["markdig"] = typeof(Markdig.Markdown).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
    ["settings"] = new JsonObject { ["runs"] = runs, ["timeoutSeconds"] = timeout },
    ["pool"] = new JsonObject
    {
        ["files"] = pool.Count,
        ["generated"] = pool.Count(f => f.Set == "generated"),
        ["realworld"] = pool.Count(f => f.Set == "realworld"),
        ["docx"] = pool.Count(f => f.Format == "docx"),
        ["xlsx"] = pool.Count(f => f.Format == "xlsx"),
        ["pptx"] = pool.Count(f => f.Format == "pptx"),
    },
    ["wrapperFloor"] = new JsonObject
    {
        ["program"] = "/bin/true",
        ["medianMs"] = Stats.Round(Stats.Median(floor.Select(f => f.WallMs)), 3),
        ["maxRssKiB"] = floor.Max(f => f.MaxRssKiB),
    },
    ["workDirectoryLeftBehind"] = new JsonArray([.. leftBehind.Select(n => (JsonNode)n)]),
    ["fidelity"] = fidelity,
    ["performance"] = performance,
    ["footprint"] = Footprint.Office(root, office),
    ["files"] = fileResults,
};

var day = Footprint.Day(started);
var n = 1;
while (Directory.Exists(Path.Combine(outRoot, $"{day}-{n}")))
{
    n++;
}
var dir = Directory.CreateDirectory(Path.Combine(outRoot, $"{day}-{n}"));
var output = Path.Combine(dir.FullName, "results.json");
File.WriteAllText(output, results.ToJsonString(new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }) + "\n");
Console.Error.WriteLine($"baseline: wrote {Path.GetRelativePath(root, output)}");
foreach (var subset in new[] { "pooled", "realworld" })
{
    Console.Error.WriteLine($"baseline: {subset}");
    foreach (var row in fidelity[subset]!.AsArray())
    {
        Console.Error.WriteLine($"  {row!["kind"],-15} {row["found"],6} / {row["expected"],-6} {row["score"]}");
    }
}

// The end of a long text, where a traceback's exception is.
static string Truncate(string text, int length) => text.Length <= length ? text : "..." + text[^length..];
