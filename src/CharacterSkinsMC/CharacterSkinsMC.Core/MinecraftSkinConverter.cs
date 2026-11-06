#nullable enable
using System;
using System.IO;

namespace CharacterSkinsMC.Core;

/// <summary>Minecraft 皮肤转 Survivalcraft 皮肤的可复用公开 API。</summary>
public static class MinecraftSkinConverter
{
	public const int AtlasSize = ScSkinAtlas.Size;

	/// <summary>
	/// 生成 64×64 的 Survivalcraft 皮肤图集。
	/// 注意：目标矩形按图片自上而下的行坐标定义，已抵消 Human* 模型 UV 的 V 方向差异；
	/// 产物是直接可被 Texture2D.Load 读取的 PNG 字节内容，扩展名用 .scskin 即可。
	/// </summary>
	public static RgbaImage Convert(MinecraftSkin skin, bool composeOverlay = true, bool fillBlank = false)
	{
		ArgumentNullException.ThrowIfNull(skin);
		RgbaImage source = composeOverlay ? skin.Composed : skin.Base;
		var atlas = new RgbaImage(AtlasSize, AtlasSize);
		foreach (ScRegion region in ScSkinAtlas.GetRegions(skin.ArmModel))
		{
			Rect faceRect = skin.GetFaceRect(region.Part, region.Face);
			for (int dy = 0; dy < region.Dest.H; dy++)
			{
				for (int dx = 0; dx < region.Dest.W; dx++)
				{
					double tx = (dx + 0.5) / region.Dest.W;
					double ty = (dy + 0.5) / region.Dest.H;
					double u = region.MirrorX
						? region.U1 - tx * (region.U1 - region.U0)
						: region.U0 + tx * (region.U1 - region.U0);
					double v = region.V0 + ty * (region.V1 - region.V0);
					int sourceX = faceRect.X + SampleColumn(region, u, dx, faceRect.W);
					int sourceY = faceRect.Y + Math.Clamp((int)(v * faceRect.H), 0, faceRect.H - 1);
					atlas.SetPixel(region.Dest.X + dx, region.Dest.Y + dy, source.GetPixel(sourceX, sourceY));
				}
			}
		}
		if (fillBlank)
		{
			// 鞘翅补齐：游戏按 UV 采样图集，落在已贴图区域边缘/空隙的透明像素会渲染成黑块；
			// 转译后把空白从相邻已贴图像素扩散补齐，手臂、肩膀等部位不再出现黑色漏贴图。
			atlas = atlas.FillBlankExtending(new RgbaColor(0, 0, 0, 255));
		}
		return atlas;
	}

	/// <summary>
	/// 目标列 → 源面列。横跨整个盒面的区块（SymmetricX，如整张脸）按中线成对取整：
	/// 左半取上边界、右半取下边界，8→10 得到 [0,1,1,2,3,4,5,6,6,7]，
	/// 关于源列 3/4 镜像，额外列落在两侧，眼睛等细节保持原宽度。
	/// 若两半都按常规下取整，会得到 [0,1,2,2,3,4,5,6,6,7]：
	/// 源列 2（左眼）被复制，其镜像列 5（右眼）没有，两眼于是一大一小。
	/// </summary>
	private static int SampleColumn(ScRegion region, double u, int dx, int faceWidth)
	{
		double position = u * faceWidth;
		double column = region.SymmetricX && dx < region.Dest.W / 2
			? Math.Ceiling(position) - 1
			: Math.Floor(position);
		return Math.Clamp((int)column, 0, faceWidth - 1);
	}

	/// <summary>输出可被游戏直接导入的 .scskin 字节（PNG 内容）。</summary>
	public static byte[] ToScSkinBytes(RgbaImage atlas)
	{
		ArgumentNullException.ThrowIfNull(atlas);
		if (atlas.Width != AtlasSize || atlas.Height != AtlasSize)
		{
			throw new InvalidOperationException($"Survivalcraft 皮肤图集必须为 {AtlasSize}×{AtlasSize}。");
		}
		return atlas.ToPngBytes();
	}

	/// <summary>把图集写成 .scskin 文件；未带扩展名时自动补全。</summary>
	public static string SaveScSkin(RgbaImage atlas, string path)
	{
		ArgumentNullException.ThrowIfNull(atlas);
		if (string.IsNullOrWhiteSpace(path))
		{
			throw new ArgumentException("输出路径不能为空。", nameof(path));
		}
		string directory = Path.GetDirectoryName(Path.GetFullPath(path)) ?? throw new ArgumentException("输出路径无效。", nameof(path));
		Directory.CreateDirectory(directory);
		string fullPath = Path.ChangeExtension(Path.GetFullPath(path), ".scskin");
		atlas.SavePng(fullPath);
		return fullPath;
	}
}
