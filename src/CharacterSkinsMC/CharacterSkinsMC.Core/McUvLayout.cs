#nullable enable
using System;
using System.Collections.Generic;

namespace CharacterSkinsMC.Core;

/// <summary>一条 Minecraft 原版皮肤分区参考线，第二层单独成项。</summary>
public sealed record McUvRegion(string Name, Rect Rect, bool IsOverlay);

/// <summary>
/// Minecraft 64×64 双层皮肤的 UV 分区配置，仅用于「MC 皮肤」页的参考线。
/// 坐标仍以 MinecraftSkin.GetFaceRect 为唯一事实源，这里只做显式枚举，避免与 SC 分区混用。
/// </summary>
public sealed class McUvLayout
{
	public IReadOnlyList<McUvRegion> Regions { get; }

	public McUvLayout(MinecraftSkin skin)
	{
		ArgumentNullException.ThrowIfNull(skin);
		var regions = new List<McUvRegion>();
		foreach (McBodyPart part in Enum.GetValues<McBodyPart>())
		{
			foreach (McFace face in Enum.GetValues<McFace>())
			{
				regions.Add(new McUvRegion($"{part}/{face} 基础层", skin.GetFaceRect(part, face), false));
				if (skin.HasOverlay)
				{
					regions.Add(new McUvRegion($"{part}/{face} 第二层", skin.GetFaceRect(part, face, overlay: true), true));
				}
			}
		}
		Regions = regions;
	}
}

public static class McUvLayouts
{
	/// <summary>按皮肤的手臂模型与第二层存在性生成 MC 分区表。</summary>
	public static McUvLayout Create(MinecraftSkin skin) => new(skin);
}
