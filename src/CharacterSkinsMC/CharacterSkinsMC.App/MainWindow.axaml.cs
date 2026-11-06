#nullable enable
using Avalonia.Controls;

namespace CharacterSkinsMC.App;

public partial class MainWindow : Window
{
	public MainWindow()
	{
		InitializeComponent();
		// 桌面窗口标题附带大版本号（构建日期段），与「关于」窗口口径一致。
		Title = $"{Title} {AboutWindow.GetMajorVersion(AboutWindow.GetVersion())}";
	}
}
