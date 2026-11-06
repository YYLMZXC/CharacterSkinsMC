#nullable enable
using System;
using System.Collections.Generic;

namespace CharacterSkinsMC.Core;

/// <summary>一张随程序集内嵌的 Minecraft 示例皮肤；DisplayName 为界面展示名。</summary>
public sealed record McBuiltinSkin(string Id, string DisplayName, string ResourceName);

/// <summary>
/// 内置的 Minecraft 示例皮肤（res 文件夹收录，64×64 双层 PNG）。
/// 内嵌后不依赖输入目录，空目录或 Android 沙盒下也能直接转换预览。
/// </summary>
public static class McBuiltinSkins
{
	private static readonly McBuiltinSkin[] s_all = new McBuiltinSkin[]
	{
		new("Alice", "爱丽丝·玛格特罗伊德", "AliceMargatroid.png"),
		new("Cirno", "琪露诺", "Cirno.png")
	};

	public static IReadOnlyList<McBuiltinSkin> All => s_all;

	public static McBuiltinSkin? Find(string id) =>
		s_all.FirstOrDefault(skin => string.Equals(skin.Id, id, StringComparison.OrdinalIgnoreCase));

	/// <summary>读取内嵌 PNG 字节。</summary>
	public static byte[] ReadPng(McBuiltinSkin skin)
	{
		ArgumentNullException.ThrowIfNull(skin);
		return EmbeddedAssets.Read(skin.ResourceName);
	}

	/// <summary>按当前手臂模型把内嵌皮肤加载成已归一化的 MinecraftSkin。</summary>
	public static MinecraftSkin Load(McBuiltinSkin skin, McArmModel requestedArmModel)
	{
		ArgumentNullException.ThrowIfNull(skin);
		return MinecraftSkin.Load(ReadPng(skin), requestedArmModel);
	}
}
