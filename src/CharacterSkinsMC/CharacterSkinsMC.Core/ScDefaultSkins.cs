#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CharacterSkinsMC.Core;

/// <summary>Survivalcraft 人物性别，决定使用哪一套人物模型与内置默认皮肤。</summary>
public enum ScPlayerClass
{
	Male,
	Female
}

/// <summary>一张内置默认皮肤；DisplayName 与游戏 CharacterSkinsManager 的显示名一致。</summary>
public sealed record ScDefaultSkin(
	string Id,
	string DisplayName,
	ScPlayerClass PlayerClass,
	int Index,
	string ResourceName)
{
	public string Caption => $"{DisplayName}（{(PlayerClass == ScPlayerClass.Male ? "男" : "女")} {Index}）";
}

/// <summary>
/// 本仓内置的 8 张 Survivalcraft 默认人物皮肤：男 4 张、女 4 张。
/// 资源由 Content/Assets/Textures/Creatures/Human*.webp 转成 PNG 后内嵌，保持原始 64×64 图集布局。
/// </summary>
public static class ScDefaultSkins
{
	private static readonly ScDefaultSkin[] s_all = new ScDefaultSkin[]
	{
		new("$Male1", "Walter", ScPlayerClass.Male, 1, "HumanMale1.png"),
		new("$Male2", "Basil", ScPlayerClass.Male, 2, "HumanMale2.png"),
		new("$Male3", "Geoffrey", ScPlayerClass.Male, 3, "HumanMale3.png"),
		new("$Male4", "Zachary", ScPlayerClass.Male, 4, "HumanMale4.png"),
		new("$Female1", "Doris", ScPlayerClass.Female, 1, "HumanFemale1.png"),
		new("$Female2", "Mabel", ScPlayerClass.Female, 2, "HumanFemale2.png"),
		new("$Female3", "Ada", ScPlayerClass.Female, 3, "HumanFemale3.png"),
		new("$Female4", "Shirley", ScPlayerClass.Female, 4, "HumanFemale4.png")
	};

	public static IReadOnlyList<ScDefaultSkin> All => s_all;

	public static IReadOnlyList<ScDefaultSkin> GetByClass(ScPlayerClass playerClass) =>
		s_all.Where(skin => skin.PlayerClass == playerClass).ToArray();

	public static ScDefaultSkin? Find(string id) =>
		s_all.FirstOrDefault(skin => string.Equals(skin.Id, id, StringComparison.OrdinalIgnoreCase));

	/// <summary>读取内置皮肤的 64×64 图集；它本身就是可直接使用的 .scskin 内容。</summary>
	public static RgbaImage LoadSkin(ScDefaultSkin skin)
	{
		ArgumentNullException.ThrowIfNull(skin);
		RgbaImage atlas = RgbaImage.FromMemory(EmbeddedAssets.Read(skin.ResourceName));
		if (atlas.Width != ScSkinAtlas.Size || atlas.Height != ScSkinAtlas.Size)
		{
			throw new InvalidDataException($"内置皮肤 {skin.Id} 尺寸异常：{atlas.Width}×{atlas.Height}");
		}
		return atlas;
	}

	/// <summary>读取对应性别的人物 DAE 模型，已应用场景节点变换。</summary>
	public static ScModel LoadModel(ScPlayerClass playerClass) => ScModel.Load(
		EmbeddedAssets.Read(playerClass == ScPlayerClass.Male ? "HumanMale.dae" : "HumanFemale.dae"));

	/// <summary>把内置皮肤导出成可直接放进游戏皮肤目录的 .scskin 文件。</summary>
	public static string SaveSkin(ScDefaultSkin skin, string outputPath)
	{
		ArgumentNullException.ThrowIfNull(skin);
		return MinecraftSkinConverter.SaveScSkin(LoadSkin(skin), outputPath);
	}
}
