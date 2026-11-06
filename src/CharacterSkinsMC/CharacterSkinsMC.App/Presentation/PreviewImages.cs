#nullable enable
using System.IO;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using CharacterSkinsMC.Core;

namespace CharacterSkinsMC.App.Presentation;

/// <summary>
/// 预览适配层：把 Core 的像素图渲染并转换成 Avalonia 位图。
/// 视图只传控件与参数，不再自行调用渲染算法或管理 Bitmap 生命周期。
/// </summary>
public static class PreviewImages
{
	public static void Set(Image target, RgbaImage? source)
	{
		Bitmap? previous = target.Source as Bitmap;
		target.Source = null;
		previous?.Dispose();
		if (source == null)
		{
			return;
		}
		target.Source = new Bitmap(new MemoryStream(source.ToPngBytes()));
	}

	public static void RenderMcSkin(Image target, MinecraftSkin skin, int scale, bool guides) =>
		Set(target, SkinPreview.RenderMcSkin(skin, scale, guides));

	public static void RenderAtlas(Image target, RgbaImage? atlas, int scale, bool guides, ScUvLayout layout) =>
		Set(target, atlas == null ? null : SkinPreview.RenderAtlas(atlas, scale, guides, layout));

	public static void RenderDifference(Image target, RgbaImage current, RgbaImage reference,
		int scale, bool guides, ScUvLayout layout) =>
		Set(target, ScAtlasComparison.RenderDifference(current, reference, scale, guides, layout));

	public static void RenderMcModel(Image target, MinecraftSkin skin, bool composeOverlay,
		int width, int height, double yawDegrees, double pitchDegrees, bool fillTransparent) =>
		Set(target, SkinPreview.Render3D(skin, composeOverlay, width, height, yawDegrees, pitchDegrees, fillTransparent));

	public static void RenderScModel(Image target, RgbaImage? atlas, ScModel model,
		int width, int height, double yawDegrees, double pitchDegrees, bool fillTransparent) =>
		Set(target, atlas == null ? null : ScModelPreview.Render(atlas, model, width, height, yawDegrees, pitchDegrees, fillTransparent));
}
