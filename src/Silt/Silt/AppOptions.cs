namespace Silt;

/// <summary>
/// Parsed application options from command line args.
/// </summary>
public sealed class AppOptions
{
    public bool BenchmarkEnabled { get; init; }
    public string? BenchmarkSceneId { get; init; }
    public string? BenchmarkOutputFilePath { get; init; }

    public double BenchmarkWarmUpSeconds { get; init; }
    public double BenchmarkSampleSeconds { get; init; }
}
