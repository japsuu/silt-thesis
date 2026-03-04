using System.Globalization;
using Serilog;

namespace Silt.Metrics;

public readonly struct BenchmarkConfig(
    string outputFilePath,
    Action? onComplete,
    double warmUpMeshingSeconds = 10.0,
    double sampleMeshingSeconds = 10.0,
    double warmUpRenderingSeconds = 10.0,
    double sampleRenderingSeconds = 30.0)
{
    public readonly string OutputFilePath = outputFilePath;
    public readonly Action? OnComplete = onComplete;

    public readonly double WarmUpMeshingSeconds = warmUpMeshingSeconds;
    public readonly double SampleMeshingSeconds = sampleMeshingSeconds;
    public readonly double WarmUpRenderingSeconds = warmUpRenderingSeconds;
    public readonly double SampleRenderingSeconds = sampleRenderingSeconds;
}

public enum BenchmarkState
{
    NotStarted,
    MeshingWarmup,
    MeshingSample,
    RenderingWarmup,
    RenderingSample,
    Complete
}

public sealed class BenchmarkRun
{
    public BenchmarkConfig Config { get; set; }

    public BenchmarkState State { get; private set; } = BenchmarkState.NotStarted;

    public bool IsComplete => State == BenchmarkState.Complete;

    /// <summary>
    /// Fired whenever the benchmark transitions to a new state.
    /// </summary>
    public event Action<BenchmarkState>? OnStateChanged;

    // Rendering (frame time) stats during RenderingSample
    public double RenderingFrameMsAvg { get; private set; }
    public double RenderingFrameMsMin { get; private set; }
    public double RenderingFrameMsMax { get; private set; }
    public double RenderingTotalTimeMs { get; private set; }
    public int RenderingSampleFrameCount { get; private set; }

    // Phase timing and counts for reporting
    public double WarmUpMeshingTimeMs { get; private set; }
    public double SampleMeshingTimeMs { get; private set; }
    public double WarmUpRenderingTimeMs { get; private set; }
    public double SampleRenderingTimeMs { get; private set; }

    public int WarmUpMeshingFrameCount { get; private set; }
    public int SampleMeshingFrameCount { get; private set; }
    public int WarmUpRenderingFrameCount { get; private set; }
    public int SampleRenderingFrameCount { get; private set; }
    
    public double TotalTimeMs { get; private set; }

    private readonly double _warmUpMeshingTargetMs;
    private readonly double _sampleMeshingTargetMs;
    private readonly double _warmUpRenderingTargetMs;
    private readonly double _sampleRenderingTargetMs;

    // Preallocated storage for rendering samples and scratch for percentile.
    private readonly double[] _renderingSamplesMs;
    private readonly double[] _renderingScratchMs;

    private const int MAX_SAMPLES_PER_SECOND = 3072;


    public BenchmarkRun(BenchmarkConfig config)
    {
        Config = config;

        RenderingFrameMsAvg = 0;
        RenderingFrameMsMin = double.MaxValue;
        RenderingFrameMsMax = double.MinValue;
        RenderingTotalTimeMs = 0;
        RenderingSampleFrameCount = 0;

        WarmUpMeshingTimeMs = 0;
        SampleMeshingTimeMs = 0;
        WarmUpRenderingTimeMs = 0;
        SampleRenderingTimeMs = 0;

        WarmUpMeshingFrameCount = 0;
        SampleMeshingFrameCount = 0;
        WarmUpRenderingFrameCount = 0;
        SampleRenderingFrameCount = 0;
        
        TotalTimeMs = 0;

        _warmUpMeshingTargetMs = Math.Max(0, Config.WarmUpMeshingSeconds) * 1000.0;
        _sampleMeshingTargetMs = Math.Max(0, Config.SampleMeshingSeconds) * 1000.0;
        _warmUpRenderingTargetMs = Math.Max(0, Config.WarmUpRenderingSeconds) * 1000.0;
        _sampleRenderingTargetMs = Math.Max(0, Config.SampleRenderingSeconds) * 1000.0;

        _renderingSamplesMs = new double[(int)(Math.Max(0, Config.SampleRenderingSeconds) * MAX_SAMPLES_PER_SECOND)];
        _renderingScratchMs = new double[_renderingSamplesMs.Length];

        TransitionTo(BenchmarkState.MeshingWarmup);
    }


