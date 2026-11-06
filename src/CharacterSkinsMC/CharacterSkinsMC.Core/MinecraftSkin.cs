#nullable enable
using System;
using System.IO;

namespace CharacterSkinsMC.Core;

public enum McBodyPart
{
	Head,
	Body,
	RightArm,
	LeftArm,
	RightLeg,
	LeftLeg
}

public enum McFace
{
	Top,
	Bottom,
	Right,
	Front,
	Left,
	Back
}

/// <summary>Minecraft 手臂宽度模型；PNG 自身不携带该信息，需要由使用者指定。</summary>
public enum McArmModel
{
	Classic,
	Slim
}

/// <summary>
/// 已归一化为 64×64 的 Minecraft 皮肤。Base 保留原始布局，Composed 把第二层叠回各面部区域，
/// OverlayLayer 只保留第二层的 alpha 内容，供 3D 预览绘制外壳。
/// </summary>
public sealed class MinecraftSkin
{
	public const int Size = 64;

	private static readonly McBodyPart[] s_parts = Enum.GetValues<McBodyPart>();
	private static readonly McFace[] s_faces = Enum.GetValues<McFace>();

	public RgbaImage Base { get; }
	public RgbaImage Composed { get; }
	public RgbaImage OverlayLayer { get; }
	public bool HasOverlay { get; }
	public McArmModel ArmModel { get; }
	/// <summary>源皮肤相对 64×64 基准的分辨率倍数：1=16x、2=32x、4=64x、8=128x。</summary>
	public int SourceScale { get; }
	public string SourceDescription { get; }

	private MinecraftSkin(RgbaImage normalized, bool hasOverlay, McArmModel armModel, string description, int sourceScale)
	{
		Base = normalized;
		HasOverlay = hasOverlay;
		ArmModel = armModel;
		SourceScale = sourceScale;
		SourceDescription = description;
		Composed = normalized.Clone();
		OverlayLayer = new RgbaImage(Size, Size);
		ComposeOverlayIntoFacePositions();
	}

	public static MinecraftSkin Load(string path, McArmModel requestedArmModel)
	{
		if (!File.Exists(path))
		{
			throw new FileNotFoundException("找不到皮肤文件。", path);
		}
		return Load(File.ReadAllBytes(path), requestedArmModel);
	}

	/// <summary>从 PNG 字节加载（供内嵌皮肤等无文件路径来源使用）。</summary>
	public static MinecraftSkin Load(byte[] png, McArmModel requestedArmModel)
	{
		RgbaImage source = RgbaImage.FromMemory(png);
		int width = source.Width;
		int height = source.Height;

		if (width == 64 && height == 32)
		{
			// 64×32 旧版皮肤只有右侧四肢，左臂/左腿按官方规则水平镜像补齐；旧版没有第二层，也没有细手模型，固定宽手。
			return new MinecraftSkin(NormalizeLegacy(source), false, McArmModel.Classic, "64×32 旧版皮肤（已镜像补齐左侧四肢）", 1);
		}
		if (width == 64 && height == 64)
		{
			return new MinecraftSkin(source, true, requestedArmModel, "64×64 皮肤（16x）", 1);
		}
		if (width == height && width is 128 or 256 or 512)
		{
			// 32x / 64x / 128x 高清皮肤：按各自倍数降采样回 64×64，规则见 DownsampleHd。
			int scale = width / 64;
			return new MinecraftSkin(DownsampleHd(source, scale), true, requestedArmModel,
				$"{width}×{height} 高清皮肤（{scale * 16}x，已按 {scale} 倍降采样）", scale);
		}
		throw new InvalidDataException($"不支持的皮肤尺寸 {width}×{height}；仅支持 64×32、64×64、128×128、256×256 与 512×512。");
	}

	/// <summary>取得指定部位、指定面的源矩形；overlay 为 true 时返回第二层对应区域。</summary>
	public Rect GetFaceRect(McBodyPart part, McFace face, bool overlay = false)
	{
		int armWidth = ArmModel == McArmModel.Slim ? 3 : 4;
		int faceWidth = part switch
		{
			McBodyPart.Head => 8,
			McBodyPart.Body => 8,
			McBodyPart.RightArm or McBodyPart.LeftArm => armWidth,
			_ => 4
		};
		int depth = part switch
		{
			McBodyPart.Head => 8,
			McBodyPart.Body => 4,
			McBodyPart.RightArm or McBodyPart.LeftArm => armWidth,
			_ => 4
		};
		int bodyHeight = part == McBodyPart.Head ? 8 : 12;

		int x;
		int y;
		if (!overlay)
		{
			(x, y) = part switch
			{
				McBodyPart.Head => (0, 0),
				McBodyPart.Body => (16, 16),
				McBodyPart.RightArm => (40, 16),
				McBodyPart.RightLeg => (0, 16),
				McBodyPart.LeftArm => (32, 48),
				McBodyPart.LeftLeg => (16, 48),
				_ => throw new ArgumentOutOfRangeException(nameof(part))
			};
		}
		else
		{
			(x, y) = part switch
			{
				McBodyPart.Head => (32, 0),
				McBodyPart.Body => (16, 32),
				McBodyPart.RightArm => (40, 32),
				McBodyPart.RightLeg => (0, 32),
				McBodyPart.LeftArm => (48, 48),
				McBodyPart.LeftLeg => (0, 48),
				_ => throw new ArgumentOutOfRangeException(nameof(part))
			};
		}

		int originX = x + depth;
		int originY = y + depth;
		return face switch
		{
			McFace.Top => new Rect(originX, y, faceWidth, depth),
			McFace.Bottom => new Rect(originX + faceWidth, y, faceWidth, depth),
			McFace.Right => new Rect(x, originY, depth, bodyHeight),
			McFace.Front => new Rect(originX, originY, faceWidth, bodyHeight),
			McFace.Left => new Rect(originX + faceWidth, originY, depth, bodyHeight),
			McFace.Back => new Rect(originX + faceWidth + depth, originY, faceWidth, bodyHeight),
			_ => throw new ArgumentOutOfRangeException(nameof(face))
		};
	}

