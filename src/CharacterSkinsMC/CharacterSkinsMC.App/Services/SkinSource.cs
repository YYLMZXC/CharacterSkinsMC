#nullable enable
using System.IO;
using CharacterSkinsMC.Core;

namespace CharacterSkinsMC.App.Services;

/// <summary>输入列表条目的纯数据模型：只描述来源，不做加载、不引用 UI 控件。</summary>
public sealed record SkinSource(string DisplayName, string OutputName, string? FilePath, McBuiltinSkin? Builtin)
{
	public static SkinSource FromFile(string path) =>
		new(Path.GetFileName(path), Path.GetFileNameWithoutExtension(path), path, null);

	public static SkinSource FromBuiltin(McBuiltinSkin skin) =>
		new($"内置：{skin.DisplayName}", skin.DisplayName, null, skin);

	public override string ToString() => DisplayName;
}
