using System.Numerics;
using System.Text;
using Silk.NET.OpenGL;
using Silt.Core.Utils;

namespace Silt.Core.Graphics;

public class ShaderCompilationException(string message) : Exception(message);

public class ShaderLinkingException(string message) : Exception(message);

/// <summary>
/// Represents a shader program.
/// Handles loading, compiling, and linking of vertex and fragment shaders.
/// </summary>
public sealed class Shader : GraphicsResource
{
    public readonly string Name;


    /// <summary>
    /// Creates a new shader program from the specified vertex and fragment shader file paths.
    /// </summary>
    /// <param name="gl">The OpenGL context.</param>
    /// <param name="name">The name of the shader program.</param>
    /// <param name="vertexPath">The file path to the vertex shader source.</param>
    /// <param name="fragmentPath">The file path to the fragment shader source.</param>
    /// <exception cref="FileNotFoundException">Thrown if a shader file cannot be found.</exception>
    /// <exception cref="ShaderCompilationException">Thrown if a shader fails to compile.</exception>
    /// <exception cref="ShaderLinkingException">Thrown if the shader program fails to link.</exception>
    public Shader(GL gl, string name, string vertexPath, string fragmentPath) : base(gl)
    {
        Name = name;
        string vertexSource = LoadAndPreprocessShader(vertexPath);
        string fragmentSource = LoadAndPreprocessShader(fragmentPath);

        uint vertexShader = CompileShader(ShaderType.VertexShader, vertexSource);
        uint fragmentShader = CompileShader(ShaderType.FragmentShader, fragmentSource);

        Handle = Gl.CreateProgram();
        Gl.AttachShader(Handle, vertexShader);
        Gl.AttachShader(Handle, fragmentShader);
        Gl.LinkProgram(Handle);

        Gl.GetProgram(Handle, ProgramPropertyARB.LinkStatus, out int status);
        if (status != (int)GLEnum.True)
        {
            string infoLog = Gl.GetProgramInfoLog(Handle);
            throw new ShaderLinkingException($"Failed to link shader program: {infoLog}");
        }

        // Shaders are now linked into the program so we can delete them.
        Gl.DetachShader(Handle, vertexShader);
        Gl.DetachShader(Handle, fragmentShader);
        Gl.DeleteShader(vertexShader);
        Gl.DeleteShader(fragmentShader);
    }


    /// <summary>
    /// Gets the location of a uniform variable in the shader program.
    /// Use to cache uniform locations for setting uniform values.
    /// </summary>
    /// <param name="name">The name of the uniform variable.</param>
    /// <returns>The location of the uniform variable.</returns>
    public int GetUniformLocation(string name)
    {
        int location = Gl.GetUniformLocation(Handle, name);
        if (location == -1)
            LoggerUtil.LogMissingUniformOnce(Name, name);
        return location;
    }


    public void SetUniform(int location, int value)
    {
        Gl.Uniform1(location, value);
    }


    public void SetUniform(int location, float value)
    {
        Gl.Uniform1(location, value);
    }


    public void SetUniform(int location, TextureUnit value)
    {
        int valueInt = (int)value - (int)TextureUnit.Texture0;
        Gl.Uniform1(location, valueInt);
    }
    
    
    public void SetUniform(int location, Texture texture, TextureUnit unit)
    {
        texture.Bind(unit);
        SetUniform(location, unit);
    }
    
    
    public unsafe void SetUniform(int location, Matrix4x4 value)
    {
        Gl.UniformMatrix4(location, 1, false, (float*)&value);
    }


    /// <summary>
    /// Sets a uniform variable in the shader program.
    /// Consider caching uniform locations with <see cref="GetUniformLocation(string)"/> and using <see cref="SetUniform(int, int)"/> instead.
    /// Ensure the shader program is active with <see cref="Use()"/> before setting uniforms.
    /// </summary>
    public void SetUniform(string name, int value)
    {
        int location = GetUniformLocation(name);
        Gl.Uniform1(location, value);
    }


    /// <summary>
    /// Sets a uniform variable in the shader program.
    /// Consider caching uniform locations with <see cref="GetUniformLocation(string)"/> and using <see cref="SetUniform(int, float)"/> instead.
    /// Ensure the shader program is active with <see cref="Use()"/> before setting uniforms.
    /// </summary>
    public void SetUniform(string name, float value)
    {
        int location = GetUniformLocation(name);
        Gl.Uniform1(location, value);
    }


    /// <summary>
    /// Sets a uniform variable in the shader program.
    /// Consider caching uniform locations with <see cref="GetUniformLocation(string)"/> and using <see cref="SetUniform(int, TextureUnit)"/> instead.
    /// Ensure the shader program is active with <see cref="Use()"/> before setting uniforms.
    /// </summary>
    public void SetUniform(string name, TextureUnit value)
    {
        int location = GetUniformLocation(name);
        int valueInt = (int)value - (int)TextureUnit.Texture0;
        Gl.Uniform1(location, valueInt);
    }


    /// <summary>
    /// Sets a texture uniform by binding the texture to the specified texture unit and updating the uniform.
    /// Consider caching uniform locations with <see cref="GetUniformLocation(string)"/> and using <see cref="SetUniform(int, Texture, TextureUnit)"/> instead.
    /// Ensure the shader program is active with <see cref="Use()"/> before setting uniforms.
    /// </summary>
    public void SetUniform(string name, Texture texture, TextureUnit unit)
    {
        texture.Bind(unit);
        SetUniform(name, unit);
    }
    
    
    /// <summary>
    /// Sets a uniform variable in the shader program.
    /// Consider caching uniform locations with <see cref="GetUniformLocation(string)"/> and using <see cref="SetUniform(int, Matrix4x4)"/> instead.
    /// Ensure the shader program is active with <see cref="Use()"/> before setting uniforms.
    /// </summary>
    public unsafe void SetUniform(string name, Matrix4x4 value)
    {
        int location = GetUniformLocation(name);
        Gl.UniformMatrix4(location, 1, false, (float*)&value);
    }


    /// <summary>
    /// Activates this shader program for use in rendering.
    /// </summary>
    public void Use()
    {
        Gl.UseProgram(Handle);
    }


    protected override void DisposeResources(bool manual)
    {
        Gl.DeleteProgram(Handle);
    }


    private static string LoadAndPreprocessShader(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"Shader file not found: '{path}'");

        StringBuilder source = new();

        // Inject the GLSL version and common definitions.
        source.AppendLine("#version 330 core");
#if DEBUG
        source.AppendLine("#define DEBUG 1");
#endif
        source.AppendLine();

        source.Append(File.ReadAllText(path));

        return source.ToString();
    }


    private uint CompileShader(ShaderType type, string source)
    {
        uint shader = Gl.CreateShader(type);
        Gl.ShaderSource(shader, source);
        Gl.CompileShader(shader);

        Gl.GetShader(shader, ShaderParameterName.CompileStatus, out int status);
        if (status != (int)GLEnum.True)
        {
            string infoLog = Gl.GetShaderInfoLog(shader);
            Gl.DeleteShader(shader); // Don't leak the shader.
            throw new ShaderCompilationException($"Failed to compile {type} shader: {infoLog}");
        }

        return shader;
    }
}