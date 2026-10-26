namespace SpikeAiPipeline.Office.Tests;

public class CliTests
{
    [Fact]
    public void VersionIsSet() => Assert.StartsWith("0.0.0", Cli.Version, StringComparison.Ordinal);
}
