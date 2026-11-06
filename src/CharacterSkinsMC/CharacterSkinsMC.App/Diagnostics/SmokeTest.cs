#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CharacterSkinsMC.App.Infrastructure;
using CharacterSkinsMC.Core;

namespace CharacterSkinsMC.App.Diagnostics;

/// <summary>
/// 无界面自检：按「转换管线 / 分辨率 / 规则表 / UV 覆盖 / 内置资产 / 预览」分组校验。
/// 与界面入口完全解耦，可单独调用。
/// </summary>
public static class SmokeTest
{
	private readonly record struct PreviewValidation(
		RgbaImage ModelPreview,
		RgbaImage ScModelPreview,
		int MaleTriangles,
		int FemaleTriangles);

	public static void Run()
	{
		string directory = Path.Combine(Path.GetTempPath(), "CharacterSkinsMC-smoke");
		Directory.CreateDirectory(directory);
		try
		{
			string skinPath = Path.Combine(directory, "skin.png");
			ImageFiles.WritePng(skinPath, CreateGradientSource());
			MinecraftSkin skin = MinecraftSkin.Load(ImageFiles.ReadBytes(skinPath), McArmModel.Classic);
			RgbaImage atlas = MinecraftSkinConverter.Convert(skin, composeOverlay: true);
			string scskinPath = Path.Combine(directory, "skin.scskin");
			ImageFiles.WriteScSkin(scskinPath, atlas);
			RgbaImage reloaded = ImageFiles.ReadImage(scskinPath);

			ValidateResolutions();
			ValidateArmRegionTables();
			ValidateModelUvCoverage();
			ValidateSymmetricFace();
			ValidateUvLayouts(skin, atlas);
			int defaultSkinCount = ValidateBuiltinAssets();
			PreviewValidation previews = ValidatePreviewRendering(skin, atlas);
			ScAtlasDiff comparison = ValidateComparison(atlas);
			int opaquePixels = CountOpaquePixels(atlas);

			int expectedSize = SkinDimensions.AtlasSize;
			if (atlas.Width != expectedSize || atlas.Height != expectedSize ||
				reloaded.Width != expectedSize || reloaded.Height != expectedSize || opaquePixels == 0)
			{
				throw new InvalidOperationException("转译产物尺寸或内容不符合预期。");
			}
			Console.WriteLine($"自检通过：图集 {atlas.Width}×{atlas.Height}，非透明像素 {opaquePixels}，内置皮肤 {defaultSkinCount} 张，人物三角形 男={previews.MaleTriangles} 女={previews.FemaleTriangles}，MC 3D {previews.ModelPreview.Width}×{previews.ModelPreview.Height}，SC 3D {previews.ScModelPreview.Width}×{previews.ScModelPreview.Height}，图集差异 {comparison.DifferentPixels}/{comparison.TotalPixels}，产物 {scskinPath}");
		}
		finally
		{
			Directory.Delete(directory, recursive: true);
		}
	}

	/// <summary>六个盒面都有不透明像素，第二层用半透明红色，便于验证合成路径。</summary>
	private static RgbaImage CreateGradientSource()
	{
		int size = SkinDimensions.AtlasSize;
		var source = new RgbaImage(size, size);
		for (int y = 0; y < size; y++)
		{
			for (int x = 0; x < size; x++)
			{
				source.SetPixel(x, y, y < size / 2
					? new RgbaColor((byte)(x * 4), (byte)(y * 8), 160, 255)
					: new RgbaColor(255, 0, 0, 128));
			}
		}
		return source;
	}

	/// <summary>覆盖旧版镜像补齐、各分辨率降采样（2/4/8 倍）与宽手/细手两条规则分支。</summary>
	private static void ValidateResolutions()
	{
		foreach ((int width, int height, McArmModel arm, int expectedScale) in new (int, int, McArmModel, int)[]
		{
			(64, 32, McArmModel.Classic, 1),
			(64, 64, McArmModel.Slim, 1),
			(128, 128, McArmModel.Classic, 2),
			(256, 256, McArmModel.Slim, 4),
			(512, 512, McArmModel.Classic, 8)
		})
		{
			var variant = new RgbaImage(width, height);
			for (int y = 0; y < height; y++)
			{
				for (int x = 0; x < width; x++)
				{
					variant.SetPixel(x, y, new RgbaColor(120, 160, 200, 255));
				}
			}
			MinecraftSkin variantSkin = MinecraftSkin.Load(variant.ToPngBytes(), arm);
			if (variantSkin.SourceScale != expectedScale)
			{
				throw new InvalidOperationException($"{width}×{height} 皮肤分辨率倍数应为 {expectedScale}。");
			}
			if (width == 64 && height == 32 && variantSkin.ArmModel != McArmModel.Classic)
			{
				throw new InvalidOperationException("64×32 旧版皮肤没有细手模型，必须固定为宽手。");
			}
			RgbaImage variantAtlas = MinecraftSkinConverter.Convert(variantSkin, composeOverlay: true);
			if (variantAtlas.Width != SkinDimensions.AtlasSize || variantAtlas.Height != SkinDimensions.AtlasSize)
			{
				throw new InvalidOperationException($"{width}×{height} 归一化后图集尺寸异常。");
			}
		}
	}

