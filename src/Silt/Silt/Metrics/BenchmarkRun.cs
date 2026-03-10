using System.Globalization;
using Serilog;

namespace Silt.Metrics;

public readonly struct BenchmarkConfig(
    string? outputFilePath,
    Action? onComplete,
    double warmUpRenderingSeconds = 10.0,
    double sampleRenderingSeconds = 30.0,
    double warmUpMeshingSeconds = 10.0,
    double sampleMeshingSeconds = 30.0,
    int batchRemeshWarmupIterations = 3,
    int batchRemeshSampleIterations = 3)
{
    public readonly string? OutputFilePath = outputFilePath;
    public readonly Action? OnComplete = onComplete;

    public readonly double WarmUpRenderingSeconds = warmUpRenderingSeconds;
    public readonly double SampleRenderingSeconds = sampleRenderingSeconds;

    public readonly double WarmUpMeshingSeconds = warmUpMeshingSeconds;
    public readonly double SampleMeshingSeconds = sampleMeshingSeconds;
    
    public readonly int BatchRemeshWarmupIterations = batchRemeshWarmupIterations;
    public readonly int BatchRemeshSampleIterations = batchRemeshSampleIterations;
}

public enum BenchmarkState
{
    NotStarted,
    RenderingWarmup,
    RenderingSample,
    MeshingWarmup,
    MeshingSample,
    BatchRemeshWarmup,
    BatchRemeshSample,
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
    public event Action<BenchmarkState, BenchmarkState>? OnStateChanged;

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
    
    // Batch remesh metrics
    public int BatchRemeshTotalChunks { get; private set; }
    public int BatchRemeshWarmupIterations { get; private set; }
    public int BatchRemeshSampleIterations { get; private set; }
    public double BatchRemeshTotalTimeMs { get; private set; }
    public double BatchRemeshAvgIterationMs { get; private set; }
    public double BatchRemeshMinIterationMs { get; private set; } = double.MaxValue;
    public double BatchRemeshMaxIterationMs { get; private set; } = double.MinValue;
    public double BatchRemeshChunksPerSecond { get; private set; }
    
    public double TotalTimeMs { get; private set; }

    private readonly double _warmUpMeshingTargetMs;
    private readonly double _sampleMeshingTargetMs;
    private readonly double _warmUpRenderingTargetMs;
    private readonly double _sampleRenderingTargetMs;
    
    private readonly int _batchRemeshWarmupTargetIterations;
    private readonly int _batchRemeshSampleTargetIterations;

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
        
        _batchRemeshWarmupTargetIterations = Math.Max(0, Config.BatchRemeshWarmupIterations);
        _batchRemeshSampleTargetIterations = Math.Max(1, Config.BatchRemeshSampleIterations);

        _meshingSamplesMs = new double[(int)(Math.Max(0, Config.SampleMeshingSeconds) * MAX_SAMPLES_PER_SECOND)];
        _meshingScratchMs = new double[_meshingSamplesMs.Length];

