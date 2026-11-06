#nullable enable
using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using AndroidX.Core.View;
using Avalonia;
using Avalonia.Android;

namespace CharacterSkinsMC.App.Platforms.Android;

// Avalonia 12 移除了泛型版 AvaloniaMainActivity，须继承非泛型基类。
// 基类继承 AppCompatActivity，onCreate 里 setContentView 要求 AppCompat 主题；
// 用平台 Theme.Material 会抛 IllegalStateException 导致启动即闪退。
[Activity(
	Label = "CharacterSkinsMC",
	Theme = "@style/Theme.AppCompat.Light.NoActionBar",
	MainLauncher = true,
	ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
public class MainActivity : AvaloniaMainActivity
{
	protected override void OnCreate(Bundle? savedInstanceState)
	{
		base.OnCreate(savedInstanceState);
		// targetSdk 35+（Android 15 起）强制 edge-to-edge，内容会延伸到状态栏与导航栏下面，
		// 底部按钮/状态栏文字被系统导航栏挡住。统一改为：内容铺满全屏，
		// 把系统栏 insets 转成内容根的内边距，全部版本行为一致。
		WindowCompat.SetDecorFitsSystemWindows(Window!, false);
		// global:: 前缀避免本命名空间 ...Platforms.Android 遮蔽平台命名空间。
		if (FindViewById<ViewGroup>(global::Android.Resource.Id.Content) is { } root)
		{
			ViewCompat.SetOnApplyWindowInsetsListener(root, new SystemBarInsetsListener());
		}
	}

	/// <summary>把状态栏/导航栏 insets 落到内容根内边距，返回 insets 让事件继续传递。</summary>
	private sealed class SystemBarInsetsListener : Java.Lang.Object, IOnApplyWindowInsetsListener
	{
		public WindowInsetsCompat? OnApplyWindowInsets(View? view, WindowInsetsCompat? insets)
		{
			if (view == null || insets == null)
			{
				return insets;
			}
			var bars = insets.GetInsets(WindowInsetsCompat.Type.SystemBars());
			if (bars != null)
			{
				view.SetPadding(bars.Left, bars.Top, bars.Right, bars.Bottom);
			}
			return insets;
		}
	}
}