	/// <summary>宽手 / 细手必须是两套独立的规则表：条目数一致，但手臂条目名称不同。</summary>
	private static void ValidateArmRegionTables()
	{
		IReadOnlyList<ScRegion> classicRegions = ScSkinAtlas.GetRegions(McArmModel.Classic);
		IReadOnlyList<ScRegion> slimRegions = ScSkinAtlas.GetRegions(McArmModel.Slim);
		if (classicRegions.Count != slimRegions.Count ||
			classicRegions.Zip(slimRegions, (a, b) => a.Name == b.Name).All(same => same))
		{
			throw new InvalidOperationException("宽手与细手的转译规则表必须分开维护，手臂条目应有区分。");
		}
	}

	/// <summary>转译落点必须盖住人物模型采样的全部 UV 像素，否则游戏里会渲染成黑块。</summary>
	private static void ValidateModelUvCoverage()
	{
		foreach (ScPlayerClass playerClass in new[] { ScPlayerClass.Male, ScPlayerClass.Female })
		{
			ScModel model = ScDefaultSkins.LoadModel(playerClass);
			var coveredPixels = new HashSet<(int X, int Y)>();
			foreach (ScRegion region in ScSkinAtlas.GetRegions(McArmModel.Classic))
			{
				for (int y = 0; y < region.Dest.H; y++)
				{
					for (int x = 0; x < region.Dest.W; x++)
					{
						coveredPixels.Add((region.Dest.X + x, region.Dest.Y + y));
					}
				}
			}
			foreach (ScTriangle triangle in model.Triangles)
			{
				(double X, double Y) a = (triangle.UvA.U * SkinDimensions.AtlasSize, (1.0 - triangle.UvA.V) * SkinDimensions.AtlasSize);
				(double X, double Y) b = (triangle.UvB.U * SkinDimensions.AtlasSize, (1.0 - triangle.UvB.V) * SkinDimensions.AtlasSize);
				(double X, double Y) c = (triangle.UvC.U * SkinDimensions.AtlasSize, (1.0 - triangle.UvC.V) * SkinDimensions.AtlasSize);
				int minX = Math.Max(0, (int)Math.Floor(Math.Min(a.X, Math.Min(b.X, c.X))));
				int maxX = Math.Min(SkinDimensions.AtlasSize - 1, (int)Math.Ceiling(Math.Max(a.X, Math.Max(b.X, c.X))));
				int minY = Math.Max(0, (int)Math.Floor(Math.Min(a.Y, Math.Min(b.Y, c.Y))));
				int maxY = Math.Min(SkinDimensions.AtlasSize - 1, (int)Math.Ceiling(Math.Max(a.Y, Math.Max(b.Y, c.Y))));
				for (int y = minY; y <= maxY; y++)
				{
					for (int x = minX; x <= maxX; x++)
					{
						double px = x + 0.5;
						double py = y + 0.5;
						double d1 = (b.X - a.X) * (py - a.Y) - (b.Y - a.Y) * (px - a.X);
						double d2 = (c.X - b.X) * (py - b.Y) - (c.Y - b.Y) * (px - b.X);
						double d3 = (a.X - c.X) * (py - c.Y) - (a.Y - c.Y) * (px - c.X);
						bool inside = (d1 >= 0 && d2 >= 0 && d3 >= 0) || (d1 <= 0 && d2 <= 0 && d3 <= 0);
						if (inside && !coveredPixels.Contains((x, y)))
						{
							throw new InvalidOperationException(
								$"{playerClass} 模型 UV ({x},{y}) 没有任何转译规则覆盖，游戏里会渲染成黑块。");
						}
					}
				}
			}
		}
	}

