#nullable enable
using System.Collections.Generic;

namespace CharacterSkinsMC.App.Infrastructure;

/// <summary>项目托管地址；关于抽屉与关于窗口共用同一份清单。</summary>
public sealed record ProjectLink(string Platform, string Url);

public static class ProjectLinks
{
	public static IReadOnlyList<ProjectLink> All { get; } = new ProjectLink[]
	{
		new("CNB", "https://cnb.cool/SurvivalcraftTool/CharacterSkinsMC"),
		new("Gitee", "https://gitee.com/yylmzxc/character-skins-mc"),
		new("GitHub", "https://github.com/YYLMZXC/CharacterSkinsMC")
	};
}
