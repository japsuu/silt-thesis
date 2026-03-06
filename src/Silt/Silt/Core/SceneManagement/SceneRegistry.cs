using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using Silt.Scenes;

namespace Silt.Core.SceneManagement;

/// <summary>
/// Simple registry for Scenes keyed by a string id.
/// </summary>
public sealed class SceneRegistry
{
    private readonly Dictionary<string, Func<GL, IWindow, Scene>> _factories = new(StringComparer.OrdinalIgnoreCase);

    public IEnumerable<string> SceneIds => _factories.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase);


    public void Register(string id, Func<GL, IWindow, Scene> factory)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("Scene id cannot be null/empty", nameof(id));
        _factories[id] = factory ?? throw new ArgumentNullException(nameof(factory));
    }


    public bool Contains(string id) => _factories.ContainsKey(id);


    public Scene Create(string id, GL gl, IWindow window)
    {
        if (!_factories.TryGetValue(id, out Func<GL, IWindow, Scene>? factory))
            throw new KeyNotFoundException($"Unknown scene id '{id}'.");

        return factory(gl, window);
    }


    public static SceneRegistry CreateBenchmarks()
    {
        SceneRegistry registry = new();

        registry.Register("normal-fast", (gl, window) => new BenchmarkScene(2, 0.05f, gl, window));
        registry.Register("normal", (gl, window) => new BenchmarkScene(4, 0.05f, gl, window));
        registry.Register("worst-case", (gl, window) => new BenchmarkScene(4, 1f, gl, window));

        return registry;
    }
}