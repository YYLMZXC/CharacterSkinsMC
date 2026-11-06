#nullable enable
using Android.App;
using Android.Content.PM;
using Avalonia;
using Avalonia.Android;

namespace CharacterSkinsMC.App.Platforms.Android;

// Avalonia 12 移除了泛型版 AvaloniaMainActivity，须继承非泛型基类。
[Activity(
	Label = "CharacterSkinsMC",
	Theme = "@android:style/Theme.Material.Light.NoActionBar",
	MainLauncher = true,
	ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
public class MainActivity : AvaloniaMainActivity
{
}
