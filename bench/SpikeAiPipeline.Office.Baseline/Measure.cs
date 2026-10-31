using System.Diagnostics;
using System.Globalization;

namespace SpikeAiPipeline.Office.Baseline;

/// <summary>One run of office, as the rusage wrapper reports it: how it ended (<c>exited</c>,
/// <c>signaled</c>, or <c>timeout</c>) with its exit code or signal, its peak resident memory, and
/// its wall, user, and system time.</summary>
internal sealed record Run(string Status, int Code, long MaxRssKiB, double WallMs, double UserMs, double SysMs);

/// <summary>Runs a program through <c>bench/rusage.c</c>, the small wrapper whose child's
/// <c>wait4</c> rusage is the program's own (the wrapper's comment says why it can't be the bench
/// itself), with stdout and stderr captured to files.</summary>
internal sealed class Runner(string rusage, string work, int timeoutSeconds)
{
    /// <summary>Runs the program once, in the work directory, with the document's path among its
    /// arguments, and returns the run with what it wrote to stdout and stderr.</summary>
    public (Run Run, string Stdout, string Stderr) Once(string program, IEnumerable<string> arguments)
    {
        var stdout = Path.Combine(work, "stdout");
        var stderr = Path.Combine(work, "stderr");
        var start = new ProcessStartInfo(rusage)
        {
            WorkingDirectory = work,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var argument in new[] { timeoutSeconds.ToString(CultureInfo.InvariantCulture), stdout, stderr, program }.Concat(arguments))
        {
            start.ArgumentList.Add(argument);
        }
        using var process = Process.Start(start) ?? throw new InvalidOperationException("rusage didn't start");
        var report = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"rusage failed ({process.ExitCode}): {error.Trim()}");
        }
        var fields = report.Trim().Split(' ').Select(f => f.Split('=', 2)).ToDictionary(f => f[0], f => f[1]);
        var run = new Run(
            fields["status"],
            int.Parse(fields["code"], CultureInfo.InvariantCulture),
            long.Parse(fields["maxrss_kib"], CultureInfo.InvariantCulture),
            long.Parse(fields["wall_ns"], CultureInfo.InvariantCulture) / 1e6,
            long.Parse(fields["user_us"], CultureInfo.InvariantCulture) / 1e3,
            long.Parse(fields["sys_us"], CultureInfo.InvariantCulture) / 1e3);
        return (run, File.ReadAllText(stdout), File.ReadAllText(stderr));
    }

    /// <summary>Runs a program directly and returns its stdout, trimmed: for a version.</summary>
    public static string Output(string program, params string[] arguments)
    {
        var start = new ProcessStartInfo(program) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }
        using var process = Process.Start(start) ?? throw new InvalidOperationException($"{program} didn't start");
        var output = process.StandardOutput.ReadToEnd();
        process.StandardError.ReadToEnd();
        process.WaitForExit();
        return output.Trim();
    }
}

/// <summary>Medians and spreads of runs.</summary>
internal static class Stats
{
    public static double Median(IEnumerable<double> values)
    {
        var sorted = values.Order().ToList();
        if (sorted.Count == 0)
        {
            return double.NaN;
        }
        var middle = sorted.Count / 2;
        return sorted.Count % 2 == 1 ? sorted[middle] : (sorted[middle - 1] + sorted[middle]) / 2;
    }

    public static double Round(double value, int digits = 2) => Math.Round(value, digits, MidpointRounding.AwayFromZero);
}