	private void ComposeOverlayIntoFacePositions()
	{
		if (!HasOverlay)
		{
			return;
		}
		foreach (McBodyPart part in s_parts)
		{
			foreach (McFace face in s_faces)
			{
				Rect baseRect = GetFaceRect(part, face);
				Rect overlayRect = GetFaceRect(part, face, overlay: true);
				for (int y = 0; y < baseRect.H; y++)
				{
					for (int x = 0; x < baseRect.W; x++)
					{
						RgbaColor baseColor = Base.GetPixel(baseRect.X + x, baseRect.Y + y);
						RgbaColor overlayColor = Base.GetPixel(overlayRect.X + x, overlayRect.Y + y);
						Composed.SetPixel(baseRect.X + x, baseRect.Y + y, RgbaColor.Blend(baseColor, overlayColor));
						OverlayLayer.SetPixel(baseRect.X + x, baseRect.Y + y, overlayColor);
					}
				}
			}
		}
	}

	private static RgbaImage NormalizeLegacy(RgbaImage source)
	{
		var result = new RgbaImage(Size, Size);
		for (int y = 0; y < 32; y++)
		{
			for (int x = 0; x < 64; x++)
			{
				result.SetPixel(x, y, source.GetPixel(x, y));
			}
		}
		CopyMirrored(source, new Rect(0, 16, 16, 16), result, new Rect(16, 48, 16, 16));
		CopyMirrored(source, new Rect(40, 16, 16, 16), result, new Rect(32, 48, 16, 16));
		return result;
	}

	private static void CopyMirrored(RgbaImage source, Rect sourceRect, RgbaImage destination, Rect destinationRect)
	{
		for (int y = 0; y < destinationRect.H; y++)
		{
			for (int x = 0; x < destinationRect.W; x++)
			{
				int sourceX = sourceRect.X + sourceRect.W - 1 - x;
				int sourceY = sourceRect.Y + y;
				destination.SetPixel(destinationRect.X + x, destinationRect.Y + y, source.GetPixel(sourceX, sourceY));
			}
		}
	}

	/// <summary>
	/// 高清降采样，按源分辨率分级：
	/// 32x（2 倍）取每个 2×2 块的中心像素，保留像素画锐利边缘；
	/// 64x / 128x（4、8 倍）改用 alpha 加权的区域平均（premultiply），避免高频细节缩成噪点。
	/// </summary>
	private static RgbaImage DownsampleHd(RgbaImage source, int scale)
	{
		var result = new RgbaImage(Size, Size);
		if (scale == 2)
		{
			for (int y = 0; y < Size; y++)
			{
				for (int x = 0; x < Size; x++)
				{
					result.SetPixel(x, y, source.GetPixel(x * 2 + 1, y * 2 + 1));
				}
			}
			return result;
		}
		for (int y = 0; y < Size; y++)
		{
			for (int x = 0; x < Size; x++)
			{
				int rSum = 0, gSum = 0, bSum = 0, aSum = 0;
				for (int dy = 0; dy < scale; dy++)
				{
					for (int dx = 0; dx < scale; dx++)
					{
						RgbaColor c = source.GetPixel(x * scale + dx, y * scale + dy);
						rSum += c.R * c.A;
						gSum += c.G * c.A;
						bSum += c.B * c.A;
						aSum += c.A;
					}
				}
				int block = scale * scale;
				byte a = (byte)((aSum + block / 2) / block);
				// RGB 按 alpha 预乘平均，防止透明黑边污染缩小后的颜色。
				byte r = aSum == 0 ? (byte)0 : (byte)((rSum + aSum / 2) / aSum);
				byte g = aSum == 0 ? (byte)0 : (byte)((gSum + aSum / 2) / aSum);
				byte b = aSum == 0 ? (byte)0 : (byte)((bSum + aSum / 2) / aSum);
				result.SetPixel(x, y, new RgbaColor(r, g, b, a));
			}
		}
		return result;
	}
}
