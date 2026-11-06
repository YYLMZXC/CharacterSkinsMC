#nullable enable
using System;
using System.Diagnostics;
using System.Reflection;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace CharacterSkinsMC.App;

public partial class AboutWindow : Window
{
	// 无参构造器供 XAML 运行时加载器使用（AVLN3001）。
	public AboutWindow() : this(null)
	{
	}

	public AboutWindow(Window? owner)
	{
		InitializeComponent();
		string version = GetVersion();
		VersionText.Text = $"版本 {version}（按构建时间自动生成）";
		// 标题显示大版本号（日期部分），如「关于 CharacterSkinsMC 26.11.06」。
		Title = $"关于 CharacterSkinsMC {GetMajorVersion(version)}";
		if (owner != null)
		{
			// Avalonia 12 中 Owner 的 set 访问器是 protected，只能在子类构造器里赋值。
			Owner = owner;
		}
	}

	/// <summary>读程序集 InformationalVersion；Directory.Build.props 在构建时写入 yy.MM.dd-HHmm。</summary>
	public static string GetVersion()
	{
		Assembly assembly = typeof(AboutWindow).Assembly;
		string? version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
		return string.IsNullOrWhiteSpace(version) ? "dev" : version!;
	}

	/// <summary>大版本号：取版本号「-」前的日期段，如 26.11.06。</summary>
	public static string GetMajorVersion(string version) => version.Split('-')[0];

	private void OnOpenProjectUrlClick(object? sender, RoutedEventArgs e)
	{
		try
		{
			var psi = new ProcessStartInfo(ProjectUrlText.Text ?? string.Empty) { UseShellExecute = true };
			Process.Start(psi);
		}
		catch (Exception ex)
		{
			Trace.WriteLine($"[CharacterSkinsMC] 打开项目主页失败 地址={ProjectUrlText.Text} 消息={ex.Message}");
		}
	}

	private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();
}
