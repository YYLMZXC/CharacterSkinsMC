#nullable enable
using System;
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
		VersionText.Text = $"版本 {GetVersion()}（按构建时间自动生成）";
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

	private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();
}
