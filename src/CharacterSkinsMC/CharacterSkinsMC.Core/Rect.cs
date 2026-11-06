#nullable enable
namespace CharacterSkinsMC.Core;

/// <summary>皮肤图集上的整数像素矩形，坐标原点在左上角。</summary>
public readonly record struct Rect(int X, int Y, int W, int H);
