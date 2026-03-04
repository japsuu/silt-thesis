using System.Globalization;

namespace Silt.Metrics;

/// <summary>
/// Aggregates per-chunk meshing durations.
/// </summary>
public sealed class MeshingStats
{
    public int SampleCount { get; private set; }

    /// <summary>Total meshing time over all samples.</summary>
    public double TotalMs { get; private set; }

    public double MinMs { get; private set; } = double.MaxValue;
    public double MaxMs { get; private set; } = double.MinValue;

    public double AvgMs => SampleCount > 0 ? TotalMs / SampleCount : 0;


    public void Reset()
    {
        SampleCount = 0;
        TotalMs = 0;
        MinMs = double.MaxValue;
        MaxMs = double.MinValue;
    }


    public void AddSample(double meshingMs)
    {
        if (double.IsNaN(meshingMs) || double.IsInfinity(meshingMs) || meshingMs < 0)
            return;

        SampleCount++;
        TotalMs += meshingMs;
        MinMs = Math.Min(MinMs, meshingMs);
        MaxMs = Math.Max(MaxMs, meshingMs);
    }


    public string FormatInvariant(string keyPrefix)
    {
        double min = SampleCount > 0 ? MinMs : 0;
        double max = SampleCount > 0 ? MaxMs : 0;
        double avg = SampleCount > 0 ? AvgMs : 0;
        double total = TotalMs;

        return $"{keyPrefix}_count={SampleCount}\n" +
               $"{keyPrefix}_ms_avg={avg.ToString("F4", CultureInfo.InvariantCulture)}\n" +
               $"{keyPrefix}_ms_min={min.ToString("F4", CultureInfo.InvariantCulture)}\n" +
               $"{keyPrefix}_ms_max={max.ToString("F4", CultureInfo.InvariantCulture)}\n" +
               $"{keyPrefix}_ms_total={total.ToString("F4", CultureInfo.InvariantCulture)}\n";
    }
}

