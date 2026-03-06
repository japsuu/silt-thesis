using System.Numerics;
using Silk.NET.Input;

namespace Silt.Core.InputManagement;

/// <summary>
/// Manages input from keyboard and mouse devices.
/// </summary>
public static class Input
{
    public static event Action<IKeyboard>? KeyboardAdded;
    public static event Action<IKeyboard>? KeyboardRemoved;
    public static event Action<IMouse>? MouseAdded;
    public static event Action<IMouse>? MouseRemoved;
    
    /// <summary>
    /// Current position of the mouse cursor.
    /// </summary>
    public static Vector2 MousePosition { get; private set; }
    
    /// <summary>
    /// Mouse position delta since the last frame.
    /// </summary>
    public static Vector2 MouseDelta { get; private set; }

    /// <summary>
    /// Mouse scroll wheel offset since the last frame.
    /// </summary>
    public static Vector2 MouseScrollDelta { get; private set; }
    
    public static IReadOnlyList<IKeyboard> Keyboards => _context.Keyboards;
    public static IReadOnlyList<IMouse> Mice => _context.Mice;
    
    private static readonly Dictionary<Key, double> _heldKeys = [];
    private static readonly HashSet<Key> _pressedKeys = [];
    private static readonly HashSet<Key> _releasedKeys = [];
    private static readonly Dictionary<MouseButton, double> _heldMouseButtons = [];
    private static readonly HashSet<MouseButton> _pressedMouseButtons = [];
    private static readonly HashSet<MouseButton> _releasedMouseButtons = [];
    private static IInputContext _context = null!;
    private static Vector2 _lastSilkPosition;
    private static bool _hasLastSilkPosition;
    private static CursorMode? _lastCursorMode;


    public static void Initialize(IInputContext inputContext)
    {
        _context = inputContext;
        foreach (IKeyboard keyboard in inputContext.Keyboards)
        {
            RegisterKeyboard(keyboard);
        }

        foreach (IMouse mouse in inputContext.Mice)
        {
            RegisterMouse(mouse);
        }
        
        inputContext.ConnectionChanged += OnInputDeviceConnectionChanged;
        
        _lastSilkPosition = Vector2.Zero;
        _hasLastSilkPosition = false;
        _lastCursorMode = null;
        
        MousePosition = Vector2.Zero;
        MouseDelta = Vector2.Zero;
        MouseScrollDelta = Vector2.Zero;
    }


    /// <summary>
    /// Updates the input state. Should be called once per frame.
    /// </summary>
    public static void Update(double deltaTime)
    {
        foreach (KeyValuePair<Key, double> item in _heldKeys)
            _heldKeys[item.Key] += deltaTime;
        
        foreach (KeyValuePair<MouseButton, double> item in _heldMouseButtons)
            _heldMouseButtons[item.Key] += deltaTime;
        
        MouseDelta = Vector2.Zero;
        MouseScrollDelta = Vector2.Zero;
        _pressedKeys.Clear();
        _releasedKeys.Clear();
        _pressedMouseButtons.Clear();
        _releasedMouseButtons.Clear();
    }


    /// <summary>
    /// Checks if a specific key is currently being held down.
    /// </summary>
    /// <returns>True if the key is pressed, false otherwise.</returns>
    public static bool IsKeyDown(Key key) => _heldKeys.ContainsKey(key);
    
    /// <summary>
    /// Checks if a specific key was pressed during the current frame.
    /// </summary>
    /// <returns>True if the key was pressed, false otherwise.</returns>
    public static bool WasKeyPressed(Key key) => _pressedKeys.Contains(key);
    
    
    /// <summary>
    /// Checks if a specific key was released during the current frame.
    /// </summary>
    /// <returns>True if the key was released, false otherwise.</returns>
    public static bool WasKeyReleased(Key key) => _releasedKeys.Contains(key);
    
    
    /// <summary>
    /// Gets the duration a specific key has been held down.
    /// </summary>
    /// <returns>The time in seconds the key has been held down.</returns>
    public static double GetKeyHoldTime(Key key) => _heldKeys.TryGetValue(key, out double time) ? time : 0.0;


