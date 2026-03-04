using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using Silt.Scenes;

namespace Silt.SceneManagement;

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

        registry.Register("small", (gl, window) => new BenchmarkScene(2, gl, window));
        registry.Register("large", (gl, window) => new BenchmarkScene(4, gl, window));

        return registry;
    }
}