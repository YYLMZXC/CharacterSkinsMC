#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace CharacterSkinsMC.Core;

/// <summary>一条 SC 原版图集区块参考线。</summary>
public sealed record ScUvRegion(string Name, Rect Rect);

/// <summary>一套 SC 原版图集 UV 布局，仅用于预览参考线，不参与皮肤转译。</summary>
public sealed class ScUvLayout
{
	private readonly Dictionary<string, Rect> m_byName;

	public string Name { get; }
	public ScPlayerClass PlayerClass { get; }
	public IReadOnlyList<ScUvRegion> Regions { get; }

	public ScUvLayout(string name, ScPlayerClass playerClass, IEnumerable<ScUvRegion> regions)
	{
		Name = name;
		PlayerClass = playerClass;
		Regions = regions.ToArray();
		m_byName = Regions.ToDictionary(region => region.Name, region => region.Rect, StringComparer.Ordinal);
	}

	public Rect? Find(string name) => m_byName.TryGetValue(name, out Rect rect) ? rect : null;
}

/// <summary>
/// SC 原版 64×64 图集区块表，按从原图像素测量的坐标维护。
/// 与 ScSkinAtlas.Regions（MC 盒面 → SC 图集的转译映射）是两回事：
/// 本表只决定预览里绿框的位置，改动它不会改变任何皮肤像素。
/// MC 分区另由 McUvLayouts 按 MinecraftSkin.GetFaceRect 生成，仅用于「MC 皮肤」标签页。
/// </summary>
public static class ScUvLayouts
{
	/// <summary>SC 男性（Walter）图集：躯干高 24，腿部自 Y=40 起。</summary>
	public static ScUvLayout Male { get; } = new("SC 男性（Walter）", ScPlayerClass.Male, new ScUvRegion[]
	{
		new("头正面", new Rect(16, 0, 16, 16)),
		new("头背面", new Rect(32, 0, 16, 16)),
		new("头右", new Rect(0, 0, 16, 16)),
		new("头左", new Rect(48, 0, 16, 16)),
		new("躯干前", new Rect(16, 16, 16, 24)),
		new("躯干后", new Rect(32, 16, 16, 24)),
		new("右臂前", new Rect(0, 16, 8, 24)),
		new("右臂后", new Rect(8, 16, 8, 24)),
		new("左臂前", new Rect(48, 16, 8, 24)),
		new("左臂后", new Rect(56, 16, 8, 24)),
		new("右腿前", new Rect(16, 40, 8, 24)),
		new("右腿后", new Rect(24, 40, 8, 24)),
		new("左腿前", new Rect(32, 40, 8, 24)),
		new("左腿后", new Rect(40, 40, 8, 24))
	});

	/// <summary>SC 女性（Doris）图集：头部与男性一致，躯干/手臂高 20，腿部上移到 Y=36。</summary>
	public static ScUvLayout Female { get; } = new("SC 女性（Doris）", ScPlayerClass.Female, new ScUvRegion[]
	{
		new("头正面", new Rect(16, 0, 16, 16)),
		new("头背面", new Rect(32, 0, 16, 16)),
		new("头右", new Rect(0, 0, 16, 16)),
		new("头左", new Rect(48, 0, 16, 16)),
		new("躯干前", new Rect(16, 16, 16, 20)),
		new("躯干后", new Rect(32, 16, 16, 20)),
		new("右臂前", new Rect(0, 16, 8, 20)),
		new("右臂后", new Rect(8, 16, 8, 20)),
		new("左臂前", new Rect(48, 16, 8, 20)),
		new("左臂后", new Rect(56, 16, 8, 20)),
		new("右腿前", new Rect(16, 36, 8, 24)),
		new("右腿后", new Rect(24, 36, 8, 24)),
		new("左腿前", new Rect(32, 36, 8, 24)),
		new("左腿后", new Rect(40, 36, 8, 24))
	});

	public static ScUvLayout Get(ScPlayerClass playerClass) =>
		playerClass == ScPlayerClass.Female ? Female : Male;
}
