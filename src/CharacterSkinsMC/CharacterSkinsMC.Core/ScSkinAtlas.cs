#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace CharacterSkinsMC.Core;

/// <summary>
/// 一条转译规则：把 Minecraft 某个面的 [U0,U1]×[V0,V1] 区间，按最近邻采样写入转译产物的目标矩形。
/// 矩形边界来自本仓 HumanMale.dae 的 UV 岛与内置 HumanMale1.webp 的不透明区域实测。
/// </summary>
/// <remarks>
/// 这些矩形是「MC 像素的落点」，不是 Survivalcraft 原版 64×64 图集的 UV 分区；
/// 预览参考线一律使用 ScUvLayouts 的男/女两套原版分区，本表只供 MinecraftSkinConverter 生成像素。
/// </remarks>
public sealed record ScRegion(
	string Name,
	Rect Dest,
	McBodyPart Part,
	McFace Face,
	double U0 = 0.0,
	double U1 = 1.0,
	double V0 = 0.0,
	double V1 = 1.0,
	bool MirrorX = false);

/// <summary>
/// MC 盒面 → SC 图集的转译规则集合，以及转译产物的固定尺寸。
/// 本表不是原版皮肤 UV 分区，详见 ScUvLayouts。
/// </summary>
public static class ScSkinAtlas
{
	public const int Size = 64;

	private static ScRegion P(string name, int x, int y, int w, int h, McBodyPart part, McFace face,
		double u0 = 0.0, double u1 = 1.0, bool mirrorX = false)
	{
		return new ScRegion(name, new Rect(x, y, w, h), part, face, u0, u1, 0.0, 1.0, mirrorX);
	}

	/// <summary>
	/// 布局说明：人物模型把头、躯干、四肢拆成「正面左右半、背面左右半、左右侧面」共六类岛；
	/// 头与躯干还有顶/底带状岛，手臂在各视角里是窄长条。
	/// </summary>
	/// <summary>宽手（Steve）转译规则；旧入口保留，供预览/比较等与手臂无关的场景使用。</summary>
	public static IReadOnlyList<ScRegion> Regions { get; } = GetRegions(McArmModel.Classic);

	/// <summary>
	/// 按手臂模型取转译规则：宽手（Steve）与细手（Alex）分开维护。
	/// 两套规则共享头/躯干/腿等区块，只有四条手臂条目不同——SC 手臂块固定 4 像素宽，
	/// 细手臂源面只有 3 像素宽，由转换器按像素中心做对称拉伸（3→4 中间补一列）。
	/// </summary>
	public static IReadOnlyList<ScRegion> GetRegions(McArmModel armModel) =>
		armModel == McArmModel.Slim ? s_slimRegions : s_classicRegions;

