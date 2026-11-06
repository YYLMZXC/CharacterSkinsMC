#nullable enable
using System;
using System.Diagnostics;
using System.Reflection;
using Avalonia.Controls;
using Avalonia.Input;
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
		PlatformLinks.ItemsSource = new PlatformLink[]
		{
			new("CNB", "https://cnb.cool/SurvivalcraftTool/CharacterSkinsMC"),
			new("Gitee", "https://gitee.com/yylmzxc/character-skins-mc"),
			new("GitHub", "https://github.com/YYLMZXC/CharacterSkinsMC")
		};
		if (owner != null)
		{
			// Avalonia 12 中 Owner 的 set 访问器是 protected，只能在子类构造器里赋值。
			Owner = owner;
		}
	}

	/// <summary>代码托管平台条目：关于页按平台名 + 地址列出，点击整行打开。</summary>
	private sealed record PlatformLink(string Platform, string Url);

	/// <summary>读程序集 InformationalVersion；Directory.Build.props 在构建时写入 yy.MM.dd-HHmm。</summary>
	public static string GetVersion()
	{
		Assembly assembly = typeof(AboutWindow).Assembly;
		string? version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
		return string.IsNullOrWhiteSpace(version) ? "dev" : version!;
	}

	/// <summary>大版本号：取版本号「-」前的日期段，如 26.11.06。</summary>
	public static string GetMajorVersion(string version) => version.Split('-')[0];

	/// <summary>平台卡片整行可点，点击后用系统默认浏览器打开对应仓库地址。</summary>
	private void OnPlatformLinkPressed(object? sender, PointerPressedEventArgs e)
	{
		if ((sender as Border)?.DataContext is not PlatformLink link || string.IsNullOrWhiteSpace(link.Url))
		{
			return;
		}
		try
		{
			Process.Start(new ProcessStartInfo(link.Url) { UseShellExecute = true });
		}
		catch (Exception ex)
		{
			// 外部浏览器不可用属于既定降级通道：保持窗口可用与链接可读，仅记录完整失败上下文。
			Trace.WriteLine($"[CharacterSkinsMC] 打开项目主页失败 地址={link.Url} 类型={ex.GetType().FullName} 消息={ex.Message}\n{ex}");
		}
	}

	private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();
}
