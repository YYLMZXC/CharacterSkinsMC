#nullable enable
using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using CharacterSkinsMC.App.Infrastructure;

namespace CharacterSkinsMC.App.Presentation;

/// <summary>经 TopLevel 启动器打开项目地址：桌面走默认浏览器，Android 走浏览器 Intent。</summary>
public static class ProjectLinkLauncher
{
	public static async Task<bool> OpenAsync(Visual anchor, string url)
	{
		if (string.IsNullOrWhiteSpace(url))
		{
			return false;
		}
		try
		{
			TopLevel? topLevel = TopLevel.GetTopLevel(anchor);
			return topLevel != null && await topLevel.Launcher.LaunchUriAsync(new Uri(url));
		}
		catch (Exception ex)
		{
			AppLog.Error($"打开项目主页失败 地址={url}", ex);
			return false;
		}
	}
}