	private static readonly ScRegion[] s_commonRegions = new ScRegion[]
	{
		// 头部：正面、背面各拆左右半块，左右侧面各一整块，顶/底两条带。
		P("头部正面左", 6, 3, 5, 7, McBodyPart.Head, McFace.Front, 0.0, 0.5),
		P("头部正面右", 11, 3, 5, 7, McBodyPart.Head, McFace.Front, 0.5, 1.0),
		P("头部背面左", 29, 3, 5, 7, McBodyPart.Head, McFace.Back, 0.0, 0.5),
		P("头部背面右", 34, 3, 5, 7, McBodyPart.Head, McFace.Back, 0.5, 1.0),
		P("头部左侧", 45, 1, 8, 10, McBodyPart.Head, McFace.Left),
		P("头部右侧", 54, 1, 8, 10, McBodyPart.Head, McFace.Right),
		P("头部顶面", 6, 0, 10, 3, McBodyPart.Head, McFace.Top),
		P("头部顶面背", 29, 0, 10, 3, McBodyPart.Head, McFace.Top),
		P("头部底面", 6, 10, 10, 3, McBodyPart.Head, McFace.Bottom),
		P("头部底面背", 29, 10, 10, 3, McBodyPart.Head, McFace.Bottom),

		// 躯干：正面/背面左右半块 + 左右侧面。
		P("躯干正面左", 4, 14, 7, 18, McBodyPart.Body, McFace.Front, 0.0, 0.5),
		P("躯干正面右", 11, 14, 7, 18, McBodyPart.Body, McFace.Front, 0.5, 1.0),
		P("躯干背面左", 27, 14, 7, 18, McBodyPart.Body, McFace.Back, 0.0, 0.5),
		P("躯干背面右", 34, 14, 7, 18, McBodyPart.Body, McFace.Back, 0.5, 1.0),
		P("躯干左侧", 46, 14, 7, 18, McBodyPart.Body, McFace.Left),
		P("躯干右侧", 54, 14, 7, 18, McBodyPart.Body, McFace.Right),

		// 肩部（顶面）与腰部（底面）。
		P("肩正面左", 4, 10, 7, 4, McBodyPart.Body, McFace.Top, 0.0, 0.5),
		P("肩正面右", 11, 10, 7, 4, McBodyPart.Body, McFace.Top, 0.5, 1.0),
		P("肩背面左", 27, 10, 7, 4, McBodyPart.Body, McFace.Top, 0.0, 0.5),
		P("肩背面右", 34, 10, 7, 4, McBodyPart.Body, McFace.Top, 0.5, 1.0),
		P("肩左侧", 46, 10, 7, 4, McBodyPart.Body, McFace.Top),
		P("肩右侧", 54, 10, 7, 4, McBodyPart.Body, McFace.Top),
		P("腰正面左", 4, 32, 7, 6, McBodyPart.Body, McFace.Bottom, 0.0, 0.5),
		P("腰正面右", 11, 32, 7, 6, McBodyPart.Body, McFace.Bottom, 0.5, 1.0),
		P("腰背面左", 27, 32, 7, 6, McBodyPart.Body, McFace.Bottom, 0.0, 0.5),
		P("腰背面右", 34, 32, 7, 6, McBodyPart.Body, McFace.Bottom, 0.5, 1.0),
		P("腰左侧", 46, 32, 7, 6, McBodyPart.Body, McFace.Bottom),
		P("腰右侧", 54, 32, 7, 6, McBodyPart.Body, McFace.Bottom),

		// 双腿：正面/背面/侧面各自成条，上下端与裤腰共用同一条带做近似。
		P("右腿正面", 5, 38, 6, 26, McBodyPart.RightLeg, McFace.Front),
		P("左腿正面", 11, 38, 6, 26, McBodyPart.LeftLeg, McFace.Front),
		P("左腿背面", 28, 38, 6, 26, McBodyPart.LeftLeg, McFace.Back),
		P("右腿背面", 34, 38, 6, 26, McBodyPart.RightLeg, McFace.Back),
		P("左腿侧面", 46, 38, 7, 26, McBodyPart.LeftLeg, McFace.Left),
		P("右腿侧面", 54, 38, 7, 26, McBodyPart.RightLeg, McFace.Right)
	};

	// 宽手（Steve）：手臂盒 4×4×12，各面宽度与 SC 手臂块一一对应，逐像素直拷。
	private static readonly ScRegion[] s_classicArmRegions = new ScRegion[]
	{
		P("左臂正面", 0, 15, 4, 23, McBodyPart.LeftArm, McFace.Front),
		P("右臂正面", 18, 15, 4, 23, McBodyPart.RightArm, McFace.Front),
		P("手臂中央长条", 21, 15, 5, 49, McBodyPart.RightArm, McFace.Back),
		P("左臂背面", 41, 15, 4, 23, McBodyPart.LeftArm, McFace.Back)
	};

	// 细手（Alex）：手臂盒 3×3×12，源面窄一列；SC 手臂块宽度不变，
	// 采样时按像素中心对称拉伸（3→4 中间补一列、3→5 两侧各补半列），避免图案偏向一侧。
	private static readonly ScRegion[] s_slimArmRegions = new ScRegion[]
	{
		P("细手左臂正面", 0, 15, 4, 23, McBodyPart.LeftArm, McFace.Front),
		P("细手右臂正面", 18, 15, 4, 23, McBodyPart.RightArm, McFace.Front),
		P("细手手臂中央长条", 21, 15, 5, 49, McBodyPart.RightArm, McFace.Back),
		P("细手左臂背面", 41, 15, 4, 23, McBodyPart.LeftArm, McFace.Back)
	};

	private static readonly ScRegion[] s_classicRegions = s_commonRegions.Concat(s_classicArmRegions).ToArray();
	private static readonly ScRegion[] s_slimRegions = s_commonRegions.Concat(s_slimArmRegions).ToArray();
}
