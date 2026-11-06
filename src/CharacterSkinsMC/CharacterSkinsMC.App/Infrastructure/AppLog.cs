#nullable enable
using System;
using System.Diagnostics;

namespace CharacterSkinsMC.App.Infrastructure;

/// <summary>统一的错误记录与用户可读文案生成，避免每个界面事件各写一套 Trace。</summary>
public static class AppLog
{
	public static void Error(string context, Exception ex) =>
		Trace.WriteLine($"[CharacterSkinsMC] {context} 类型={ex.GetType().FullName} 消息={ex.Message}\n{ex}");

	public static string Failure(string prefix, string context, Exception ex)
	{
		Error(context, ex);
		return $"{prefix}：{ex.Message}";
	}
}
