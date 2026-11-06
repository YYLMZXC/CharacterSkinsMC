#nullable enable
using System;
using System.IO;
using Avalonia;
using CharacterSkinsMC.Core;
using Rect = CharacterSkinsMC.Core.Rect;

namespace CharacterSkinsMC.App;

// 桌面入口；csproj 已把本文件从 android TFM 编译列表移除，Android 入口在 Platforms/Android/MainApplication。
internal static class Program
{
	[STAThread]
	public static void Main(string[] args)
	{
		if (args.Length == 1 && args[0] == "--smoke")
		{
			RunSmokeTest();
			return;
		}
		if (args.Length == 2 && args[0] == "--dump")
		{
			DumpPreviews(args[1]);
			return;
		}
		BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
	}

	public static AppBuilder BuildAvaloniaApp()
	{
		return AppBuilder.Configure<App>()
			.UsePlatformDetect()
			.LogToTrace();
	}

	/// <summary>导出一份样例皮肤与三种预览图，用于人工核对 UV 与 3D 投影效果。</summary>
	private static void DumpPreviews(string directory)
	{
		Directory.CreateDirectory(directory);
		string skinPath = Path.Combine(directory, "sample-skin.png");
		BuildSampleSkin(skinPath);
		MinecraftSkin skin = MinecraftSkin.Load(skinPath, McArmModel.Classic);
		RgbaImage atlas = MinecraftSkinConverter.Convert(skin, composeOverlay: true);
		MinecraftSkinConverter.SaveScSkin(atlas, Path.Combine(directory, "sample-skin.scskin"));
		var mcPreview = SkinPreview.RenderMcSkin(skin, 8, guides: true);
		var scPreview = SkinPreview.RenderAtlas(atlas, 8, guides: true);
		var modelPreview = SkinPreview.Render3D(skin, composeOverlay: true, 480, 560, 35, 18);
		mcPreview.SavePng(Path.Combine(directory, "preview-mc.png"));
		scPreview.SavePng(Path.Combine(directory, "preview-sc.png"));
		modelPreview.SavePng(Path.Combine(directory, "preview-mc3d.png"));

		ScDefaultSkin maleSkin = ScDefaultSkins.GetByClass(ScPlayerClass.Male)[0];
		ScDefaultSkin femaleSkin = ScDefaultSkins.GetByClass(ScPlayerClass.Female)[0];
		RgbaImage maleAtlas = ScDefaultSkins.LoadSkin(maleSkin);
		RgbaImage femaleAtlas = ScDefaultSkins.LoadSkin(femaleSkin);
		maleAtlas.SavePng(Path.Combine(directory, $"default-{maleSkin.DisplayName}.png"));
		femaleAtlas.SavePng(Path.Combine(directory, $"default-{femaleSkin.DisplayName}.png"));
		// 男模型贴转译结果、女模型贴内置默认皮肤，两条通道都能人工核对。
		var scMale = ScModelPreview.Render(atlas, ScDefaultSkins.LoadModel(ScPlayerClass.Male), 480, 560, 35, 18);
		var scFemale = ScModelPreview.Render(femaleAtlas, ScDefaultSkins.LoadModel(ScPlayerClass.Female), 480, 560, 35, 18);
		scMale.SavePng(Path.Combine(directory, "preview-sc3d-male.png"));
		scFemale.SavePng(Path.Combine(directory, "preview-sc3d-female.png"));
		var scMaleDefault = ScModelPreview.Render(maleAtlas, ScDefaultSkins.LoadModel(ScPlayerClass.Male), 480, 560, 0, 18);
		scMaleDefault.SavePng(Path.Combine(directory, "preview-sc3d-male-default.png"));
		ScAtlasDiff comparison = ScAtlasComparison.Compare(atlas, maleAtlas);
		ScAtlasComparison.RenderDifference(atlas, maleAtlas, 6, true)
			.SavePng(Path.Combine(directory, "preview-atlas-diff.png"));
		Console.WriteLine($"预览已导出到：{directory}；与 {maleSkin.Caption} 差异 {comparison.DifferentPixels}/{comparison.TotalPixels} 像素");
	}

