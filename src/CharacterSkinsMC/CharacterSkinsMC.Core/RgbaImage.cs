#nullable enable
using System;
using System.Collections.Generic;
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

	/// <summary>
	/// 返回一张新图：alpha 低于阈值的像素从相邻不透明像素逐圈扩散取色补齐（把最近的不透明颜色向外生长），
	/// 用于消除 3D 预览中 UV 采样落在区域边缘/空隙上的黑边与镂空；
	/// 扩散不到的远处孤立空白才填为 fallback 色。全图处理完后没有任何半透明像素。
	/// </summary>
	public RgbaImage FillBlankExtending(RgbaColor fallback, byte alphaThreshold = 255)
	{
		var result = Clone();
		bool Enough(int x, int y) => result.GetPixel(x, y).A >= alphaThreshold;
		while (true)
		{
			var updates = new List<(int X, int Y, RgbaColor Color)>();
			for (int y = 0; y < Height; y++)
			{
				for (int x = 0; x < Width; x++)
				{
					if (Enough(x, y))
					{
						continue;
					}
					int r = 0, g = 0, b = 0, n = 0;
					CollectNeighbor(result, x - 1, y, alphaThreshold, ref r, ref g, ref b, ref n);
					CollectNeighbor(result, x + 1, y, alphaThreshold, ref r, ref g, ref b, ref n);
					CollectNeighbor(result, x, y - 1, alphaThreshold, ref r, ref g, ref b, ref n);
					CollectNeighbor(result, x, y + 1, alphaThreshold, ref r, ref g, ref b, ref n);
					if (n > 0)
					{
						updates.Add((x, y, new RgbaColor((byte)(r / n), (byte)(g / n), (byte)(b / n), 255)));
					}
				}
			}
			if (updates.Count == 0)
			{
				break;
			}
			foreach ((int x, int y, RgbaColor color) in updates)
			{
				result.SetPixel(x, y, color);
			}
		}
		// 扩散不到的远处空白（与任何内容都不相邻的大片区域）按 fallback 兜底。
		for (int y = 0; y < Height; y++)
		{
			for (int x = 0; x < Width; x++)
			{
				if (!Enough(x, y))
				{
					result.SetPixel(x, y, fallback);
				}
			}
		}
		return result;
	}

	private void CollectNeighbor(RgbaImage image, int x, int y, byte alphaThreshold, ref int r, ref int g, ref int b, ref int n)
	{
		if ((uint)x >= (uint)Width || (uint)y >= (uint)Height)
		{
			return;
		}
		RgbaColor pixel = image.GetPixel(x, y);
		if (pixel.A < alphaThreshold)
		{
			return;
		}
		r += pixel.R;
		g += pixel.G;
		b += pixel.B;
		n++;
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

	/// <summary>逐像素比较两张图；供测试与对比工具共用，不依赖文件系统。</summary>
	public bool ContentEquals(RgbaImage other)
	{
		ArgumentNullException.ThrowIfNull(other);
		if (Width != other.Width || Height != other.Height)
		{
			return false;
		}
		for (int y = 0; y < Height; y++)
		{
			for (int x = 0; x < Width; x++)
			{
				if (GetPixel(x, y) != other.GetPixel(x, y))
				{
					return false;
				}
			}
		}
		return true;
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
