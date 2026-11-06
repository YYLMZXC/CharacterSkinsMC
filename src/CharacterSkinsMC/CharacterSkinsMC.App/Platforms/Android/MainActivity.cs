#nullable enable
using Android.App;
using Android.Content.PM;
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
}
