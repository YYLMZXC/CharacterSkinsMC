#nullable enable
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

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
		using var bitmap = new Bitmap(path);
		return FromBitmap(bitmap);
	}

	public static RgbaImage FromBitmap(Bitmap source)
	{
		using var clone = source.Clone(new Rectangle(0, 0, source.Width, source.Height), PixelFormat.Format32bppArgb);
		BitmapData data = clone.LockBits(new Rectangle(0, 0, clone.Width, clone.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
		try
		{
			byte[] buffer = new byte[data.Stride * clone.Height];
			Marshal.Copy(data.Scan0, buffer, 0, buffer.Length);
			var image = new RgbaImage(clone.Width, clone.Height);
			for (int y = 0; y < clone.Height; y++)
			{
				for (int x = 0; x < clone.Width; x++)
				{
					int index = y * data.Stride + x * 4;
					// GDI+ 的 Format32bppArgb 内存顺序为 BGRA，此处统一转成 RGBA。
					image.SetPixel(x, y, new RgbaColor(buffer[index + 2], buffer[index + 1], buffer[index], buffer[index + 3]));
				}
			}
			return image;
		}
		finally
		{
			clone.UnlockBits(data);
		}
	}

	public Bitmap ToBitmap()
	{
		var bitmap = new Bitmap(Width, Height, PixelFormat.Format32bppArgb);
		BitmapData data = bitmap.LockBits(new Rectangle(0, 0, Width, Height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
		try
		{
			byte[] buffer = new byte[data.Stride * Height];
			for (int y = 0; y < Height; y++)
			{
				for (int x = 0; x < Width; x++)
				{
					RgbaColor color = GetPixel(x, y);
					int index = y * data.Stride + x * 4;
					buffer[index] = color.B;
					buffer[index + 1] = color.G;
					buffer[index + 2] = color.R;
					buffer[index + 3] = color.A;
				}
			}
			Marshal.Copy(buffer, 0, data.Scan0, buffer.Length);
		}
		finally
		{
			bitmap.UnlockBits(data);
		}
		return bitmap;
	}

	public void SavePng(string path)
	{
		using var bitmap = ToBitmap();
		bitmap.Save(path, ImageFormat.Png);
	}

	public byte[] ToPngBytes()
	{
		using var stream = new MemoryStream();
		using (var bitmap = ToBitmap())
		{
			bitmap.Save(stream, ImageFormat.Png);
		}
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
