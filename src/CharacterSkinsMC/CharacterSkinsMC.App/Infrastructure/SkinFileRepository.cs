#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CharacterSkinsMC.App.Services;
using CharacterSkinsMC.Core;

namespace CharacterSkinsMC.App.Infrastructure;

/// <summary>基于本地目录的皮肤仓库：扫描、导入、读取与保存全部集中在此。</summary>
public sealed class SkinFileRepository : ISkinRepository
{
	private readonly AppPaths m_paths;

	public SkinFileRepository(AppPaths paths)
	{
		ArgumentNullException.ThrowIfNull(paths);
		m_paths = paths;
	}

	public string InputDirectory => m_paths.InputDirectory;
	public string OutputDirectory => m_paths.OutputDirectory;

	public IReadOnlyList<SkinSource> ListSources()
	{
		var sources = new List<SkinSource>();
		// 内置示例皮肤始终排在最前，空目录或 Android 沙盒下也能直接转换预览。
		foreach (McBuiltinSkin builtin in McBuiltinSkins.All)
		{
			sources.Add(SkinSource.FromBuiltin(builtin));
		}
		foreach (string path in Directory.GetFiles(InputDirectory, "*" + AppPaths.PngExtension)
			.OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
		{
			sources.Add(SkinSource.FromFile(path));
		}
		return sources;
	}

	public byte[] ReadSource(SkinSource source)
	{
		ArgumentNullException.ThrowIfNull(source);
		return source.Builtin != null
			? McBuiltinSkins.ReadPng(source.Builtin)
			: ImageFiles.ReadBytes(source.FilePath!);
	}

	public Task ImportAsync(string fileName, Stream source, string? sourcePath = null)
	{
		if (!AppPaths.IsPngFile(fileName))
		{
			return Task.CompletedTask;
		}
		string target = Path.Combine(InputDirectory, fileName);
		if (sourcePath != null && string.Equals(Path.GetFullPath(sourcePath), Path.GetFullPath(target),
			StringComparison.OrdinalIgnoreCase))
		{
			return Task.CompletedTask;
		}
		return CopyIntoInputAsync(target, source);
	}

	private static async Task CopyIntoInputAsync(string target, Stream source)
	{
		await using Stream output = File.Create(target);
		await source.CopyToAsync(output);
	}

	public string Save(RgbaImage atlas, SkinSource source)
	{
		ArgumentNullException.ThrowIfNull(source);
		string path = m_paths.ScSkinPath(source.OutputName);
		ImageFiles.WriteScSkin(path, atlas);
		return path;
	}

	public string ExportDefault(ScDefaultSkin skin)
	{
		ArgumentNullException.ThrowIfNull(skin);
		string path = m_paths.ScSkinPath($"Survivalcraft-{skin.DisplayName}");
		ImageFiles.WriteScSkin(path, ScDefaultSkins.LoadSkin(skin));
		return path;
	}

	public void SetOutputDirectory(string path) => m_paths.SetOutputDirectory(path);
}
