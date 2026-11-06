#nullable enable
using System.Reflection;

namespace CharacterSkinsMC.App.Infrastructure;

/// <summary>程序集版本信息的唯一读取点，不依赖任何窗口类。</summary>
public static class AppVersion
{
	/// <summary>Directory.Build.props 写入的 InformationalVersion（yy.MM.dd-HHmm）。</summary>
	public static string Full
	{
		get
		{
			string? version = typeof(AppVersion).Assembly
				.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
			return string.IsNullOrWhiteSpace(version) ? "dev" : version!;
		}
	}

	/// <summary>大版本号：取「-」前的日期段，如 26.11.06。</summary>
	public static string Major => Full.Split('-')[0];
}