	/// <summary>对称脸回归检查：源脸左右镜像对称时，头正面两眼宽度必须一致。</summary>
	private static void ValidateSymmetricFace()
	{
		int size = SkinDimensions.AtlasSize;
		var symmetricSource = new RgbaImage(size, size);
		var faceColor = new RgbaColor(200, 170, 130, 255);
		var eyeColor = new RgbaColor(255, 255, 255, 255);
		for (int y = 0; y < 8; y++)
		{
			for (int x = 0; x < 8; x++)
			{
				symmetricSource.SetPixel(8 + x, 8 + y, x == 2 || x == 5 ? eyeColor : faceColor);
			}
		}
		MinecraftSkin skin = MinecraftSkin.Load(symmetricSource.ToPngBytes(), McArmModel.Classic);
		RgbaImage atlas = MinecraftSkinConverter.Convert(skin, composeOverlay: false);
		for (int y = 3; y < 10; y++)
		{
			int leftEyeWidth = 0;
			int rightEyeWidth = 0;
			for (int x = 6; x < 16; x++)
			{
				RgbaColor pixel = atlas.GetPixel(x, y);
				if (pixel != atlas.GetPixel(21 - x, y))
				{
					throw new InvalidOperationException($"头正面第 {y} 行左右不对称，眼睛会被拉成一大一小。");
				}
				if (pixel != eyeColor)
				{
					continue;
				}
				if (x < 11)
				{
					leftEyeWidth++;
				}
				else
				{
					rightEyeWidth++;
				}
			}
			if (leftEyeWidth == 0 || leftEyeWidth != rightEyeWidth)
			{
				throw new InvalidOperationException($"头正面第 {y} 行两眼宽度不一致：左 {leftEyeWidth} / 右 {rightEyeWidth}。");
			}
		}
	}

	/// <summary>自检两套分区配置：MC 表来自皮肤布局，SC 男/女表各自维护且互不串用。</summary>
	private static void ValidateUvLayouts(MinecraftSkin skin, RgbaImage atlas)
	{
		McUvLayout mcLayout = McUvLayouts.Create(skin);
		if (mcLayout.Regions.Count != 72)
		{
			throw new InvalidOperationException("MC 分区表应包含 6 部位 × 6 面 × 基础层/第二层共 72 个区块。");
		}
		ValidateScLayout(ScUvLayouts.Male, ScPlayerClass.Male, 24, 40);
		ValidateScLayout(ScUvLayouts.Female, ScPlayerClass.Female, 20, 36);

		// 同一张图集用两套 SC 表绘制，参考线必须不同；关掉参考线后两表必须给出完全相同的图像。
		RgbaImage maleGuides = SkinPreview.RenderAtlas(atlas, 2, true, ScUvLayouts.Male);
		RgbaImage femaleGuides = SkinPreview.RenderAtlas(atlas, 2, true, ScUvLayouts.Female);
		RgbaImage malePlain = SkinPreview.RenderAtlas(atlas, 2, false, ScUvLayouts.Male);
		RgbaImage femalePlain = SkinPreview.RenderAtlas(atlas, 2, false, ScUvLayouts.Female);
		if (maleGuides.ContentEquals(femaleGuides) || !malePlain.ContentEquals(femalePlain))
		{
			throw new InvalidOperationException("SC 男/女分区表的参考线未生效，或分区表改动了图像像素。");
		}
	}

	private static void ValidateScLayout(ScUvLayout layout, ScPlayerClass playerClass, int torsoHeight, int legY)
	{
		if (layout.PlayerClass != playerClass || layout.Regions.Count != 14)
		{
			throw new InvalidOperationException($"SC {playerClass} 分区表的性别或条目数不符。");
		}
		foreach (ScUvRegion region in layout.Regions)
		{
			Rect rect = region.Rect;
			if (rect.X < 0 || rect.Y < 0 || rect.W <= 0 || rect.H <= 0 ||
				rect.X + rect.W > SkinDimensions.AtlasSize || rect.Y + rect.H > SkinDimensions.AtlasSize)
			{
				throw new InvalidOperationException($"SC 分区表 {layout.Name} 的 {region.Name} 超出图集。");
			}
		}
		Rect? torso = layout.Find("躯干前");
		Rect? leg = layout.Find("右腿前");
		if (torso == null || torso.Value.H != torsoHeight || leg == null || leg.Value.Y != legY)
		{
			throw new InvalidOperationException($"SC 分区表 {layout.Name} 的躯干高度或腿部起点不符。");
		}
	}