        _renderingSamplesMs = new double[(int)(Math.Max(0, Config.SampleRenderingSeconds) * MAX_SAMPLES_PER_SECOND)];
        _renderingScratchMs = new double[_renderingSamplesMs.Length];
    }
    
    
    public void Start()
    {
        if (State != BenchmarkState.NotStarted)
            throw new InvalidOperationException("BenchmarkRun can only be started once.");

        TransitionTo(GetNextState());
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
            case BenchmarkState.RenderingWarmup:
                RenderingWarmUpTimeMs += frameMs;
                RenderingWarmUpFrameCount++;
                if (RenderingWarmUpTimeMs >= _warmUpRenderingTargetMs)
                    TransitionTo(GetNextState());
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
                    TransitionTo(GetNextState());

                break;
            }

            case BenchmarkState.MeshingWarmup:
                MeshingWarmUpTimeMs += frameMs;
                MeshingWarmUpFrameCount++;
                if (MeshingWarmUpTimeMs >= _warmUpMeshingTargetMs)
                    TransitionTo(GetNextState());
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
                    TransitionTo(GetNextState());
                break;
            
            // BatchRemeshWarmup and BatchRemeshSample are driven externally via RecordBatchRemeshIteration
            case BenchmarkState.BatchRemeshWarmup:
            case BenchmarkState.BatchRemeshSample:
                break;

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

        BenchmarkState oldState = State;
        State = newState;
        OnStateChanged?.Invoke(oldState, newState);
    }


    /// <summary>
    /// Records a single batch remesh iteration (remeshing all chunks in the world).
    /// Called externally by the benchmark scene after completing a full world remesh.
    /// </summary>
    /// <param name="iterationMs">Time taken to remesh all chunks in milliseconds.</param>
    /// <param name="chunkCount">Total number of chunks that were remeshed.</param>
    public void RecordBatchRemeshIteration(double iterationMs, int chunkCount)
    {
        if (double.IsNaN(iterationMs) || double.IsInfinity(iterationMs) || iterationMs <= 0)
            return;

        BatchRemeshTotalChunks = chunkCount;

        switch (State)
        {
            case BenchmarkState.BatchRemeshWarmup:
                BatchRemeshWarmupIterations++;
                if (BatchRemeshWarmupIterations >= _batchRemeshWarmupTargetIterations)
                    TransitionTo(BenchmarkState.BatchRemeshSample);
                break;

            case BenchmarkState.BatchRemeshSample:
                BatchRemeshTotalTimeMs += iterationMs;
                BatchRemeshMinIterationMs = Math.Min(BatchRemeshMinIterationMs, iterationMs);
                BatchRemeshMaxIterationMs = Math.Max(BatchRemeshMaxIterationMs, iterationMs);
                BatchRemeshSampleIterations++;
                
                BatchRemeshAvgIterationMs = BatchRemeshSampleIterations > 0 
                    ? BatchRemeshTotalTimeMs / BatchRemeshSampleIterations 
                    : 0;
                
                // Calculate chunks per second based on average iteration time
                if (BatchRemeshAvgIterationMs > 0)
                    BatchRemeshChunksPerSecond = chunkCount / (BatchRemeshAvgIterationMs / 1000.0);

                if (BatchRemeshSampleIterations >= _batchRemeshSampleTargetIterations)
                {
                    CompleteAndWriteResults();
                    TransitionTo(BenchmarkState.Complete);
                }
                break;
        }
    }


    private void CompleteAndWriteResults()
    {
        Log.Information("Benchmark complete.");
        
        if (Config.OutputFilePath != null)
        {
            double meshingP99 = MeshingSampleFrameCount > 0
                ? PercentileHelper.P99FromSamples(_meshingSamplesMs, _meshingScratchMs, MeshingSampleFrameCount)
                : 0;

            double renderingP99 = RenderingSampleFrameCount > 0
                ? PercentileHelper.P99FromSamples(_renderingSamplesMs, _renderingScratchMs, RenderingSampleFrameCount)
                : 0;

            MeshingStats meshing = PerfMonitor.BenchmarkChunkMeshing!;
            string meshingBlock = meshing.FormatInvariant("meshing_chunk");

            ChunkStats chunkStats = PerfMonitor.BenchmarkChunkStats!;
            string chunkStatsBlock = chunkStats.FormatInvariant("chunk");

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
                            $"batch_remesh_total_chunks={BatchRemeshTotalChunks}\n" +
                            $"batch_remesh_warmup_iterations={BatchRemeshWarmupIterations}\n" +
                            $"batch_remesh_sample_iterations={BatchRemeshSampleIterations}\n" +
                            $"batch_remesh_iteration_ms_avg={BatchRemeshAvgIterationMs.ToString("F4", CultureInfo.InvariantCulture)}\n" +
                            $"batch_remesh_iteration_ms_min={(BatchRemeshSampleIterations > 0 ? BatchRemeshMinIterationMs : 0).ToString("F4", CultureInfo.InvariantCulture)}\n" +
                            $"batch_remesh_iteration_ms_max={(BatchRemeshSampleIterations > 0 ? BatchRemeshMaxIterationMs : 0).ToString("F4", CultureInfo.InvariantCulture)}\n" +
                            $"batch_remesh_total_time_ms={BatchRemeshTotalTimeMs.ToString("F4", CultureInfo.InvariantCulture)}\n" +
                            $"batch_remesh_chunks_per_second={BatchRemeshChunksPerSecond.ToString("F4", CultureInfo.InvariantCulture)}\n" +
                            "\n" +
                            chunkStatsBlock +
                            "\n" +
                            $"benchmark_time_total_ms={TotalTimeMs.ToString("F4", CultureInfo.InvariantCulture)}\n";

            File.WriteAllText(Config.OutputFilePath, output);
            string fullPath = Path.GetFullPath(Config.OutputFilePath);
            Log.Information("Results written to {OutputFilePath}", fullPath);
        }
        
        Config.OnComplete?.Invoke();
    }


    private BenchmarkState GetNextState()
    {
        // Get next state that does not have zero duration targets
        switch (State)
        {
            case BenchmarkState.NotStarted:
                if (_warmUpRenderingTargetMs > 0)
                    return BenchmarkState.RenderingWarmup;
                goto case BenchmarkState.RenderingWarmup;

            case BenchmarkState.RenderingWarmup:
                if (_sampleRenderingTargetMs > 0)
                    return BenchmarkState.RenderingSample;
                goto case BenchmarkState.RenderingSample;

            case BenchmarkState.RenderingSample:
                if (_warmUpMeshingTargetMs > 0)
                    return BenchmarkState.MeshingWarmup;
                goto case BenchmarkState.MeshingWarmup;

            case BenchmarkState.MeshingWarmup:
                if (_sampleMeshingTargetMs > 0)
                    return BenchmarkState.MeshingSample;
                goto case BenchmarkState.MeshingSample;

            case BenchmarkState.MeshingSample:
                if (_batchRemeshWarmupTargetIterations > 0)
                    return BenchmarkState.BatchRemeshWarmup;
                goto case BenchmarkState.BatchRemeshWarmup;

            case BenchmarkState.BatchRemeshWarmup:
                if (_batchRemeshSampleTargetIterations > 0)
                    return BenchmarkState.BatchRemeshSample;
                goto case BenchmarkState.BatchRemeshSample;

            case BenchmarkState.BatchRemeshSample:
                return BenchmarkState.Complete;

            case BenchmarkState.Complete:
            default:
                throw new InvalidOperationException($"Invalid benchmark state: {State}");
        }
    }
}