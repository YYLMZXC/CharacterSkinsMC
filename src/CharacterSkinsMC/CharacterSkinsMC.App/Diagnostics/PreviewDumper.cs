#nullable enable
using System;
using System.IO;
using CharacterSkinsMC.App.Infrastructure;
using CharacterSkinsMC.Core;

namespace CharacterSkinsMC.App.Diagnostics;

/// <summary>导出样例皮肤与各类预览图，供人工核对 UV 与 3D 投影效果。</summary>
public static class PreviewDumper
{
	private const int PreviewScale = 8;
	private const int PreviewWidth = 480;
	private const int PreviewHeight = 560;

	public static void Dump(string directory)
	{
		Directory.CreateDirectory(directory);
		string skinPath = Path.Combine(directory, "sample-skin.png");
		ImageFiles.WritePng(skinPath, SampleSkinFactory.Create());
		MinecraftSkin skin = MinecraftSkin.Load(ImageFiles.ReadBytes(skinPath), McArmModel.Classic);
		RgbaImage atlas = MinecraftSkinConverter.Convert(skin, composeOverlay: true);
		ImageFiles.WriteScSkin(Path.Combine(directory, "sample-skin.scskin"), atlas);

		ImageFiles.WritePng(Path.Combine(directory, "preview-mc.png"),
			SkinPreview.RenderMcSkin(skin, PreviewScale, guides: true));
		ImageFiles.WritePng(Path.Combine(directory, "preview-sc.png"),
			SkinPreview.RenderAtlas(atlas, PreviewScale, guides: true, layout: ScUvLayouts.Male));
		ImageFiles.WritePng(Path.Combine(directory, "preview-mc3d.png"),
			SkinPreview.Render3D(skin, composeOverlay: true, PreviewWidth, PreviewHeight, 35, 18));

		ScDefaultSkin maleSkin = ScDefaultSkins.GetByClass(ScPlayerClass.Male)[0];
		ScDefaultSkin femaleSkin = ScDefaultSkins.GetByClass(ScPlayerClass.Female)[0];
		RgbaImage maleAtlas = ScDefaultSkins.LoadSkin(maleSkin);
		RgbaImage femaleAtlas = ScDefaultSkins.LoadSkin(femaleSkin);
		ImageFiles.WritePng(Path.Combine(directory, $"default-{maleSkin.DisplayName}.png"), maleAtlas);
		ImageFiles.WritePng(Path.Combine(directory, $"default-{femaleSkin.DisplayName}.png"), femaleAtlas);
		// 男/女原版图集各套自己的 SC 分区参考线，供人工核对矩形是否对齐。
		ImageFiles.WritePng(Path.Combine(directory, "preview-sc-uv-male.png"),
			SkinPreview.RenderAtlas(maleAtlas, PreviewScale, guides: true, layout: ScUvLayouts.Male));
		ImageFiles.WritePng(Path.Combine(directory, "preview-sc-uv-female.png"),
			SkinPreview.RenderAtlas(femaleAtlas, PreviewScale, guides: true, layout: ScUvLayouts.Female));
		// 男模型贴转译结果、女模型贴内置默认皮肤，两条通道都能人工核对。
		ImageFiles.WritePng(Path.Combine(directory, "preview-sc3d-male.png"),
			ScModelPreview.Render(atlas, ScDefaultSkins.LoadModel(ScPlayerClass.Male), PreviewWidth, PreviewHeight, 35, 18));
		ImageFiles.WritePng(Path.Combine(directory, "preview-sc3d-female.png"),
			ScModelPreview.Render(femaleAtlas, ScDefaultSkins.LoadModel(ScPlayerClass.Female), PreviewWidth, PreviewHeight, 35, 18));
		ImageFiles.WritePng(Path.Combine(directory, "preview-sc3d-male-default.png"),
			ScModelPreview.Render(maleAtlas, ScDefaultSkins.LoadModel(ScPlayerClass.Male), PreviewWidth, PreviewHeight, 0, 18));

		ScAtlasDiff comparison = ScAtlasComparison.Compare(atlas, maleAtlas);
		ImageFiles.WritePng(Path.Combine(directory, "preview-atlas-diff.png"),
			ScAtlasComparison.RenderDifference(atlas, maleAtlas, 6, true, ScUvLayouts.Male));
		Console.WriteLine($"预览已导出到：{directory}；与 {maleSkin.Caption} 差异 {comparison.DifferentPixels}/{comparison.TotalPixels} 像素；UV 分区 MC={McUvLayouts.Create(skin).Regions.Count} 区 / SC 男={ScUvLayouts.Male.Regions.Count} 区 / SC 女={ScUvLayouts.Female.Regions.Count} 区");
	}
}
