using System.Diagnostics;
using System.Numerics;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using Silt.CameraControllers;
using Silt.Core.CameraManagement;
using Silt.Core.SceneManagement;
using Silt.World;
using Silt.Metrics;
using Silt.World.Generation;

namespace Silt.Scenes;

public sealed class BenchmarkScene : Scene
{
    private readonly VoxelWorld _world;
    private readonly int _worldRadiusChunks;
    private int _remeshIndex = 0;
    private bool _isMeshingWorkloadActive;
    private bool _isBatchRemeshWorkloadActive;
    

    public BenchmarkScene(int worldRadiusChunks, float noiseFrequency, GL gl, IWindow window) : base(gl, window)
    {
        _worldRadiusChunks = worldRadiusChunks;
        _world = new VoxelWorld(gl, _worldRadiusChunks);
        
        ChunkGenerator.ConfigureNoise(noiseFrequency);
    }


    public override void Load()
    {
        // Setup scene camera
        float cameraDistance = _worldRadiusChunks * Chunk.SIZE * 2 * 1.4f;
        CameraManager.MainCamera.Position = new Vector3(cameraDistance, cameraDistance, cameraDistance);
        CameraManager.MainCamera.LookAt(Vector3.Zero);
        CameraManager.SetActiveController(new FreeCameraController());
        
        _world.Generate();

        // Record chunk info for benchmark statistics
        int totalChunks = _world.ChunkManager.Chunks.Length;
        PerfMonitor.SetChunkInfo(totalChunks, Chunk.VOXEL_DATA_BYTES_PER_CHUNK);

        PerfMonitor.BenchmarkStateChanged += OnBenchmarkStateChanged;
        PerfMonitor.Start(3);
    }


    public override void Unload()
    {
        if (PerfMonitor.Mode == PerfMonitorMode.Benchmark)
            PerfMonitor.BenchmarkStateChanged -= OnBenchmarkStateChanged;

        _world.Dispose();
    }


    public override void Update(double deltaTime)
    {
        _world.Update(deltaTime);

        // Per-frame single chunk meshing (for per-chunk timing)
        if (_isMeshingWorkloadActive)
        {
            _world.ChunkManager.Chunks[_remeshIndex].UpdateMeshTimed();
            _remeshIndex = (_remeshIndex + 1) % _world.ChunkManager.Chunks.Length;
        }
        
        // Batch remesh all chunks (for throughput/parallelism timing)
        if (_isBatchRemeshWorkloadActive)
        {
            PerformBatchRemesh();
        }
    }


    private void PerformBatchRemesh()
    {
        long startTicks = Stopwatch.GetTimestamp();
        
        Chunk[] chunks = _world.ChunkManager.Chunks;
        foreach (Chunk c in chunks)
            c.UpdateMesh();
        
        long endTicks = Stopwatch.GetTimestamp();
        double iterationMs = (endTicks - startTicks) * 1000.0 / Stopwatch.Frequency;
        
        PerfMonitor.RecordBatchRemeshIteration(iterationMs, chunks.Length);
    }


    private void OnBenchmarkStateChanged(BenchmarkState state)
    {
        _isMeshingWorkloadActive = state is BenchmarkState.MeshingWarmup or BenchmarkState.MeshingSample;
        _isBatchRemeshWorkloadActive = state is BenchmarkState.BatchRemeshWarmup or BenchmarkState.BatchRemeshSample;
    }


    public override void FixedUpdate(double deltaTime)
    {
        
    }


    public override unsafe void Render(double deltaTime)
    {
        _world.Draw();
    }
}