	private static void BuildSampleSkin(string path)
	{
		var image = new RgbaImage(64, 64);
		var skin = new RgbaColor(224, 178, 132, 255);
		var hair = new RgbaColor(72, 48, 32, 255);
		var shirt = new RgbaColor(40, 120, 200, 255);
		var trousers = new RgbaColor(48, 60, 140, 255);
		var shoe = new RgbaColor(60, 44, 36, 255);
		// 头部六面
		Fill(image, new Rect(8, 8, 8, 8), skin);
		Fill(image, new Rect(0, 8, 8, 8), skin);
		Fill(image, new Rect(16, 8, 8, 8), skin);
		Fill(image, new Rect(24, 8, 8, 8), hair);
		Fill(image, new Rect(8, 0, 8, 8), hair);
		Fill(image, new Rect(16, 0, 8, 8), hair);
		image.SetPixel(10, 10, new RgbaColor(255, 255, 255, 255));
		image.SetPixel(13, 10, new RgbaColor(255, 255, 255, 255));
		image.SetPixel(10, 11, new RgbaColor(30, 40, 90, 255));
		image.SetPixel(13, 11, new RgbaColor(30, 40, 90, 255));
		// 躯干、双臂、双腿
		Fill(image, new Rect(20, 20, 8, 12), shirt);
		Fill(image, new Rect(32, 20, 8, 12), shirt);
		Fill(image, new Rect(20, 16, 8, 4), shirt);
		Fill(image, new Rect(44, 20, 4, 12), skin);
		Fill(image, new Rect(52, 20, 4, 12), skin);
		Fill(image, new Rect(36, 52, 4, 12), skin);
		Fill(image, new Rect(4, 20, 4, 12), trousers);
		Fill(image, new Rect(20, 52, 4, 12), trousers);
		Fill(image, new Rect(4, 28, 4, 4), shoe);
		Fill(image, new Rect(20, 60, 4, 4), shoe);
		// 第二层：帽子与外套
		Fill(image, new Rect(40, 8, 8, 8), new RgbaColor(220, 60, 60, 190));
		Fill(image, new Rect(20, 36, 8, 12), new RgbaColor(255, 210, 80, 160));
		image.SavePng(path);
	}

	private static void Fill(RgbaImage image, Rect rect, RgbaColor color)
	{
		for (int y = 0; y < rect.H; y++)
		{
			for (int x = 0; x < rect.W; x++)
			{
				image.SetPixel(rect.X + x, rect.Y + y, color);
			}
		}
	}

