using System.Numerics;
using Serilog;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using Silt.CameraManagement;
using Silt.SceneManagement;
using Silt.World;
using Silt.Metrics;

namespace Silt.Scenes;

public sealed class BenchmarkScene : Scene
{
    private readonly VoxelWorld _world;
    private readonly Chunk[] _centerChunks = new Chunk[8];
    private readonly int _worldRadiusChunks;
    private float _remeshTimer = 0f;
    private int _remeshIndex = 0;
    private bool _isMeshingWorkloadActive;
    

    public BenchmarkScene(int worldRadiusChunks, GL gl, IWindow window) : base(gl, window)
    {
        _worldRadiusChunks = worldRadiusChunks;
        _world = new VoxelWorld(gl, _worldRadiusChunks);
    }


    public override void Load()
    {
        // Setup scene camera
        int cameraDistance = _worldRadiusChunks * Chunk.SIZE * 2 * 2;
        CameraManager.MainCamera.Position = new Vector3(-cameraDistance, cameraDistance, cameraDistance);
        CameraManager.MainCamera.LookAt(Vector3.Zero);
        CameraManager.SetActiveController(new FreeCameraController());
        
        // Get center chunks
        _centerChunks[0] = _world.ChunkManager.GetChunkAtPosition(-1, -1, -1);
        _centerChunks[1] = _world.ChunkManager.GetChunkAtPosition(-1, -1, 0);
        _centerChunks[2] = _world.ChunkManager.GetChunkAtPosition(0, -1, -1);
        _centerChunks[3] = _world.ChunkManager.GetChunkAtPosition(0, -1, 0);
        _centerChunks[4] = _world.ChunkManager.GetChunkAtPosition(-1, 0, -1);
        _centerChunks[5] = _world.ChunkManager.GetChunkAtPosition(-1, 0, 0);
        _centerChunks[6] = _world.ChunkManager.GetChunkAtPosition(0, 0, -1);
        _centerChunks[7] = _world.ChunkManager.GetChunkAtPosition(0, 0, 0);

        PerfMonitor.BenchmarkStateChanged += OnBenchmarkStateChanged;
        PerfMonitor.StartBenchmark(3);
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

        _remeshTimer += (float)deltaTime;
        if (_remeshTimer >= 1f)
        {
            _centerChunks[_remeshIndex].UpdateMesh();
            _remeshIndex = (_remeshIndex + 1) % _centerChunks.Length;
            _remeshTimer = 0f;
            Log.Information("Remeshed chunk at index {Index}", _remeshIndex);
        }
    }


    private void OnBenchmarkStateChanged(BenchmarkState state)
    {
        _isMeshingWorkloadActive = state is BenchmarkState.MeshingWarmup or BenchmarkState.MeshingSample;

        if (!_isMeshingWorkloadActive)
            _remeshTimer = 0f;
    }


    public override void FixedUpdate(double deltaTime)
    {
        
    }


    public override unsafe void Render(double deltaTime)
    {
        _world.Draw();
    }
}