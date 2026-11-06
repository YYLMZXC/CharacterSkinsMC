#nullable enable
using System;
using System.IO;
using CharacterSkinsMC.Core;

namespace CharacterSkinsMC.App.Infrastructure;

/// <summary>
/// Core 只处理内存字节，所有磁盘读写集中在这里：
/// 转换核心既不依赖文件系统，也不关心 .scskin 的命名规则。
/// </summary>
public static class ImageFiles
{
	public static byte[] ReadBytes(string path)
	{
		if (!File.Exists(path))
		{
			throw new FileNotFoundException("找不到图片文件。", path);
		}
		return File.ReadAllBytes(path);
	}

	public static RgbaImage ReadImage(string path) => RgbaImage.FromMemory(ReadBytes(path));

	public static void WritePng(string path, RgbaImage image)
	{
		ArgumentNullException.ThrowIfNull(image);
		WriteBytes(path, image.ToPngBytes());
	}

	public static void WriteScSkin(string path, RgbaImage atlas)
	{
		ArgumentNullException.ThrowIfNull(atlas);
		WriteBytes(path, MinecraftSkinConverter.ToScSkinBytes(atlas));
	}

	private static void WriteBytes(string path, byte[] bytes)
	{
		string? directory = Path.GetDirectoryName(Path.GetFullPath(path));
		if (!string.IsNullOrEmpty(directory))
		{
			Directory.CreateDirectory(directory);
		}
		File.WriteAllBytes(path, bytes);
	}
}
