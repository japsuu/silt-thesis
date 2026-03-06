using System.Diagnostics;

namespace Silt.Core.Platform;

/// <summary>
/// Contains information about the current memory usage of the process.
/// </summary>
public static class MemoryInfo
{
    public static long ManagedMemoryBytes => GC.GetTotalMemory(false);
    public static long WorkingSetBytes => _currentProcess.WorkingSet64;
    public static long GcGen0Collections => GC.CollectionCount(0);
    public static long GcGen1Collections => GC.CollectionCount(1);
    public static long GcGen2Collections => GC.CollectionCount(2);

    private static Process _currentProcess = null!;
    
    
    public static void Initialize()
    {
        _currentProcess = Process.GetCurrentProcess();
    }
}