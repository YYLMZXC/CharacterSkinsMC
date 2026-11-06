#nullable enable
using System;
using System.IO;

namespace CharacterSkinsMC.App.Infrastructure;

/// <summary>集中管理输入/输出目录、扩展名与产物路径。</summary>
public sealed class AppPaths
{
	public const string PngExtension = ".png";
	public const string ScSkinExtension = ".scskin";
	private const string InputFolderName = "CharacterSkinsMC";
	private const string OutputFolderName = "CharacterSkins";

	public string InputDirectory { get; }
	public string OutputDirectory { get; private set; }

	public AppPaths(IAppEnvironment environment)
	{
		ArgumentNullException.ThrowIfNull(environment);
		InputDirectory = EnsureDefaultDirectory(Path.Combine(environment.DataRoot, InputFolderName));
		OutputDirectory = EnsureDefaultDirectory(Path.Combine(environment.DataRoot, OutputFolderName));
	}

	/// <summary>用户自选目录：创建失败时直接抛给调用方显示，不静默回退。</summary>
	public void SetOutputDirectory(string path)
	{
		Directory.CreateDirectory(path);
		OutputDirectory = path;
	}

	public string ScSkinPath(string outputName) =>
		Path.Combine(OutputDirectory, outputName + ScSkinExtension);

	public static bool IsPngFile(string fileName) =>
		Path.GetExtension(fileName).Equals(PngExtension, StringComparison.OrdinalIgnoreCase);

	private static string EnsureDefaultDirectory(string path)
	{
		try
		{
			Directory.CreateDirectory(path);
			return path;
		}
		catch
		{
			// 只读安装目录等场景下退到程序目录，保证界面仍可启动。
			return AppContext.BaseDirectory;
		}
	}
}
