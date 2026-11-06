#nullable enable
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using CharacterSkinsMC.App.Services;
using CharacterSkinsMC.Core;

namespace CharacterSkinsMC.App.Infrastructure;

/// <summary>皮肤来源与产物的持久化边界；替换实现即可接入其它存储而不改业务与界面。</summary>
public interface ISkinRepository
{
	string InputDirectory { get; }
	string OutputDirectory { get; }
	IReadOnlyList<SkinSource> ListSources();
	byte[] ReadSource(SkinSource source);
	Task ImportAsync(string fileName, Stream source, string? sourcePath = null);
	string Save(RgbaImage atlas, SkinSource source);
	string ExportDefault(ScDefaultSkin skin);
	void SetOutputDirectory(string path);
}
