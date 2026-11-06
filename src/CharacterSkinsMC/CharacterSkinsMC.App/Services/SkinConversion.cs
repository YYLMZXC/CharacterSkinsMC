#nullable enable
using System.Collections.Generic;
using CharacterSkinsMC.Core;

namespace CharacterSkinsMC.App.Services;

/// <summary>由界面选项组装的转换参数，业务逻辑不再直接读控件。</summary>
public sealed record ConversionOptions(McArmModel ArmModel, bool ComposeOverlay, bool FillElytra);

public sealed record SkinConversionResult(MinecraftSkin Skin, RgbaImage Atlas);

public sealed record BatchConversionReport(int Total, int Succeeded, IReadOnlyList<string> Failures);