    public void Update(double frameMs)
    {
        if (double.IsNaN(frameMs) || double.IsInfinity(frameMs) || frameMs <= 0)
            return;

        if (IsComplete)
            return;
        
        TotalTimeMs += frameMs;

        switch (State)
        {
            case BenchmarkState.MeshingWarmup:
                WarmUpMeshingTimeMs += frameMs;
                WarmUpMeshingFrameCount++;
                if (WarmUpMeshingTimeMs >= _warmUpMeshingTargetMs)
                    TransitionTo(BenchmarkState.MeshingSample);
                break;

            case BenchmarkState.MeshingSample:
                SampleMeshingTimeMs += frameMs;
                SampleMeshingFrameCount++;
                if (SampleMeshingTimeMs >= _sampleMeshingTargetMs)
                    TransitionTo(BenchmarkState.RenderingWarmup);
                break;

            case BenchmarkState.RenderingWarmup:
                WarmUpRenderingTimeMs += frameMs;
                WarmUpRenderingFrameCount++;
                if (WarmUpRenderingTimeMs >= _warmUpRenderingTargetMs)
                    TransitionTo(BenchmarkState.RenderingSample);
                break;

            case BenchmarkState.RenderingSample:
            {
                // Record rendering sample
                _renderingSamplesMs[RenderingSampleFrameCount] = frameMs;
                RenderingTotalTimeMs += frameMs;
                SampleRenderingTimeMs += frameMs;
                RenderingSampleFrameCount++;
                SampleRenderingFrameCount++;

                // Update rendering aggregates
                RenderingFrameMsMin = Math.Min(RenderingFrameMsMin, frameMs);
                RenderingFrameMsMax = Math.Max(RenderingFrameMsMax, frameMs);
                RenderingFrameMsAvg = RenderingSampleFrameCount > 0 ? RenderingTotalTimeMs / RenderingSampleFrameCount : 0;

                if (SampleRenderingTimeMs >= _sampleRenderingTargetMs)
                {
                    CompleteAndWriteResults();
                    TransitionTo(BenchmarkState.Complete);
                }

                break;
            }

            case BenchmarkState.NotStarted:
            case BenchmarkState.Complete:
            default:
                break;
        }
    }


    private void TransitionTo(BenchmarkState newState)
    {
        if (State == newState)
            return;

        State = newState;
        OnStateChanged?.Invoke(newState);
    }


    private void CompleteAndWriteResults()
    {
        double renderingP99 = RenderingSampleFrameCount > 0
            ? PercentileHelper.P99FromSamples(_renderingSamplesMs, _renderingScratchMs, RenderingSampleFrameCount)
            : 0;

        MeshingStats meshing = PerfMonitor.BenchmarkChunkMeshing!;
        string meshingBlock = meshing.FormatInvariant("chunk_meshing");

        string output = $"mode=benchmark\n" +
                        $"rendering_warmup_seconds={Config.WarmUpRenderingSeconds.ToString("F4", CultureInfo.InvariantCulture)}\n" +
                        $"rendering_warmup_frames={WarmUpRenderingFrameCount}\n" +
                        $"rendering_sample_seconds={Config.SampleRenderingSeconds.ToString("F4", CultureInfo.InvariantCulture)}\n" +
                        $"rendering_sample_frames={SampleRenderingFrameCount}\n" +
                        $"rendering_frame_ms_avg={RenderingFrameMsAvg.ToString("F4", CultureInfo.InvariantCulture)}\n" +
                        $"rendering_frame_ms_min={(RenderingSampleFrameCount > 0 ? RenderingFrameMsMin : 0).ToString("F4", CultureInfo.InvariantCulture)}\n" +
                        $"rendering_frame_ms_max={(RenderingSampleFrameCount > 0 ? RenderingFrameMsMax : 0).ToString("F4", CultureInfo.InvariantCulture)}\n" +
                        $"rendering_frame_ms_p99={renderingP99.ToString("F4", CultureInfo.InvariantCulture)}\n" +
                        $"rendering_time_total_ms={SampleRenderingTimeMs.ToString("F4", CultureInfo.InvariantCulture)}\n" +
                        "\n" +
                        $"meshing_warmup_seconds={Config.WarmUpMeshingSeconds.ToString("F4", CultureInfo.InvariantCulture)}\n" +
                        $"meshing_warmup_frames={WarmUpMeshingFrameCount}\n" +
                        $"meshing_sample_seconds={Config.SampleMeshingSeconds.ToString("F4", CultureInfo.InvariantCulture)}\n" +
                        $"meshing_sample_frames={SampleMeshingFrameCount}\n" +
                        meshingBlock +
                        $"meshing_time_total_ms={SampleMeshingTimeMs.ToString("F4", CultureInfo.InvariantCulture)}\n" +
                        "\n" +
                        $"total_time_ms={TotalTimeMs.ToString("F4", CultureInfo.InvariantCulture)}\n";

        File.WriteAllText(Config.OutputFilePath, output);
        string fullPath = Path.GetFullPath(Config.OutputFilePath);
        Log.Information("Benchmark complete. Results written to {OutputFilePath}", fullPath);
        Config.OnComplete?.Invoke();
    }
}