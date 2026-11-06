#nullable enable
using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace CharacterSkinsMC.Core;

/// <summary>读取本程序集内嵌的 Survivalcraft 原始资产，工具独立运行不依赖主仓 Content 路径。</summary>
internal static class EmbeddedAssets
{
	public static byte[] Read(string fileNameSuffix)
	{
		Assembly assembly = typeof(EmbeddedAssets).Assembly;
		string? resourceName = assembly.GetManifestResourceNames()
			.FirstOrDefault(name => name.EndsWith(fileNameSuffix, StringComparison.OrdinalIgnoreCase));
		if (resourceName == null)
		{
			throw new InvalidOperationException($"找不到内嵌资产：{fileNameSuffix}");
		}
		using Stream stream = assembly.GetManifestResourceStream(resourceName)
			?? throw new InvalidOperationException($"无法打开内嵌资产：{resourceName}");
		using var buffer = new MemoryStream();
		stream.CopyTo(buffer);
		return buffer.ToArray();
	}
}
