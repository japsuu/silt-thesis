using System.Drawing;
using System.Numerics;
using ImGuiNET;
using Serilog;
using Serilog.Events;
using Silk.NET.Core.Native;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.OpenGL.Extensions.ImGui;
using Silk.NET.Windowing;
using Silt.CameraManagement;
using Silt.InputManagement;
using Silt.Metrics;
using Silt.Platform;
using Silt.SceneManagement;
using Silt.Scenes;
using Silt.UI;
using Silt.UI.Windows;

namespace Silt;

public sealed class SiltEngine
{
    private AppOptions _options = null!;
    private SceneRegistry _benchmarkSceneRegistry = null!;
    private IWindow _window = null!;
    private GL _gl = null!;
    private ImGuiController _imguiController = null!;
    private UiManager _uiManager = null!;
    private Scene _currentScene = null!;
    private double _fixedFrameAccumulator;
    private bool _isExitRequested;


    public void Run(AppOptions? options = null)
    {
        try
        {
            _options = options ?? new AppOptions();
            _benchmarkSceneRegistry = SceneRegistry.CreateBenchmarks();

            Log.Information("Starting Silt engine...");

            _window = CreateWindow();
            _window.Load += OnLoad;
            _window.Update += OnUpdate;
            _window.Render += OnRender;
            _window.FramebufferResize += OnFramebufferResize;
            _window.Closing += OnClose;

            _window.Run();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Silt terminated unexpectedly");
        }
        finally
        {
            _window.Dispose();
            Log.CloseAndFlush();
        }
    }


    private void OnLoad()
    {
        // Setup OpenGL
        _gl = _window.CreateOpenGL();
#if DEBUG
        SetupOpenGlLogging(_gl);
#endif
        _gl.ClearColor(Color.Black);
        _gl.Enable(GLEnum.DepthTest);

        // Setup platform info
        MemoryInfo.Initialize();
        SystemInfo.Initialize(_gl);
        WindowInfo.Initialize(_window);
        
        // Setup performance metrics
        if (_options.BenchmarkEnabled)
        {
            string outputPath = string.IsNullOrWhiteSpace(_options.BenchmarkOutputFilePath)
                ? "benchmark_results.txt"
                : _options.BenchmarkOutputFilePath!;

            PerfMonitor.Initialize(new BenchmarkConfig(
                outputPath,
                onComplete: () => _isExitRequested = true,
                warmUpSeconds: _options.BenchmarkWarmUpSeconds,
                sampleSeconds: _options.BenchmarkSampleSeconds));
        }
        else
        {
            PerfMonitor.Initialize();
        }

        // Setup input
        IInputContext input = _window.CreateInput();
        Input.Initialize(input);

        // Setup ImGui + UI
        _imguiController = new ImGuiController(_gl, _window, input, () =>
        {
            ImGuiIOPtr io = ImGui.GetIO();
            io.ConfigFlags |= ImGuiConfigFlags.DockingEnable;
        });
        _uiManager = new UiManager();
        _uiManager.Register(new StatsWindow());
        _uiManager.Initialize();

        // Setup camera
        CameraManager.Initialize(new Camera(new Vector3(0, 0, 0)));

        // Determine and load the initial scene
        _currentScene = _options.BenchmarkEnabled
            ?_benchmarkSceneRegistry.Create(_options.BenchmarkSceneId!, _gl, _window)
            : new TestScene(_gl, _window);
        _currentScene.Load();
    }


    private void OnUpdate(double deltaTime)
    {
        if (_isExitRequested)
        {
            _window.Close();
            return;
        }
        
        PerfMonitor.BeginFrame(deltaTime);

        _fixedFrameAccumulator += deltaTime;
        
        while (_fixedFrameAccumulator >= SiltConstants.FIXED_DELTA_TIME)
        {
            InternalFixedUpdate(SiltConstants.FIXED_DELTA_TIME);
            _fixedFrameAccumulator -= SiltConstants.FIXED_DELTA_TIME;
        }
        
        InternalUpdate(deltaTime);
    }


    private void OnRender(double deltaTime)
    {
        InternalRender(deltaTime);
    }


    private void OnClose()
    {
        _uiManager.Dispose();
        _imguiController.Dispose();
        
        _currentScene.Unload();
    }


    private void OnFramebufferResize(Vector2D<int> newSize)
    {
        // Keep GL viewport in sync with the framebuffer.
        _gl.Viewport(newSize);
    }


    private void InternalUpdate(double deltaTime)
    {
        _currentScene.Update(deltaTime);

        if (Input.GetKeyHoldTime(Key.Escape) > 3)
            _window.Close();

        if (Input.WasKeyPressed(Key.F1))
            UiManager.ToggleUiVisibility();

        _uiManager.Update(deltaTime);
        _imguiController.Update((float)deltaTime);

        CameraManager.Update(deltaTime);
        Input.Update(deltaTime);
    }


    private void InternalFixedUpdate(double fixedDeltaTime)
    {
        _currentScene.FixedUpdate(fixedDeltaTime);
    }


    private void InternalRender(double deltaTime)
    {
        _gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

        _currentScene.Render(deltaTime);

        // Render ImGui on top of the scene.
        _uiManager.Draw(deltaTime);
        _imguiController.Render();
    }


    private static IWindow CreateWindow()
    {
        ContextFlags flags = ContextFlags.ForwardCompatible;
#if DEBUG
        flags |= ContextFlags.Debug;
#endif
        WindowOptions options = WindowOptions.Default with
        {
            Size = new Vector2D<int>(1920, 1080),
            Title = "Silt",
            VSync = false,
            API = new GraphicsAPI(ContextAPI.OpenGL, ContextProfile.Core, flags, new APIVersion(3, 3))
        };

        return Window.Create(options);
    }


    private static unsafe void SetupOpenGlLogging(GL gl)
    {
        // Enable debug output
        gl.Enable(GLEnum.DebugOutput);
        gl.Enable(GLEnum.DebugOutputSynchronous);

        // Filter noise (notifications etc.)
        gl.DebugMessageControl(
            GLEnum.DontCare,
            GLEnum.DontCare,
            GLEnum.DebugSeverityNotification,
            0,
            null,
            false);

        // Register debug callback
        gl.DebugMessageCallback(
            (
                source,
                type,
                id,
                severity,
                length,
                message,
                userParam) =>
            {
                string msg = SilkMarshal.PtrToString(message, NativeStringEncoding.UTF8) ?? string.Empty;

                // Map severity to Serilog levels
                LogEventLevel level = severity switch
                {
                    GLEnum.DebugSeverityHigh => LogEventLevel.Error,
                    GLEnum.DebugSeverityMedium => LogEventLevel.Warning,
                    GLEnum.DebugSeverityLow => LogEventLevel.Information,
                    _ => LogEventLevel.Debug
                };

                string src = source.ToString();
                string typ = type.ToString();

                Log.Write(
                    level,
                    "OpenGL [{Source}] [{Type}] Id={Id}: {Message}",
                    src,
                    typ,
                    id,
                    msg);
            }, null);
    }
}