using System.Diagnostics;
using Serilog;

namespace Silt.Metrics;

public enum PerfMonitorMode
{
    /// <summary>
    /// Normal runtime mode. Performance metrics are updated every frame and can be displayed in the UI.
    /// </summary>
    Runtime,

    /// <summary>
    /// Benchmarking mode. Performance metrics are collected for a fixed number of frames and then averaged. Useful for automated performance testing.
    /// </summary>
    Benchmark
}

public static class PerfMonitor
{
    // Max number of frame time samples to keep for calculating P99 and other percentiles, in runtime mode.
    // In benchmark mode, all samples are kept until the run is complete.
    private const int RUNTIME_FRAME_TIME_SAMPLES_MAX = 2048;

    // Update p99 at a reduced cadence to avoid doing a sort every frame.
    private const int RUNTIME_P99_UPDATE_INTERVAL_FRAMES = 30;

    // Preallocated runtime frame-time ring.
    private static FrameTimeRingBuffer? _runtimeFrameTimeBuffer;
    private static int _runtimeP99Countdown;

    public static PerfMonitorMode Mode { get; private set; }
    public static int DrawCallCount { get; private set; }
    public static int TriangleCount { get; private set; }
    public static int VertexCount { get; private set; }

    public static double FrameMsAvg { get; private set; }
    public static double FrameMsMin { get; private set; }
    public static double FrameMsMax { get; private set; }
    public static double FrameMsP99 { get; private set; }
    public static int SampleCount { get; private set; }

    public static BenchmarkRun? BenchmarkRun { get; private set; }

    public static BenchmarkState BenchmarkState => BenchmarkRun?.State ?? BenchmarkState.NotStarted;

    /// <summary>
    /// Subscribe to benchmark state changes (only relevant in benchmark mode).
    /// Convenience wrapper over <see cref="BenchmarkRun.OnStateChanged"/>.
    /// </summary>
    public static event Action<BenchmarkState>? BenchmarkStateChanged;

    /// <summary>
    /// Aggregated chunk meshing timings for the current benchmark run.
    /// Null in runtime mode.
    /// </summary>
    public static MeshingStats? BenchmarkChunkMeshing { get; private set; }

    private static bool _isCapturing;
    private static bool _isStarted;
    private static int _skipFrames;


    /// <summary>
    /// Initializes the performance metrics. This should be called at the start of the application, and when starting a new benchmark run.
    /// It resets all metrics to their initial state.
    /// </summary>
    /// <param name="benchmarkConfig">If defined, initializes metrics for benchmarking mode with the given configuration. If null, initializes for normal runtime mode.</param>
    public static void Initialize(BenchmarkConfig? benchmarkConfig = null)
    {
        if (benchmarkConfig.HasValue)
        {
            Mode = PerfMonitorMode.Benchmark;
            BenchmarkRun = new BenchmarkRun(benchmarkConfig.Value);
            BenchmarkRun.OnStateChanged += state => BenchmarkStateChanged?.Invoke(state);

            BenchmarkChunkMeshing ??= new MeshingStats();
            BenchmarkChunkMeshing.Reset();

            Log.Information("Performance metrics initialized in BENCHMARK mode. Output file: {OutputFilePath}", benchmarkConfig.Value.OutputFilePath);
        }
        else
        {
            Mode = PerfMonitorMode.Runtime;
            BenchmarkRun = null;
            BenchmarkChunkMeshing = null;
        }

        // Allocate once for the lifetime of the app.
        _runtimeFrameTimeBuffer ??= new FrameTimeRingBuffer(RUNTIME_FRAME_TIME_SAMPLES_MAX);
        _runtimeFrameTimeBuffer.Reset();
        _runtimeP99Countdown = RUNTIME_P99_UPDATE_INTERVAL_FRAMES;

        DrawCallCount = 0;
        TriangleCount = 0;
        VertexCount = 0;
        FrameMsAvg = 0;
        FrameMsMin = double.MaxValue;
        FrameMsMax = double.MinValue;
        FrameMsP99 = 0;
        SampleCount = 0;
        
        _isCapturing = false;
        _isStarted = false;
        _skipFrames = 0;
    }
    
    
    public static void Start(int skipFrameCount)
    {
        _isStarted = true;
        _skipFrames = skipFrameCount;
        BenchmarkRun?.Start();
        Log.Information("PerfMonitor started");
    }


    public static void BeginFrame(double deltaTime)
    {
        if (!_isStarted)
            return;
        
        if (_skipFrames > 0)
        {
            _skipFrames--;
            _isCapturing = _skipFrames <= 0;
            return;
        }
        
        if (deltaTime <= 0)
            return;

        double frameMs = deltaTime * 1000.0;

        DrawCallCount = 0;
        TriangleCount = 0;
        VertexCount = 0;

        switch (Mode)
        {
            case PerfMonitorMode.Benchmark:
            {
                Debug.Assert(BenchmarkRun != null, nameof(BenchmarkRun) + " != null");

                // Advance the benchmark state machine based on time.
                if (!BenchmarkRun.IsComplete)
                    BenchmarkRun.Update(frameMs);
                break;
            }
            case PerfMonitorMode.Runtime:
            {
                Debug.Assert(_runtimeFrameTimeBuffer != null, nameof(_runtimeFrameTimeBuffer) + " != null");

                _runtimeFrameTimeBuffer.Add(frameMs);

                SampleCount = _runtimeFrameTimeBuffer.Count;
                FrameMsAvg = _runtimeFrameTimeBuffer.AvgMs;
                FrameMsMin = _runtimeFrameTimeBuffer.MinMs;
                FrameMsMax = _runtimeFrameTimeBuffer.MaxMs;

                if (--_runtimeP99Countdown <= 0)
                {
                    FrameMsP99 = _runtimeFrameTimeBuffer.ComputeP99();
                    _runtimeP99Countdown = RUNTIME_P99_UPDATE_INTERVAL_FRAMES;
                }

                break;
            }
            default:
                throw new ArgumentOutOfRangeException($"Invalid PerfMonitorMode: {Mode}");
        }
    }


    public static void AddDrawCalls(int count)
    {
        if (!_isCapturing)
            return;

        DrawCallCount += count;
    }


    public static void AddTriangles(int count)
    {
        if (!_isCapturing)
            return;

        TriangleCount += count;
    }


    public static void AddVertices(int count)
    {
        if (!_isCapturing)
            return;

        VertexCount += count;
    }

    /// <summary>
    /// Records a single chunk meshing sample duration (ms).
    /// Only captured during <see cref="Metrics.BenchmarkState.MeshingSample"/>.
    /// </summary>
    public static void AddChunkMeshingSample(double meshingMs)
    {
        if (!_isCapturing)
            return;

        if (Mode != PerfMonitorMode.Benchmark)
            return;

        if (BenchmarkRun == null || BenchmarkRun.IsComplete)
            return;

        if (BenchmarkRun.State != BenchmarkState.MeshingSample)
            return;

        BenchmarkChunkMeshing?.AddSample(meshingMs);
    }
}