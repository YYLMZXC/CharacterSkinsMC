#nullable enable
using System;
using Android.App;
using Android.Runtime;
using Avalonia;
using Avalonia.Android;

namespace CharacterSkinsMC.App.Platforms.Android;

// Avalonia 12 的 Android 入口：Application 必须继承 AvaloniaAndroidApplication<App>。
[Application]
public class MainApplication : AvaloniaAndroidApplication<App>
{
	public MainApplication(IntPtr javaReference, JniHandleOwnership transfer) : base(javaReference, transfer)
	{
	}

	protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
	{
		return base.CustomizeAppBuilder(builder);
	}
}
