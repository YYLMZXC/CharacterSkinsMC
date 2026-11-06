#nullable enable
using System;
using Avalonia;
using CharacterSkinsMC.App.Diagnostics;

namespace CharacterSkinsMC.App;

// 桌面入口；csproj 已把本文件从 android TFM 编译列表移除，Android 入口在 Platforms/Android/MainApplication。
internal static class Program
{
	[STAThread]
	public static void Main(string[] args)
	{
		if (args.Length == 1 && args[0] == "--smoke")
		{
			SmokeTest.Run();
			return;
		}
		if (args.Length == 2 && args[0] == "--dump")
		{
			PreviewDumper.Dump(args[1]);
			return;
		}
		BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
	}

	public static AppBuilder BuildAvaloniaApp() =>
		AppBuilder.Configure<App>()
			.UsePlatformDetect()
			.LogToTrace();
}
