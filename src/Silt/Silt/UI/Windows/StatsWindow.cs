using System.Diagnostics;
using ImGuiNET;
using Silt.Metrics;
using Silt.Platform;

namespace Silt.UI.Windows;

public sealed class StatsWindow : IUiWindow
{
    public string Title => "Stats";
    public ImGuiWindowFlags Flags => ImGuiWindowFlags.AlwaysAutoResize;
    public bool IsOpen { get; set; } = true;


    public void Initialize() { }


    public void Update(double deltaTime) { }


    public void Draw(double deltaTime)
    {
        switch (PerfMonitor.Mode)
        {
            case PerfMonitorMode.Benchmark:
                DrawBenchmarkStats();
                break;
            case PerfMonitorMode.Runtime:
            default:
                DrawRuntimeStats();
                break;
        }
        
        ImGui.Separator();
        ImGui.TextUnformatted("Environment");
        ImGui.TextUnformatted($"Processors: {SystemInfo.ProcessorCount}");
        ImGui.TextUnformatted($"Main thread id: {SystemInfo.MainThreadId}");
        ImGui.TextUnformatted($"Window: {WindowInfo.ClientWidth}x{WindowInfo.ClientHeight} (AR:{WindowInfo.ClientAspectRatio:F3})");
        if (!string.IsNullOrEmpty(SystemInfo.DotnetVersion))
            ImGui.TextUnformatted($".NET: {SystemInfo.DotnetVersion}");
        if (!string.IsNullOrEmpty(SystemInfo.ProcessArch))
            ImGui.TextUnformatted($"Arch: {SystemInfo.ProcessArch}");
        if (!string.IsNullOrEmpty(SystemInfo.OsDescription))
            ImGui.TextUnformatted($"OS: {SystemInfo.OsDescription}");

        if (SystemInfo.HasGpuStringInfo)
        {
            ImGui.Separator();
            ImGui.TextUnformatted("OpenGL");
            ImGui.TextUnformatted($"Vendor: {SystemInfo.GPUVendor}");
            ImGui.TextUnformatted($"Renderer: {SystemInfo.GPURenderer}");
            ImGui.TextUnformatted($"Version: {SystemInfo.GPUVersion}");
            ImGui.TextUnformatted($"GLSL: {SystemInfo.GlslVersion}");
        }
    }


    private static void DrawBenchmarkStats()
    {
        Debug.Assert(PerfMonitor.BenchmarkRun != null);
        ImGui.TextUnformatted("Benchmark mode");

        BenchmarkRun run = PerfMonitor.BenchmarkRun;

        double warmupTargetS = Math.Max(0, run.Config.WarmUpSeconds);
        double sampleTargetS = Math.Max(0, run.Config.SampleSeconds);
        double warmupS = run.WarmUpTimeMs / 1000.0;
        double sampleS = run.SampleTimeMs / 1000.0;

        if (run.IsWarmingUp)
        {
            ImGui.TextUnformatted($"Warming up... ({warmupS:F2}/{warmupTargetS:F2} s, frames={run.WarmUpFrameCount:N0})");
        }
        else
        {
            ImGui.TextUnformatted($"Collecting benchmark data... ({sampleS:F2}/{sampleTargetS:F2} s, frames={run.SampleFrameCount:N0})");

            double bFpsAvg = run.FrameMsAvg > 0 ? 1000.0 / run.FrameMsAvg : 0;
            double bFpsMin = run.FrameMsMax > 0 ? 1000.0 / run.FrameMsMax : 0;
            double bFpsMax = run.FrameMsMin > 0 ? 1000.0 / run.FrameMsMin : 0;

            ImGui.TextUnformatted($"Frame avg    : {run.FrameMsAvg:F2} ms ({bFpsAvg:F1} FPS)");
            ImGui.TextUnformatted($"Frame min/max: {run.FrameMsMin:F2} ms ({bFpsMax:F1} FPS) / {run.FrameMsMax:F2} ms ({bFpsMin:F1} FPS)");
            ImGui.TextUnformatted($"Total sample time: {run.TotalTimeMs / 1000.0:F2} s");
        }
    }


    private static void DrawRuntimeStats()
    {
        double msAvg = PerfMonitor.FrameMsAvg;
        double fpsAvg = msAvg > 0 ? 1000.0 / msAvg : 0;
        double msMin = PerfMonitor.FrameMsMin;
        double fpsMin = msMin > 0 ? 1000.0 / msMin : 0;
        double msMax = PerfMonitor.FrameMsMax;
        double fpsMax = msMax > 0 ? 1000.0 / msMax : 0;

        double msP99 = PerfMonitor.FrameMsP99;
        double fps1Low = msP99 > 0 ? 1000.0 / msP99 : 0;

        ImGui.TextUnformatted($"Frame: {msAvg:F2} ms avg ({fpsAvg:F1} FPS)");
        ImGui.TextUnformatted($"Frame: {msMin:F2} ms min ({fpsMax:F1} FPS) / {msMax:F2} ms max ({fpsMin:F1} FPS)");
        ImGui.TextUnformatted($"1% low (p99): {msP99:F2} ms ({fps1Low:F1} FPS)");
        ImGui.TextUnformatted($"Samples: {PerfMonitor.SampleCount}");

        ImGui.Separator();
        ImGui.TextUnformatted("Render stats (this frame)");
        ImGui.TextUnformatted($"Draw calls: {PerfMonitor.DrawCallCount:N0}");
        ImGui.TextUnformatted($"Triangles: {PerfMonitor.TriangleCount:N0}");
        ImGui.TextUnformatted($"Vertices: {PerfMonitor.VertexCount:N0}");

        ImGui.Separator();
        ImGui.TextUnformatted("Memory / GC");
        ImGui.TextUnformatted($"Managed: {FormatBytes(MemoryInfo.ManagedMemoryBytes)}");
        ImGui.TextUnformatted($"Working set: {FormatBytes(MemoryInfo.WorkingSetBytes)}");
        ImGui.TextUnformatted($"GC collections: gen0={MemoryInfo.GcGen0Collections}, gen1={MemoryInfo.GcGen1Collections}, gen2={MemoryInfo.GcGen2Collections}");
    }


    private static string FormatBytes(long bytes)
    {
        const double kb = 1024;
        const double mb = 1024 * kb;
        const double gb = 1024 * mb;

        if (bytes >= gb)
            return $"{bytes / gb:F2} GiB";
        if (bytes >= mb)
            return $"{bytes / mb:F2} MiB";
        if (bytes >= kb)
            return $"{bytes / kb:F2} KiB";
        return $"{bytes} B";
    }
}




