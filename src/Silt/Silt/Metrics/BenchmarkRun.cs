using System.Globalization;
using Serilog;

namespace Silt.Metrics;

public readonly struct BenchmarkConfig(string outputFilePath, Action? onComplete, double warmUpSeconds = 10.0, double sampleSeconds = 50.0)
{
    public readonly string OutputFilePath = outputFilePath;
    public readonly Action? OnComplete = onComplete;

    public readonly double WarmUpSeconds = warmUpSeconds;
    public readonly double SampleSeconds = sampleSeconds;
}

public sealed class BenchmarkRun
{
    public BenchmarkConfig Config { get; set; }

    public bool IsComplete => SampleTimeMs >= _sampleTargetMs;
    public bool IsWarmingUp => WarmUpTimeMs < _warmUpTargetMs;

    public double FrameMsAvg { get; private set; }
    public double FrameMsMin { get; private set; }
    public double FrameMsMax { get; private set; }
    public double TotalTimeMs { get; private set; }

    public double WarmUpTimeMs { get; private set; }
    public double SampleTimeMs { get; private set; }
    public int WarmUpFrameCount { get; private set; }
    public int SampleFrameCount { get; private set; }

    private readonly double _warmUpTargetMs;
    private readonly double _sampleTargetMs;
    // Preallocated storage for benchmark samples and scratch for percentile.
    private readonly double[] _samplesMs;
    private readonly double[] _scratchMs;
    
    private const int MAX_SAMPLES_PER_SECOND = 3072;


    public BenchmarkRun(BenchmarkConfig config)
    {
        Config = config;

        FrameMsAvg = 0;
        FrameMsMin = double.MaxValue;
        FrameMsMax = double.MinValue;
        TotalTimeMs = 0;

        WarmUpTimeMs = 0;
        SampleTimeMs = 0;
        WarmUpFrameCount = 0;
        SampleFrameCount = 0;

        _warmUpTargetMs = Math.Max(0, Config.WarmUpSeconds) * 1000.0;
        _sampleTargetMs = Math.Max(0, Config.SampleSeconds) * 1000.0;

        _samplesMs = new double[(int)(Config.SampleSeconds * MAX_SAMPLES_PER_SECOND)];
        _scratchMs = new double[_samplesMs.Length];
    }


    public void Update(double frameMs)
    {
        if (double.IsNaN(frameMs) || double.IsInfinity(frameMs) || frameMs <= 0)
            return;

        if (IsComplete)
            return;

        if (IsWarmingUp)
        {
            WarmUpTimeMs += frameMs;
            WarmUpFrameCount++;
            return;
        }

        // Record sample
        _samplesMs[SampleFrameCount] = frameMs;
        TotalTimeMs += frameMs;
        SampleTimeMs += frameMs;
        SampleFrameCount++;

        // Update aggregates
        FrameMsMin = Math.Min(FrameMsMin, frameMs);
        FrameMsMax = Math.Max(FrameMsMax, frameMs);
        FrameMsAvg = SampleFrameCount > 0 ? TotalTimeMs / SampleFrameCount : 0;

        if (IsComplete)
        {
            double frameMsP99 = PercentileHelper.P99FromSamples(_samplesMs, _scratchMs, SampleFrameCount);

            string output = $"mode=benchmark\n" +
                            $"warmup_seconds={Config.WarmUpSeconds.ToString("F4", CultureInfo.InvariantCulture)}\n" +
                            $"sample_seconds={Config.SampleSeconds.ToString("F4", CultureInfo.InvariantCulture)}\n" +
                            $"warmup_frames={WarmUpFrameCount}\n" +
                            $"sample_frames={SampleFrameCount}\n" +
                            $"frame_ms_avg={FrameMsAvg.ToString("F4", CultureInfo.InvariantCulture)}\n" +
                            $"frame_ms_min={FrameMsMin.ToString("F4", CultureInfo.InvariantCulture)}\n" +
                            $"frame_ms_max={FrameMsMax.ToString("F4", CultureInfo.InvariantCulture)}\n" +
                            $"frame_ms_p99={frameMsP99.ToString("F4", CultureInfo.InvariantCulture)}\n" +
                            $"total_time_ms={TotalTimeMs.ToString("F4", CultureInfo.InvariantCulture)}\n";

            File.WriteAllText(Config.OutputFilePath, output);
            string fullPath = Path.GetFullPath(Config.OutputFilePath);
            Log.Information("Benchmark complete. Results written to {OutputFilePath}", fullPath);
            Config.OnComplete?.Invoke();
        }
    }
}