    /// <summary>
    /// Checks if a specific mouse button is currently being held down.
    /// </summary>
    /// <returns>True if the button is pressed, false otherwise.</returns>
    public static bool IsMouseButtonDown(MouseButton button) => _heldMouseButtons.ContainsKey(button);
    
    
    /// <summary>
    /// Checks if a specific mouse button was pressed during the current frame.
    /// </summary>
    /// <returns>True if the button was pressed, false otherwise.</returns>
    public static bool WasMouseButtonPressed(MouseButton button) => _pressedMouseButtons.Contains(button);
    
    
    /// <summary>
    /// Checks if a specific mouse button was released during the current frame.
    /// </summary>
    /// <returns>True if the button was released, false otherwise.</returns>
    public static bool WasMouseButtonReleased(MouseButton button) => _releasedMouseButtons.Contains(button);
    
    
    /// <summary>
    /// Gets the duration a specific mouse button has been held down.
    /// </summary>
    /// <returns>The time in seconds the button has been held down.</returns>
    public static double GetMouseButtonHoldTime(MouseButton button) => _heldMouseButtons.TryGetValue(button, out double time) ? time : 0.0;
    
    
    public static void ForceMouseRebase(Vector2 center)
    {
        // Keep public position fixed and reset delta baseline to avoid spikes.
        MousePosition = center;
        _lastSilkPosition = center;
        _hasLastSilkPosition = true;
        MouseDelta = Vector2.Zero;
    }
    
    
    private static void OnInputDeviceConnectionChanged(IInputDevice device, bool connected)
    {
        switch (device)
        {
            case IKeyboard keyboard when connected:
                RegisterKeyboard(keyboard);
                break;
            case IKeyboard keyboard:
                UnregisterKeyboard(keyboard);
                break;
            case IMouse mouse when connected:
                RegisterMouse(mouse);
                break;
            case IMouse mouse:
                UnregisterMouse(mouse);
                break;
        }
    }


    private static void RegisterKeyboard(IKeyboard keyboard)
    {
        keyboard.KeyDown += OnKeyDown;
        keyboard.KeyUp += OnKeyUp;
        KeyboardAdded?.Invoke(keyboard);
    }
    
    
    private static void UnregisterKeyboard(IKeyboard keyboard)
    {
        keyboard.KeyDown -= OnKeyDown;
        keyboard.KeyUp -= OnKeyUp;
        KeyboardRemoved?.Invoke(keyboard);
    }


    private static void RegisterMouse(IMouse mouse)
    {
        mouse.MouseDown += OnMouseDown;
        mouse.MouseUp += OnMouseUp;
        mouse.MouseMove += OnMouseMove;
        mouse.Scroll += OnMouseScroll;
        MouseAdded?.Invoke(mouse);
    }
    
    
    private static void UnregisterMouse(IMouse mouse)
    {
        mouse.MouseDown -= OnMouseDown;
        mouse.MouseUp -= OnMouseUp;
        mouse.MouseMove -= OnMouseMove;
        mouse.Scroll -= OnMouseScroll;
        MouseRemoved?.Invoke(mouse);
    }


    private static void OnKeyDown(IKeyboard keyboard, Key key, int arg3)
    {
        _heldKeys.Add(key, 0);
        _pressedKeys.Add(key);
    }


    private static void OnKeyUp(IKeyboard keyboard, Key key, int arg3)
    {
        _heldKeys.Remove(key);
        _releasedKeys.Add(key);
    }


    private static void OnMouseDown(IMouse mouse, MouseButton button)
    {
        _heldMouseButtons.Add(button, 0);
        _pressedMouseButtons.Add(button);
    }


    private static void OnMouseUp(IMouse mouse, MouseButton button)
    {
        _heldMouseButtons.Remove(button);
        _releasedMouseButtons.Add(button);
    }


    private static void OnMouseMove(IMouse mouse, Vector2 position)
    {
        // Rebase delta baseline when cursor mode changes to avoid first-frame spikes.
        CursorMode currentMode = mouse.Cursor.CursorMode;
        if (_lastCursorMode != currentMode)
        {
            _lastCursorMode = currentMode;
            _hasLastSilkPosition = false; // drop baseline so first delta after switch is zero
            MouseDelta = Vector2.Zero;    // clear any pending per-frame delta
        }

        if (!_hasLastSilkPosition)
        {
            _lastSilkPosition = position;
            _hasLastSilkPosition = true;
        }

        Vector2 rawDelta = position - _lastSilkPosition;
        _lastSilkPosition = position;

        // Always accumulate delta per frame
        MouseDelta += rawDelta;

        switch (currentMode)
        {
            case CursorMode.Normal:
            case CursorMode.Hidden:
                MousePosition = position;
                break;

            case CursorMode.Disabled:
            case CursorMode.Raw:
                // Keep MousePosition fixed
                break;
        }
    }



    private static void OnMouseScroll(IMouse mouse, ScrollWheel scroll) => MouseScrollDelta += new Vector2(scroll.X, scroll.Y);
}