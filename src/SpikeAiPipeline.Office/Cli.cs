using System.Reflection;

namespace SpikeAiPipeline.Office;

/// <summary>The CLI's identity.</summary>
public static class Cli
{
    /// <summary>The CLI's version, from its assembly.</summary>
    public static string Version =>
        typeof(Cli).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown";
}
