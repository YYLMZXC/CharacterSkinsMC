#nullable enable
using System;
using CharacterSkinsMC.Core;

namespace CharacterSkinsMC.App.Services;

/// <summary>纯计算服务：PNG 字节 → 归一化皮肤 → SC 图集，不接触文件系统与 UI。</summary>
public sealed class SkinConversionService
{
	public SkinConversionResult Convert(byte[] png, ConversionOptions options)
	{
		ArgumentNullException.ThrowIfNull(png);
		ArgumentNullException.ThrowIfNull(options);
		MinecraftSkin skin = MinecraftSkin.Load(png, options.ArmModel);
		RgbaImage atlas = MinecraftSkinConverter.Convert(skin, options.ComposeOverlay, options.FillElytra);
		return new SkinConversionResult(skin, atlas);
	}
}
