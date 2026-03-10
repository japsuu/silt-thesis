using JetBrains.Profiler.Api;
using Serilog;

namespace Silt.Metrics;

public enum ProfilePhase
{
    Render,
    Mesh,
    MeshBatch,
}

public static class ProfilePhaseExtensions
{
    public static IEnumerable<string> GetValidProfilePhases()
    {
        return Enum.GetNames<ProfilePhase>().Select(name => name.ToLower());
    }
    
    
    public static ProfilePhase ParseProfilePhase(string phase)
    {
        return Enum.TryParse(phase, true, out ProfilePhase result) 
            ? result 
            : throw new ArgumentException($"Invalid profile phase: {phase}. Valid phases are: {string.Join(", ", GetValidProfilePhases())}");
    }
    
    
    public static bool MatchesBenchmarkState(this ProfilePhase phase, BenchmarkState state)
    {
        return state switch
        {
            BenchmarkState.RenderingSample when phase == ProfilePhase.Render => true,
            BenchmarkState.MeshingSample when phase == ProfilePhase.Mesh => true,
            BenchmarkState.BatchRemeshSample when phase == ProfilePhase.MeshBatch => true,
            _ => false
        };
    }
}

/// <summary>
/// Wraps the JetBrains dotTrace Profiler API to allow programmatic control of performance profiling snapshots.
/// </summary>
public static class ProfilingHelper
{
    /// <summary>
    /// Whether profiling mode has been activated.
    /// </summary>
    public static bool IsActive { get; private set; }
    
    private static ProfilePhase _phase;


    public static void Initialize(ProfilePhase? phase)
    {
        if (phase == null)
            return;
        
        _phase = phase.Value;
        IsActive = true;

        // Detach any profiling that may have started automatically
        MeasureFeatures features = MeasureProfiler.GetFeatures();
        if (features.HasFlag(MeasureFeatures.Ready))
        {
            MeasureProfiler.StopCollectingData();
            Log.Information("[Profiling] Profiler detected and data collection stopped, waiting for meshing phase");
        }
        else
            Log.Information("[Profiling] Profiling mode enabled. Launch this application under dotTrace with 'Do not profile on start'");
        
        PerfMonitor.BenchmarkStateChanged += OnBenchmarkStateChanged;
    }


    private static void OnBenchmarkStateChanged(BenchmarkState oldState, BenchmarkState newState)
    {
        if (!IsActive)
            return;

        if (_phase.MatchesBenchmarkState(newState))
        {
            DropData();
            StartCollecting();
        }
        else if (_phase.MatchesBenchmarkState(oldState))
        {
            SaveSnapshotAndStop();
        }
        
    }


    private static void StartCollecting()
    {
        if (!IsActive)
            return;

        Log.Information("[Profiling] Starting data collection...");
        MeasureProfiler.StartCollectingData();
    }


    private static void SaveSnapshotAndStop()
    {
        if (!IsActive)
            return;

        Log.Information("[Profiling] Saving snapshot and stopping data collection...");
        MeasureProfiler.SaveData();
        MeasureProfiler.StopCollectingData();
        Log.Information("[Profiling] Snapshot saved.");
    }


    private static void DropData()
    {
        if (!IsActive)
            return;

        MeasureProfiler.DropData();
    }
}