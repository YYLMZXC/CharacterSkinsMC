#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using CharacterSkinsMC.App.Infrastructure;
using CharacterSkinsMC.Core;

namespace CharacterSkinsMC.App.Services;

/// <summary>
/// 应用服务：把「来源 → 字节 → 转换结果 → 产物」编排成一条流程。
/// 界面只依赖本类，不感知仓库实现、转换算法与磁盘路径。
/// </summary>
public sealed class SkinWorkspace
{
	private readonly ISkinRepository m_repository;
	private readonly SkinConversionService m_converter;

	public SkinWorkspace(ISkinRepository repository, SkinConversionService converter)
	{
		ArgumentNullException.ThrowIfNull(repository);
		ArgumentNullException.ThrowIfNull(converter);
		m_repository = repository;
		m_converter = converter;
	}

	public string InputDirectory => m_repository.InputDirectory;
	public string OutputDirectory => m_repository.OutputDirectory;

	public IReadOnlyList<SkinSource> GetSources() => m_repository.ListSources();

	public Task ImportAsync(string fileName, Stream source, string? sourcePath = null) =>
		m_repository.ImportAsync(fileName, source, sourcePath);

	public void SetOutputDirectory(string path) => m_repository.SetOutputDirectory(path);

	public SkinConversionResult Open(SkinSource source, ConversionOptions options) =>
		m_converter.Convert(m_repository.ReadSource(source), options);

	public string Save(SkinSource source, RgbaImage atlas) => m_repository.Save(atlas, source);

	public string ExportDefault(ScDefaultSkin skin) => m_repository.ExportDefault(skin);

	public BatchConversionReport SaveAll(IReadOnlyList<SkinSource> sources, ConversionOptions options)
	{
		ArgumentNullException.ThrowIfNull(sources);
		int succeeded = 0;
		var failures = new List<string>();
		foreach (SkinSource source in sources)
		{
			try
			{
				SkinConversionResult result = Open(source, options);
				m_repository.Save(result.Atlas, source);
				succeeded++;
			}
			catch (Exception ex)
			{
				AppLog.Error($"批量转换失败 文件={source.DisplayName}", ex);
				failures.Add($"{source.DisplayName}：{ex.Message}");
			}
		}
		return new BatchConversionReport(sources.Count, succeeded, failures);
	}
}
