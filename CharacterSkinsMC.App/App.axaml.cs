#nullable enable
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace CharacterSkinsMC.App;

// Android TFM 下有全局 using Android.App，须限定 Application 到 Avalonia。
public partial class App : Avalonia.Application
{
	public override void Initialize()
	{
		AvaloniaXamlLoader.Load(this);
	}

	// 共享桌面 + SingleView 双生命周期：桌面由 Program.cs 启动，Android 由 MainApplication 启动。
	public override void OnFrameworkInitializationCompleted()
	{
		if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
		{
			desktop.MainWindow = new MainWindow();
		}
		else if (ApplicationLifetime is ISingleViewApplicationLifetime single)
		{
			single.MainView = new MainView();
		}
		base.OnFrameworkInitializationCompleted();
	}
}
