using System.Numerics;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using Silt.CameraControllers;
using Silt.Core.CameraManagement;
using Silt.Core.SceneManagement;
using Silt.Metrics;
using Silt.World;
using Silt.World.Generation;

namespace Silt.Scenes;

public sealed class TestScene : Scene
{
    private const int WORLD_RADIUS_CHUNKS = 2;
    private readonly VoxelWorld _world;
    private int _remeshIndex = 0;
    

    public TestScene(GL gl, IWindow window) : base(gl, window)
    {
        _world = new VoxelWorld(gl, WORLD_RADIUS_CHUNKS);
        
        ChunkGenerator.ConfigureNoise(0.05f);
    }


    public override void Load()
    {
        // Setup scene camera
        const float cameraDistance = WORLD_RADIUS_CHUNKS * Chunk.SIZE * 2 * 1.4f;
        CameraManager.MainCamera.Position = new Vector3(cameraDistance, cameraDistance, cameraDistance);
        CameraManager.MainCamera.LookAt(Vector3.Zero);
        CameraManager.SetActiveController(new FreeCameraController());
        
        _world.Generate();
        PerfMonitor.Start(100);
    }


    public override void Unload()
    {
        _world.Dispose();
    }


    public override void Update(double deltaTime)
    {
        _world.Update(deltaTime);

        _world.ChunkManager.Chunks[_remeshIndex].UpdateMeshTimed();
        _remeshIndex = (_remeshIndex + 1) % _world.ChunkManager.Chunks.Length;
    }


    public override void FixedUpdate(double deltaTime)
    {
        
    }


    public override unsafe void Render(double deltaTime)
    {
        _world.Draw();
    }
}