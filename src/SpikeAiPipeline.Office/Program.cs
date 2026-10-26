// The Open XML CLI of spike-ai-pipeline. Its commands arrive with the office-cli task; until then
// it answers --version, which the processing layer's adapter checks.
using SpikeAiPipeline.Office;

if (args is ["--version"])
{
    Console.WriteLine(Cli.Version);
    return 0;
}

Console.Error.WriteLine("usage: SpikeAiPipeline.Office --version");
return 2;
