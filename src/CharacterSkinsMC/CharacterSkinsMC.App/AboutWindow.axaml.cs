#nullable enable
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using CharacterSkinsMC.App.Infrastructure;
using CharacterSkinsMC.App.Presentation;

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
		VersionText.Text = $"版本 {AppVersion.Full}（按构建时间自动生成）";
		// 标题显示大版本号（日期部分），如「关于 MC皮肤转SC 26.11.06」。
		Title = $"关于 MC皮肤转SC {AppVersion.Major}";
		PlatformLinks.ItemsSource = ProjectLinks.All;
		if (owner != null)
		{
			// Avalonia 12 中 Owner 的 set 访问器是 protected，只能在子类构造器里赋值。
			Owner = owner;
		}
	}

	/// <summary>平台卡片整块可点，与关于抽屉共用同一启动器。</summary>
	private async void OnPlatformLinkPressed(object? sender, PointerPressedEventArgs e)
	{
		if ((sender as Border)?.DataContext is not ProjectLink link)
		{
			return;
		}
		await ProjectLinkLauncher.OpenAsync(this, link.Url);
	}

	private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();
}