	/// <summary>8 张内置默认皮肤与 2 张内置 Minecraft 示例皮肤必须全部可用。</summary>
	private static int ValidateBuiltinAssets()
	{
		int count = 0;
		foreach (ScDefaultSkin defaultSkin in ScDefaultSkins.All)
		{
			RgbaImage defaultAtlas = ScDefaultSkins.LoadSkin(defaultSkin);
			if (defaultAtlas.Width != SkinDimensions.AtlasSize || defaultAtlas.Height != SkinDimensions.AtlasSize)
			{
				throw new InvalidOperationException($"内置皮肤 {defaultSkin.Id} 尺寸异常。");
			}
			if (CountOpaquePixels(defaultAtlas) == 0)
			{
				throw new InvalidOperationException($"内置皮肤 {defaultSkin.Id} 全透明。");
			}
			count++;
		}
		foreach (McBuiltinSkin builtin in McBuiltinSkins.All)
		{
			MinecraftSkin skin = McBuiltinSkins.Load(builtin, McArmModel.Classic);
			RgbaImage atlas = MinecraftSkinConverter.Convert(skin, composeOverlay: true);
			if (atlas.Width != SkinDimensions.AtlasSize || atlas.Height != SkinDimensions.AtlasSize)
			{
				throw new InvalidOperationException($"内置皮肤 {builtin.Id} 转译产物尺寸异常。");
			}
		}
		return count;
	}

	private static PreviewValidation ValidatePreviewRendering(MinecraftSkin skin, RgbaImage atlas)
	{
		_ = SkinPreview.RenderMcSkin(skin, 4, guides: true);
		_ = SkinPreview.RenderAtlas(atlas, 4, guides: true, ScUvLayouts.Male);
		RgbaImage modelPreview = SkinPreview.Render3D(skin, composeOverlay: true, 300, 400, 35, 18);
		int maleTriangles = ScDefaultSkins.LoadModel(ScPlayerClass.Male).Triangles.Count;
		int femaleTriangles = ScDefaultSkins.LoadModel(ScPlayerClass.Female).Triangles.Count;
		RgbaImage scMalePreview = ScModelPreview.Render(atlas, ScPlayerClass.Male, 300, 400, 35, 18);
		RgbaImage scFemalePreview = ScModelPreview.Render(
			ScDefaultSkins.LoadSkin(ScDefaultSkins.All[4]), ScPlayerClass.Female, 300, 400, 35, 18);
		if (maleTriangles == 0 || femaleTriangles == 0 || scMalePreview.Width != 300 || scFemalePreview.Height != 400)
		{
			throw new InvalidOperationException("Survivalcraft 人物模型或 SC 3D 预览异常。");
		}
		return new PreviewValidation(modelPreview, scMalePreview, maleTriangles, femaleTriangles);
	}

	/// <summary>图集对比：自身对比零差异，与内置默认皮肤对比可统计并渲染差异图。</summary>
	private static ScAtlasDiff ValidateComparison(RgbaImage atlas)
	{
		RgbaImage reference = ScDefaultSkins.LoadSkin(ScDefaultSkins.All[0]);
		ScAtlasDiff selfComparison = ScAtlasComparison.Compare(atlas, atlas);
		ScAtlasDiff defaultComparison = ScAtlasComparison.Compare(atlas, reference);
		RgbaImage differencePreview = ScAtlasComparison.RenderDifference(atlas, reference, 2, true, ScUvLayouts.Male);
		int total = SkinDimensions.AtlasSize * SkinDimensions.AtlasSize;
		if (selfComparison.DifferentPixels != 0 || selfComparison.TotalPixels != total ||
			defaultComparison.TotalPixels != total || defaultComparison.DifferentPixels < 0 ||
			defaultComparison.DifferentPixels > total || differencePreview.Width != 2 * SkinDimensions.AtlasSize)
		{
			throw new InvalidOperationException("图集对比统计或差异图渲染异常。");
		}
		return defaultComparison;
	}

	private static int CountOpaquePixels(RgbaImage image)
	{
		int count = 0;
		for (int y = 0; y < image.Height; y++)
		{
			for (int x = 0; x < image.Width; x++)
			{
				if (image.GetPixel(x, y).A > 0)
				{
					count++;
				}
			}
		}
		return count;
	}
}
