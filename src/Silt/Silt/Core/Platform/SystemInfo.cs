using System.Runtime.InteropServices;
using Serilog;
using Silk.NET.OpenGL;

namespace Silt.Core.Platform;

/// <summary>
/// Contains information about the current system.
/// </summary>
public static class SystemInfo
{
    /// <summary>
    /// Gets the number of processors available to the current process.
    /// </summary>
    public static int ProcessorCount { get; private set; }

    /// <summary>
    /// ID of the thread updating the main window.
    /// </summary>
    public static int MainThreadId { get; private set; }

    public static bool HasGpuStringInfo { get; private set; }
    public static string? GPUVendor { get; private set; }
    public static string? GPURenderer { get; private set; }
    public static string? GPUVersion { get; private set; }
    public static string? GlslVersion { get; private set; }

    public static string DotnetVersion { get; private set; } = null!;
    public static string OsDescription { get; private set; } = null!;
    public static string ProcessArch { get; private set; } = null!;


    public static void Initialize(GL gl)
    {
        ProcessorCount = Environment.ProcessorCount;
        MainThreadId = Environment.CurrentManagedThreadId;

        DotnetVersion = Environment.Version.ToString();
        OsDescription = RuntimeInformation.OSDescription;
        ProcessArch = RuntimeInformation.ProcessArchitecture.ToString();

        try
        {
            GPUVendor = gl.GetStringS(StringName.Vendor);
            GPURenderer = gl.GetStringS(StringName.Renderer);
            GPUVersion = gl.GetStringS(StringName.Version);
            GlslVersion = gl.GetStringS(StringName.ShadingLanguageVersion);
            HasGpuStringInfo = !string.IsNullOrEmpty(GPUVendor);
        }
        catch
        {
            Log.Warning("Failed to query GPU info strings");
        }
    }
}