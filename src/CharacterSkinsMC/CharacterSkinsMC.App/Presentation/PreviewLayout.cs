#nullable enable
namespace CharacterSkinsMC.App.Presentation;

/// <summary>预览缩放、3D 画布、旋转与响应式布局的统一常量表。</summary>
internal static class PreviewLayout
{
	public const int AtlasScale = 7;
	public const int CompareScale = 5;
	public const int ModelWidth = 460;
	public const int ModelHeight = 540;

	public const double MinRotationSpeed = 5.0;
	public const double MaxRotationSpeed = 180.0;
	public const double MinPitchDegrees = -80.0;
	public const double MaxPitchDegrees = 80.0;
	public const double DragYawDegreesPerPixel = 0.5;
	public const double DragPitchDegreesPerPixel = 0.4;
	public const double CompactLayoutThreshold = 800.0;
	public const double SidePanelWidth = 320.0;
}
