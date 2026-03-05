using System.Globalization;
using Serilog;

namespace Silt.Metrics;

public readonly struct BenchmarkConfig(
    string outputFilePath,
    Action? onComplete,
    double warmUpMeshingSeconds = 10.0,
    double sampleMeshingSeconds = 30.0,
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

    // Frame time stats during MeshingSample
    public double MeshingFrameMsAvg { get; private set; }
    public double MeshingFrameMsMin { get; private set; }
    public double MeshingFrameMsMax { get; private set; }
    public double MeshingTotalTimeMs { get; private set; }

    // Rendering (frame time) stats during RenderingSample
    public double RenderingFrameMsAvg { get; private set; }
    public double RenderingFrameMsMin { get; private set; }
    public double RenderingFrameMsMax { get; private set; }
    public double RenderingTotalTimeMs { get; private set; }
    public int RenderingSampleFrameCount { get; private set; }

    // Phase timing and counts for reporting
    public double MeshingWarmUpTimeMs { get; private set; }
    public double MeshingSampleTimeMs { get; private set; }
    public double RenderingWarmUpTimeMs { get; private set; }
    public double RenderingSampleTimeMs { get; private set; }

    public int MeshingWarmUpFrameCount { get; private set; }
    public int MeshingSampleFrameCount { get; private set; }
    public int RenderingWarmUpFrameCount { get; private set; }
    
    public double TotalTimeMs { get; private set; }

    private readonly double _warmUpMeshingTargetMs;
    private readonly double _sampleMeshingTargetMs;
    private readonly double _warmUpRenderingTargetMs;
    private readonly double _sampleRenderingTargetMs;

    // Preallocated storage for meshing samples and scratch for percentile.
    private readonly double[] _meshingSamplesMs;
    private readonly double[] _meshingScratchMs;

    // Preallocated storage for rendering samples and scratch for percentile.
    private readonly double[] _renderingSamplesMs;
    private readonly double[] _renderingScratchMs;

    private const int MAX_SAMPLES_PER_SECOND = 3072;


    public BenchmarkRun(BenchmarkConfig config)
    {
        Config = config;

        MeshingFrameMsAvg = 0;
        MeshingFrameMsMin = double.MaxValue;
        MeshingFrameMsMax = double.MinValue;
        MeshingTotalTimeMs = 0;

        RenderingFrameMsAvg = 0;
        RenderingFrameMsMin = double.MaxValue;
        RenderingFrameMsMax = double.MinValue;
        RenderingTotalTimeMs = 0;

        MeshingWarmUpTimeMs = 0;
        MeshingSampleTimeMs = 0;
        RenderingWarmUpTimeMs = 0;
        RenderingSampleTimeMs = 0;

        MeshingWarmUpFrameCount = 0;
        MeshingSampleFrameCount = 0;
        RenderingWarmUpFrameCount = 0;
        RenderingSampleFrameCount = 0;
        
        TotalTimeMs = 0;

        _warmUpMeshingTargetMs = Math.Max(0, Config.WarmUpMeshingSeconds) * 1000.0;
        _sampleMeshingTargetMs = Math.Max(0, Config.SampleMeshingSeconds) * 1000.0;
        _warmUpRenderingTargetMs = Math.Max(0, Config.WarmUpRenderingSeconds) * 1000.0;
        _sampleRenderingTargetMs = Math.Max(0, Config.SampleRenderingSeconds) * 1000.0;

        _meshingSamplesMs = new double[(int)(Math.Max(0, Config.SampleMeshingSeconds) * MAX_SAMPLES_PER_SECOND)];
        _meshingScratchMs = new double[_meshingSamplesMs.Length];

        _renderingSamplesMs = new double[(int)(Math.Max(0, Config.SampleRenderingSeconds) * MAX_SAMPLES_PER_SECOND)];
        _renderingScratchMs = new double[_renderingSamplesMs.Length];
    }
    
    
    public void Start()
    {
        if (State != BenchmarkState.NotStarted)
            throw new InvalidOperationException("BenchmarkRun can only be started once.");

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
                MeshingWarmUpTimeMs += frameMs;
                MeshingWarmUpFrameCount++;
                if (MeshingWarmUpTimeMs >= _warmUpMeshingTargetMs)
                    TransitionTo(BenchmarkState.MeshingSample);
                break;

            case BenchmarkState.MeshingSample:
                // Record meshing frame sample
                _meshingSamplesMs[MeshingSampleFrameCount] = frameMs;
                MeshingTotalTimeMs += frameMs;
                MeshingSampleTimeMs += frameMs;
                MeshingSampleFrameCount++;

                // Update meshing frame time aggregates
                MeshingFrameMsMin = Math.Min(MeshingFrameMsMin, frameMs);
                MeshingFrameMsMax = Math.Max(MeshingFrameMsMax, frameMs);
                MeshingFrameMsAvg = MeshingSampleFrameCount > 0 ? MeshingTotalTimeMs / MeshingSampleFrameCount : 0;

                if (MeshingSampleTimeMs >= _sampleMeshingTargetMs)
                    TransitionTo(BenchmarkState.RenderingWarmup);
                break;

            case BenchmarkState.RenderingWarmup:
                RenderingWarmUpTimeMs += frameMs;
                RenderingWarmUpFrameCount++;
                if (RenderingWarmUpTimeMs >= _warmUpRenderingTargetMs)
                    TransitionTo(BenchmarkState.RenderingSample);
                break;

            case BenchmarkState.RenderingSample:
            {
                // Record rendering sample
                _renderingSamplesMs[RenderingSampleFrameCount] = frameMs;
                RenderingTotalTimeMs += frameMs;
                RenderingSampleTimeMs += frameMs;
                RenderingSampleFrameCount++;

                // Update rendering aggregates
                RenderingFrameMsMin = Math.Min(RenderingFrameMsMin, frameMs);
                RenderingFrameMsMax = Math.Max(RenderingFrameMsMax, frameMs);
                RenderingFrameMsAvg = RenderingSampleFrameCount > 0 ? RenderingTotalTimeMs / RenderingSampleFrameCount : 0;

                if (RenderingSampleTimeMs >= _sampleRenderingTargetMs)
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
        double meshingP99 = MeshingSampleFrameCount > 0
            ? PercentileHelper.P99FromSamples(_meshingSamplesMs, _meshingScratchMs, MeshingSampleFrameCount)
            : 0;

        double renderingP99 = RenderingSampleFrameCount > 0
            ? PercentileHelper.P99FromSamples(_renderingSamplesMs, _renderingScratchMs, RenderingSampleFrameCount)
            : 0;

        MeshingStats meshing = PerfMonitor.BenchmarkChunkMeshing!;
        string meshingBlock = meshing.FormatInvariant("meshing_chunk");

        string output = $"mode=benchmark\n" +
                        $"rendering_target_warmup_seconds={Config.WarmUpRenderingSeconds.ToString("F4", CultureInfo.InvariantCulture)}\n" +
                        $"rendering_warmup_frames={RenderingWarmUpFrameCount}\n" +
                        $"rendering_target_sample_seconds={Config.SampleRenderingSeconds.ToString("F4", CultureInfo.InvariantCulture)}\n" +
                        $"rendering_sample_frames={RenderingSampleFrameCount}\n" +
                        $"rendering_frame_ms_avg={RenderingFrameMsAvg.ToString("F4", CultureInfo.InvariantCulture)}\n" +
                        $"rendering_frame_ms_min={(RenderingSampleFrameCount > 0 ? RenderingFrameMsMin : 0).ToString("F4", CultureInfo.InvariantCulture)}\n" +
                        $"rendering_frame_ms_max={(RenderingSampleFrameCount > 0 ? RenderingFrameMsMax : 0).ToString("F4", CultureInfo.InvariantCulture)}\n" +
                        $"rendering_frame_ms_p99={renderingP99.ToString("F4", CultureInfo.InvariantCulture)}\n" +
                        $"rendering_benchmark_time_total_ms={RenderingSampleTimeMs.ToString("F4", CultureInfo.InvariantCulture)}\n" +
                        "\n" +
                        $"meshing_target_warmup_seconds={Config.WarmUpMeshingSeconds.ToString("F4", CultureInfo.InvariantCulture)}\n" +
                        $"meshing_warmup_frames={MeshingWarmUpFrameCount}\n" +
                        $"meshing_target_sample_seconds={Config.SampleMeshingSeconds.ToString("F4", CultureInfo.InvariantCulture)}\n" +
                        $"meshing_sample_frames={MeshingSampleFrameCount}\n" +
                        $"meshing_frame_ms_avg={MeshingFrameMsAvg.ToString("F4", CultureInfo.InvariantCulture)}\n" +
                        $"meshing_frame_ms_min={(MeshingSampleFrameCount > 0 ? MeshingFrameMsMin : 0).ToString("F4", CultureInfo.InvariantCulture)}\n" +
                        $"meshing_frame_ms_max={(MeshingSampleFrameCount > 0 ? MeshingFrameMsMax : 0).ToString("F4", CultureInfo.InvariantCulture)}\n" +
                        $"meshing_frame_ms_p99={meshingP99.ToString("F4", CultureInfo.InvariantCulture)}\n" +
                        meshingBlock +
                        $"meshing_benchmark_time_total_ms={MeshingSampleTimeMs.ToString("F4", CultureInfo.InvariantCulture)}\n" +
                        "\n" +
                        $"benchmark_time_total_ms={TotalTimeMs.ToString("F4", CultureInfo.InvariantCulture)}\n";

        File.WriteAllText(Config.OutputFilePath, output);
        string fullPath = Path.GetFullPath(Config.OutputFilePath);
        Log.Information("Benchmark complete. Results written to {OutputFilePath}", fullPath);
        Config.OnComplete?.Invoke();
    }
}