using System.Numerics;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using Silt.CameraManagement;
using Silt.SceneManagement;
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
    

    public BenchmarkScene(int worldRadiusChunks, float noiseFrequency, GL gl, IWindow window) : base(gl, window)
    {
        _worldRadiusChunks = worldRadiusChunks;
        _world = new VoxelWorld(gl, _worldRadiusChunks);
        
        ChunkGenerator.ConfigureNoise(noiseFrequency);
    }


    public override void Load()
    {
        // Setup scene camera
        int cameraDistance = _worldRadiusChunks * Chunk.SIZE * 2 * 2;
        CameraManager.MainCamera.Position = new Vector3(-cameraDistance, cameraDistance, cameraDistance);
        CameraManager.MainCamera.LookAt(Vector3.Zero);
        CameraManager.SetActiveController(new FreeCameraController());
        
        _world.Generate();

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

        if (!_isMeshingWorkloadActive)
            return;
        
        _world.ChunkManager.Chunks[_remeshIndex].UpdateMesh();
        _remeshIndex = (_remeshIndex + 1) % _world.ChunkManager.Chunks.Length;
    }


    private void OnBenchmarkStateChanged(BenchmarkState state)
    {
        _isMeshingWorkloadActive = state is BenchmarkState.MeshingWarmup or BenchmarkState.MeshingSample;
    }


    public override void FixedUpdate(double deltaTime)
    {
        
    }


    public override unsafe void Render(double deltaTime)
    {
        _world.Draw();
    }
}