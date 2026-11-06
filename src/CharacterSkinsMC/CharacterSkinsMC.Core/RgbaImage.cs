#nullable enable
using System;
using System.IO;
using StbImageSharp;
using StbImageWriteSharp;

namespace CharacterSkinsMC.Core;

/// <summary>像素顺序固定为 RGBA 的位图容器，全部转换逻辑只依赖本类型，不绑定具体 UI 框架。</summary>
public sealed class RgbaImage
{
	public int Width { get; }
	public int Height { get; }
	private readonly byte[] m_pixels;

	public RgbaImage(int width, int height)
	{
		if (width <= 0 || height <= 0)
		{
			throw new ArgumentOutOfRangeException(nameof(width), "图像宽高必须为正数。");
		}
		Width = width;
		Height = height;
		m_pixels = new byte[checked(width * height * 4)];
	}

	public RgbaColor GetPixel(int x, int y)
	{
		if ((uint)x >= (uint)Width || (uint)y >= (uint)Height)
		{
			return RgbaColor.Transparent;
		}
		int index = (y * Width + x) * 4;
		return new RgbaColor(m_pixels[index], m_pixels[index + 1], m_pixels[index + 2], m_pixels[index + 3]);
	}

	public void SetPixel(int x, int y, RgbaColor color)
	{
		if ((uint)x >= (uint)Width || (uint)y >= (uint)Height)
		{
			return;
		}
		int index = (y * Width + x) * 4;
		m_pixels[index] = color.R;
		m_pixels[index + 1] = color.G;
		m_pixels[index + 2] = color.B;
		m_pixels[index + 3] = color.A;
	}

	public RgbaImage Clone()
	{
		var copy = new RgbaImage(Width, Height);
		Array.Copy(m_pixels, copy.m_pixels, m_pixels.Length);
		return copy;
	}

	public static RgbaImage FromFile(string path)
	{
		if (!File.Exists(path))
		{
			throw new FileNotFoundException("找不到皮肤文件。", path);
		}
		return FromMemory(File.ReadAllBytes(path));
	}

	/// <summary>解码任意常见格式（PNG/JPEG 等）的图像字节。System.Drawing 在 Android 上不可用，改用纯托管 stb 编解码。</summary>
	public static RgbaImage FromMemory(byte[] bytes)
	{
		ArgumentNullException.ThrowIfNull(bytes);
		ImageResult result = ImageResult.FromMemory(bytes, StbImageSharp.ColorComponents.RedGreenBlueAlpha);
		var image = new RgbaImage(result.Width, result.Height);
		Buffer.BlockCopy(result.Data, 0, image.m_pixels, 0, image.m_pixels.Length);
		return image;
	}

	public void SavePng(string path)
	{
		File.WriteAllBytes(path, ToPngBytes());
	}

	public byte[] ToPngBytes()
	{
		var writer = new ImageWriter();
		using var stream = new MemoryStream();
		writer.WritePng(m_pixels, Width, Height, StbImageWriteSharp.ColorComponents.RedGreenBlueAlpha, stream);
		return stream.ToArray();
	}
}

/// <summary>非预乘的 RGBA 颜色。</summary>
public readonly record struct RgbaColor(byte R, byte G, byte B, byte A)
{
	public static RgbaColor Transparent => new(0, 0, 0, 0);

	/// <summary>把 src 按自身 alpha 叠到 dst 之上，用于合成 Minecraft 皮肤第二层。</summary>
	public static RgbaColor Blend(RgbaColor dst, RgbaColor src)
	{
		if (src.A == 0)
		{
			return dst;
		}
		if (src.A == 255)
		{
			return src;
		}
		int srcAlpha = src.A;
		int inverse = 255 - srcAlpha;
		int dstAlpha = dst.A;
		int outAlpha = srcAlpha + dstAlpha * inverse / 255;
		if (outAlpha <= 0)
		{
			return Transparent;
		}
		int r = (src.R * srcAlpha + dst.R * dstAlpha * inverse / 255) / outAlpha;
		int g = (src.G * srcAlpha + dst.G * dstAlpha * inverse / 255) / outAlpha;
		int b = (src.B * srcAlpha + dst.B * dstAlpha * inverse / 255) / outAlpha;
		return new RgbaColor(
			(byte)Math.Clamp(r, 0, 255),
			(byte)Math.Clamp(g, 0, 255),
			(byte)Math.Clamp(b, 0, 255),
			(byte)Math.Clamp(outAlpha, 0, 255));
	}
}
