using Silk.NET.OpenGL;
using StbImageSharp;

namespace Silt.Core.Graphics;

/// <summary>
/// Represents a 2D texture on the GPU.
/// </summary>
public sealed class Texture : GraphicsResource
{
    /// <summary>
    /// The width of the texture in pixels.
    /// </summary>
    public readonly uint Width;

    /// <summary>
    /// The height of the texture in pixels.
    /// </summary>
    public readonly uint Height;


    /// <summary>
    /// Creates a new texture from an image file.
    /// </summary>
    /// <param name="gl">The OpenGL context.</param>
    /// <param name="path">The file path to the image.</param>
    /// <exception cref="FileNotFoundException">Thrown if the image file cannot be found.</exception>
    public unsafe Texture(GL gl, string path) : base(gl)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"Texture file not found: '{path}'");

        // Load image data from file
        ImageResult result = ImageResult.FromMemory(File.ReadAllBytes(path), ColorComponents.RedGreenBlueAlpha);
        Width = (uint)result.Width;
        Height = (uint)result.Height;

        Handle = Gl.GenTexture();
        Bind(TextureUnit.Texture0);

        // Upload texture data to the GPU
        fixed (void* data = result.Data)
        {
            Gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba, Width, Height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, data);
        }

        SetTextureParameters();

        Unbind();
    }
    
    
    public unsafe Texture(GL gl, ReadOnlySpan<byte> pixelData, uint width, uint height) : base(gl)
    {
        Width = width;
        Height = height;

        Handle = Gl.GenTexture();
        Bind(TextureUnit.Texture0);

        // Upload texture data to the GPU
        fixed (void* data = pixelData)
        {
            Gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba, Width, Height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, data);
        }

        SetTextureParameters();

        Unbind();
    }
    
    
    public unsafe Texture(GL gl, uint width, uint height) : base(gl)
    {
        Width = width;
        Height = height;

        Handle = Gl.GenTexture();
        Bind(TextureUnit.Texture0);

        // Allocate empty texture data on the GPU
        Gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba, Width, Height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, null);

        SetTextureParameters();

        Unbind();
    }


    /// <summary>
    /// Binds the texture to a specific texture unit.
    /// </summary>
    /// <param name="unit">The texture unit to bind to.</param>
    public void Bind(TextureUnit unit)
    {
        Gl.ActiveTexture(unit);
        Gl.BindTexture(TextureTarget.Texture2D, Handle);
    }


    /// <summary>
    /// Unbinds the texture from the current texture unit.
    /// </summary>
    public void Unbind()
    {
        Gl.BindTexture(TextureTarget.Texture2D, 0);
    }


    protected override void DisposeResources(bool manual)
    {
        Gl.DeleteTexture(Handle);
    }


    private void SetTextureParameters()
    {
        // When scaling down, use linear filtering between mipmap levels
        Gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.LinearMipmapLinear);

        // When scaling up, use linear filtering
        Gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Linear);

        // Repeat the texture if texture coordinates are outside the [0, 1] range
        Gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.Repeat);
        Gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.Repeat);
        
        // Set mipmap levels
        Gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureBaseLevel, 0);
        Gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMaxLevel, 4);

        // Generate mipmaps
        Gl.GenerateMipmap(TextureTarget.Texture2D);
    }
}