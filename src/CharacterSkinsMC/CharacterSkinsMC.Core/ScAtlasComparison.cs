#nullable enable
using System;

namespace CharacterSkinsMC.Core;

/// <summary>两张 64×64 皮肤图集的逐像素对比结果。</summary>
public readonly record struct ScAtlasDiff(int TotalPixels, int DifferentPixels, double Similarity)
{
	public int SamePixels => TotalPixels - DifferentPixels;
}

/// <summary>
/// 转译结果与内置默认皮肤的对比工具：统计差异像素并渲染差异高亮图。
/// 差异图中灰色表示两张图一致，红色表示不一致，棋盘格表示两者都透明。
/// </summary>
public static class ScAtlasComparison
{
	public const int AtlasSize = ScSkinAtlas.Size;

	public static ScAtlasDiff Compare(RgbaImage current, RgbaImage reference)
	{
		ArgumentNullException.ThrowIfNull(current);
		ArgumentNullException.ThrowIfNull(reference);
		Validate(current);
		Validate(reference);
		int total = current.Width * current.Height;
		int different = 0;
		for (int y = 0; y < current.Height; y++)
		{
			for (int x = 0; x < current.Width; x++)
			{
				if (!SamePixel(current.GetPixel(x, y), reference.GetPixel(x, y)))
				{
					different++;
				}
			}
		}
		return new ScAtlasDiff(total, different, total == 0 ? 1.0 : 1.0 - (double)different / total);
	}

	/// <summary>差异图上的参考线由调用方按当前预览对象选定 SC 原版分区，不使用转译岛矩形。</summary>
	public static RgbaImage RenderDifference(RgbaImage current, RgbaImage reference, int scale, bool guides, ScUvLayout layout)
	{
		ArgumentNullException.ThrowIfNull(current);
		ArgumentNullException.ThrowIfNull(reference);
		ArgumentNullException.ThrowIfNull(layout);
		Validate(current);
		Validate(reference);
		scale = Math.Max(1, scale);
		int size = AtlasSize * scale;
		var result = new RgbaImage(size, size);
		DrawCheckerboard(result, size, Math.Max(4, scale * 8));
		for (int y = 0; y < AtlasSize; y++)
		{
			for (int x = 0; x < AtlasSize; x++)
			{
				RgbaColor currentPixel = current.GetPixel(x, y);
				RgbaColor referencePixel = reference.GetPixel(x, y);
				RgbaColor color;
				if (SamePixel(currentPixel, referencePixel))
				{
					if (currentPixel.A == 0)
					{
						continue;
					}
					byte gray = (byte)Math.Clamp((currentPixel.R * 77 + currentPixel.G * 150 + currentPixel.B * 29) / 256, 0, 255);
					color = new RgbaColor(gray, gray, gray, currentPixel.A);
				}
				else
				{
					color = new RgbaColor(232, 62, 72, 255);
				}
				FillBlock(result, x * scale, y * scale, scale, scale, color);
			}
		}
		DrawGrid(result, size, scale);
		if (guides)
		{
			foreach (ScUvRegion region in layout.Regions)
			{
				DrawOutline(result, region.Rect, scale, new RgbaColor(40, 40, 48, 120));
			}
		}
		return result;
	}

	private static bool SamePixel(RgbaColor a, RgbaColor b)
	{
		// 透明像素的 RGB 残留值不参与比较，避免 PNG 编码差异造成误报。
		if (a.A == 0 && b.A == 0)
		{
			return true;
		}
		return a.R == b.R && a.G == b.G && a.B == b.B && a.A == b.A;
	}

	private static void Validate(RgbaImage image)
	{
		if (image.Width != AtlasSize || image.Height != AtlasSize)
		{
			throw new InvalidOperationException($"对比仅支持 {AtlasSize}×{AtlasSize} 图集，实际为 {image.Width}×{image.Height}。");
		}
	}

	private static void DrawCheckerboard(RgbaImage image, int size, int tile)
	{
		var light = new RgbaColor(205, 208, 214, 255);
		var dark = new RgbaColor(170, 174, 182, 255);
		for (int y = 0; y < size; y += tile)
		{
			for (int x = 0; x < size; x += tile)
			{
				FillBlock(image, x, y, Math.Min(tile, size - x), Math.Min(tile, size - y),
					((x / tile + y / tile) & 1) == 0 ? light : dark);
			}
		}
	}

	private static void DrawGrid(RgbaImage image, int size, int scale)
	{
		int step = 8 * scale;
		var color = new RgbaColor(70, 74, 82, 70);
		for (int position = 0; position <= size; position += step)
		{
			if (position < size)
			{
				FillBlock(image, position, 0, 1, size, color);
				FillBlock(image, 0, position, size, 1, color);
			}
		}
	}

	private static void DrawOutline(RgbaImage image, Rect rect, int scale, RgbaColor color)
	{
		int x = rect.X * scale, y = rect.Y * scale;
		int width = rect.W * scale, height = rect.H * scale;
		FillBlock(image, x, y, width, 1, color);
		FillBlock(image, x, y + height - 1, width, 1, color);
		FillBlock(image, x, y, 1, height, color);
		FillBlock(image, x + width - 1, y, 1, height, color);
	}

	private static void FillBlock(RgbaImage image, int x0, int y0, int width, int height, RgbaColor color)
	{
		for (int y = Math.Max(0, y0); y < Math.Min(image.Height, y0 + height); y++)
		{
			for (int x = Math.Max(0, x0); x < Math.Min(image.Width, x0 + width); x++)
			{
				image.SetPixel(x, y, RgbaColor.Blend(image.GetPixel(x, y), color));
			}
		}
	}
}
