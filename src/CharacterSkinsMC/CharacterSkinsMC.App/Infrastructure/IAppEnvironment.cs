#nullable enable
using System;
using System.Diagnostics;

namespace CharacterSkinsMC.App.Infrastructure;

/// <summary>平台差异的唯一出口：业务与视图不再直接判断 OperatingSystem。</summary>
public interface IAppEnvironment
{
	bool CanOpenDirectory { get; }
	string DataRoot { get; }
	void OpenDirectory(string path);
}

/// <summary>桌面与移动沙盒共用的默认平台实现。</summary>
public sealed class AppEnvironment : IAppEnvironment
{
	public bool CanOpenDirectory => OperatingSystem.IsWindows();

	public string DataRoot { get; }

	public AppEnvironment()
	{
		string root = OperatingSystem.IsAndroid() || OperatingSystem.IsIOS()
			? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
			: AppContext.BaseDirectory;
		DataRoot = string.IsNullOrWhiteSpace(root) ? AppContext.BaseDirectory : root;
	}

	public void OpenDirectory(string path)
	{
		if (!CanOpenDirectory)
		{
			throw new PlatformNotSupportedException("当前平台不支持打开资源管理器。");
		}
		Process.Start(new ProcessStartInfo("explorer.exe", path) { UseShellExecute = true });
	}
}
