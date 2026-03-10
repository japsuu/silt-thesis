using Silt.Metrics;

namespace Silt;

/// <summary>
/// Parsed application options from command line args.
/// </summary>
public sealed class AppOptions
{
    public bool BenchmarkEnabled { get; init; }
    public string? BenchmarkSceneId { get; init; }
    public string? BenchmarkOutputFilePath { get; init; }

    public double BenchmarkWarmUpMeshingSeconds { get; init; }
    public double BenchmarkSampleMeshingSeconds { get; init; }
    
    public int BenchmarkBatchRemeshWarmupIterations { get; init; }
    public int BenchmarkBatchRemeshSampleIterations { get; init; }

    public double BenchmarkWarmUpRenderingSeconds { get; init; }
    public double BenchmarkSampleRenderingSeconds { get; init; }
    
    public ProfilePhase? TargetProfilePhase { get; init; }
}