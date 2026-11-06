#nullable enable
using CharacterSkinsMC.Core;
using Rect = CharacterSkinsMC.Core.Rect;

namespace CharacterSkinsMC.App.Diagnostics;

/// <summary>构造一张用于人工核对与自检的样例皮肤，只产出内存像素图。</summary>
public static class SampleSkinFactory
{
	public static RgbaImage Create()
	{
		var image = new RgbaImage(SkinDimensions.AtlasSize, SkinDimensions.AtlasSize);
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
		return image;
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
}
