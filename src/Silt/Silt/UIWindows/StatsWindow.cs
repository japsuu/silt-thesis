using System.Diagnostics;
using ImGuiNET;
using Silt.Core.CameraManagement;
using Silt.Core.Platform;
using Silt.Core.UI;
using Silt.Metrics;

namespace Silt.UIWindows;

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
        
        ImGui.Separator();
        ImGui.TextUnformatted("Camera");
        Camera cam = CameraManager.MainCamera;
        ImGui.TextUnformatted($"Position: {cam.Position.X:F2}, {cam.Position.Y:F2}, {cam.Position.Z:F2}");
    }


    private static void DrawBenchmarkStats()
    {
        Debug.Assert(PerfMonitor.BenchmarkRun != null);
        ImGui.TextUnformatted("Benchmark mode");

        BenchmarkRun run = PerfMonitor.BenchmarkRun;

        // Phase progress
        double warmupMeshingTargetS = Math.Max(0, run.Config.WarmUpMeshingSeconds);
        double sampleMeshingTargetS = Math.Max(0, run.Config.SampleMeshingSeconds);
        double warmupRenderingTargetS = Math.Max(0, run.Config.WarmUpRenderingSeconds);
        double sampleRenderingTargetS = Math.Max(0, run.Config.SampleRenderingSeconds);

        double warmupMeshingS = run.MeshingWarmUpTimeMs / 1000.0;
        double sampleMeshingS = run.MeshingSampleTimeMs / 1000.0;
        double warmupRenderingS = run.RenderingWarmUpTimeMs / 1000.0;
        double sampleRenderingS = run.RenderingSampleTimeMs / 1000.0;

        ImGui.TextUnformatted($"State: {run.State}");

        switch (run.State)
        {
            case BenchmarkState.MeshingWarmup:
                ImGui.TextUnformatted($"Warming up meshing... ({warmupMeshingS:F2}/{warmupMeshingTargetS:F2} s, frames={run.MeshingWarmUpFrameCount:N0})");
                break;

            case BenchmarkState.MeshingSample:
            {
                ImGui.TextUnformatted($"Sampling meshing... ({sampleMeshingS:F2}/{sampleMeshingTargetS:F2} s, frames={run.MeshingSampleFrameCount:N0})");

                double frameMsAvg = run.MeshingFrameMsAvg;
                double frameMsMin = run.MeshingSampleFrameCount > 0 ? run.MeshingFrameMsMin : 0;
                double frameMsMax = run.MeshingSampleFrameCount > 0 ? run.MeshingFrameMsMax : 0;

                double bFpsAvg = frameMsAvg > 0 ? 1000.0 / frameMsAvg : 0;
                double bFpsMin = frameMsMax > 0 ? 1000.0 / frameMsMax : 0;
                double bFpsMax = frameMsMin > 0 ? 1000.0 / frameMsMin : 0;

                ImGui.TextUnformatted($"Frame avg    : {frameMsAvg:F2} ms ({bFpsAvg:F1} FPS)");
                ImGui.TextUnformatted($"Frame min/max: {frameMsMin:F2} ms ({bFpsMax:F1} FPS) / {frameMsMax:F2} ms ({bFpsMin:F1} FPS)");
                ImGui.TextUnformatted($"Total benchmark time: {run.TotalTimeMs / 1000.0:F2} s");
                break;
            }
            
            case BenchmarkState.BatchRemeshWarmup:
                ImGui.TextUnformatted($"Warming up batch remesh... (iteration {run.BatchRemeshWarmupIterations}/{run.Config.BatchRemeshWarmupIterations})");
                ImGui.TextUnformatted($"Chunks per iteration: {run.BatchRemeshTotalChunks:N0}");
                break;
            
            case BenchmarkState.BatchRemeshSample:
            {
                ImGui.TextUnformatted($"Sampling batch remesh... (iteration {run.BatchRemeshSampleIterations}/{run.Config.BatchRemeshSampleIterations})");
                ImGui.TextUnformatted($"Chunks per iteration: {run.BatchRemeshTotalChunks:N0}");
                
                double iterMsAvg = run.BatchRemeshAvgIterationMs;
                double iterMsMin = run.BatchRemeshSampleIterations > 0 ? run.BatchRemeshMinIterationMs : 0;
                double iterMsMax = run.BatchRemeshSampleIterations > 0 ? run.BatchRemeshMaxIterationMs : 0;
                
                ImGui.TextUnformatted($"Iteration avg    : {iterMsAvg:F2} ms");
                ImGui.TextUnformatted($"Iteration min/max: {iterMsMin:F2} ms / {iterMsMax:F2} ms");
                ImGui.TextUnformatted($"Chunks/sec: {run.BatchRemeshChunksPerSecond:F1}");
                ImGui.TextUnformatted($"Total benchmark time: {run.TotalTimeMs / 1000.0:F2} s");
                break;
            }

            case BenchmarkState.RenderingWarmup:
                ImGui.TextUnformatted($"Warming up rendering... ({warmupRenderingS:F2}/{warmupRenderingTargetS:F2} s, frames={run.RenderingWarmUpFrameCount:N0})");
                break;

            case BenchmarkState.RenderingSample:
            {
                ImGui.TextUnformatted($"Sampling rendering... ({sampleRenderingS:F2}/{sampleRenderingTargetS:F2} s, frames={run.RenderingSampleFrameCount:N0})");

                double frameMsAvg = run.RenderingFrameMsAvg;
                double frameMsMin = run.RenderingSampleFrameCount > 0 ? run.RenderingFrameMsMin : 0;
                double frameMsMax = run.RenderingSampleFrameCount > 0 ? run.RenderingFrameMsMax : 0;

                double bFpsAvg = frameMsAvg > 0 ? 1000.0 / frameMsAvg : 0;
                double bFpsMin = frameMsMax > 0 ? 1000.0 / frameMsMax : 0;
                double bFpsMax = frameMsMin > 0 ? 1000.0 / frameMsMin : 0;

                ImGui.TextUnformatted($"Frame avg    : {frameMsAvg:F2} ms ({bFpsAvg:F1} FPS)");
                ImGui.TextUnformatted($"Frame min/max: {frameMsMin:F2} ms ({bFpsMax:F1} FPS) / {frameMsMax:F2} ms ({bFpsMin:F1} FPS)");
                ImGui.TextUnformatted($"Total benchmark time: {run.TotalTimeMs / 1000.0:F2} s");
                break;
            }

            case BenchmarkState.Complete:
                ImGui.TextUnformatted("Complete.");
                break;

            case BenchmarkState.NotStarted:
            default:
                ImGui.TextUnformatted("Not started.");
                break;
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