	/// <summary>无界面自检：验证归一化、双层合成、图集转译、落盘与三种预览渲染。</summary>
	private static void RunSmokeTest()
	{
		string directory = Path.Combine(Path.GetTempPath(), "CharacterSkinsMC-smoke");
		Directory.CreateDirectory(directory);
		try
		{
			var source = new RgbaImage(64, 64);
			for (int y = 0; y < 64; y++)
			{
				for (int x = 0; x < 64; x++)
				{
					// 让六个盒面都有不透明像素，第二层用半透明红色，便于验证合成路径。
					source.SetPixel(x, y, y < 32
						? new RgbaColor((byte)(x * 4), (byte)(y * 8), 160, 255)
						: new RgbaColor(255, 0, 0, 128));
				}
			}
			string skinPath = Path.Combine(directory, "skin.png");
			source.SavePng(skinPath);
			MinecraftSkin skin = MinecraftSkin.Load(skinPath, McArmModel.Classic);
			RgbaImage atlas = MinecraftSkinConverter.Convert(skin, composeOverlay: true);
			string scskinPath = MinecraftSkinConverter.SaveScSkin(atlas, Path.Combine(directory, "skin.scskin"));
			RgbaImage reloaded = RgbaImage.FromFile(scskinPath);

			// 覆盖旧版镜像补齐、高清降采样与 Slim 三条归一化分支。
			foreach ((int width, int height, McArmModel arm) in new (int, int, McArmModel)[]
			{
				(64, 32, McArmModel.Classic),
				(64, 64, McArmModel.Slim),
				(128, 128, McArmModel.Classic)
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
				string variantPath = Path.Combine(directory, $"skin-{width}x{height}.png");
				variant.SavePng(variantPath);
				MinecraftSkin variantSkin = MinecraftSkin.Load(variantPath, arm);
				RgbaImage variantAtlas = MinecraftSkinConverter.Convert(variantSkin, composeOverlay: true);
				if (variantAtlas.Width != 64 || variantAtlas.Height != 64)
				{
					throw new InvalidOperationException($"{width}×{height} 归一化后图集尺寸异常。");
				}
			}

			int opaquePixels = 0;
			for (int y = 0; y < atlas.Height; y++)
			{
				for (int x = 0; x < atlas.Width; x++)
				{
					if (atlas.GetPixel(x, y).A > 0)
					{
						opaquePixels++;
					}
				}
			}

			var mcPreview = SkinPreview.RenderMcSkin(skin, 4, guides: true);
			var scPreview = SkinPreview.RenderAtlas(atlas, 4, guides: true);
			var modelPreview = SkinPreview.Render3D(skin, composeOverlay: true, 300, 400, 35, 18);

			// 8 张内置默认皮肤与男女模型必须全部可用，且 SC 专属 3D 预览可渲染。
			int defaultSkinCount = 0;
			foreach (ScDefaultSkin defaultSkin in ScDefaultSkins.All)
			{
				RgbaImage defaultAtlas = ScDefaultSkins.LoadSkin(defaultSkin);
				if (defaultAtlas.Width != 64 || defaultAtlas.Height != 64)
				{
					throw new InvalidOperationException($"内置皮肤 {defaultSkin.Id} 尺寸异常。");
				}
				int defaultOpaque = 0;
				for (int y = 0; y < defaultAtlas.Height; y++)
				{
					for (int x = 0; x < defaultAtlas.Width; x++)
					{
						if (defaultAtlas.GetPixel(x, y).A > 0)
						{
							defaultOpaque++;
						}
					}
				}
				if (defaultOpaque == 0)
				{
					throw new InvalidOperationException($"内置皮肤 {defaultSkin.Id} 全透明。");
				}
				defaultSkinCount++;
			}
			int maleTriangles = ScDefaultSkins.LoadModel(ScPlayerClass.Male).Triangles.Count;
			int femaleTriangles = ScDefaultSkins.LoadModel(ScPlayerClass.Female).Triangles.Count;
			var scMalePreview = ScModelPreview.Render(atlas, ScPlayerClass.Male, 300, 400, 35, 18);
			var scFemalePreview = ScModelPreview.Render(ScDefaultSkins.LoadSkin(ScDefaultSkins.All[4]),
				ScPlayerClass.Female, 300, 400, 35, 18);
			if (maleTriangles == 0 || femaleTriangles == 0 || scMalePreview.Width != 300 || scFemalePreview.Height != 400)
			{
				throw new InvalidOperationException("Survivalcraft 人物模型或 SC 3D 预览异常。");
			}

			// 图集对比：自身对比必须零差异，与内置默认皮肤对比必须能统计并渲染差异图。
			RgbaImage referenceForDiff = ScDefaultSkins.LoadSkin(ScDefaultSkins.All[0]);
			ScAtlasDiff selfComparison = ScAtlasComparison.Compare(atlas, atlas);
			ScAtlasDiff defaultComparison = ScAtlasComparison.Compare(atlas, referenceForDiff);
			RgbaImage differencePreview = ScAtlasComparison.RenderDifference(atlas, referenceForDiff, 2, true);
			if (selfComparison.DifferentPixels != 0 || selfComparison.TotalPixels != 4096 ||
				defaultComparison.TotalPixels != 4096 || defaultComparison.DifferentPixels < 0 ||
				defaultComparison.DifferentPixels > 4096 || differencePreview.Width != 128)
			{
				throw new InvalidOperationException("图集对比统计或差异图渲染异常。");
			}

			if (atlas.Width != 64 || atlas.Height != 64 || reloaded.Width != 64 || reloaded.Height != 64 || opaquePixels == 0)
			{
				throw new InvalidOperationException("转译产物尺寸或内容不符合预期。");
			}
			Console.WriteLine($"自检通过：图集 {atlas.Width}×{atlas.Height}，非透明像素 {opaquePixels}，内置皮肤 {defaultSkinCount} 张，人物三角形 男={maleTriangles} 女={femaleTriangles}，MC 3D {modelPreview.Width}×{modelPreview.Height}，SC 3D {scMalePreview.Width}×{scMalePreview.Height}，图集差异 {defaultComparison.DifferentPixels}/{defaultComparison.TotalPixels}，产物 {scskinPath}");
		}
		finally
		{
			Directory.Delete(directory, recursive: true);
		}
	}
}
