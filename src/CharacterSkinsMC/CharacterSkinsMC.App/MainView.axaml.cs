#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using CheckBox = Avalonia.Controls.CheckBox;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CharacterSkinsMC.Core;

namespace CharacterSkinsMC.App;

public partial class MainView : UserControl
{
	private string m_inputDirectory;
	private string m_outputDirectory;
	private MinecraftSkin? m_skin;
	private RgbaImage? m_atlas;
	private double m_yawDegrees = 35;
	private bool m_suppressYawEvent;
	private readonly DispatcherTimer m_timer;

	public MainView()
	{
		InitializeComponent();
		m_inputDirectory = ResolveDataDirectory("CharacterSkinsMC");
		m_outputDirectory = ResolveDataDirectory("CharacterSkins");

		m_timer = new DispatcherTimer(TimeSpan.FromMilliseconds(80), DispatcherPriority.Background, OnTimerTick);
		m_timer.Start();

		if (!OperatingSystem.IsWindows())
		{
			OpenInputButton.IsVisible = false;
		}
		FilesList.AddHandler(DragDrop.DragOverEvent, OnListDragOver);
		FilesList.AddHandler(DragDrop.DropEvent, OnListDrop);
		YawSlider.ValueChanged += (_, e) => OnYawSliderChanged(e.NewValue);
		SlimCheck.PropertyChanged += (_, e) =>
		{
			if (e.Property == CheckBox.IsCheckedProperty)
			{
				OpenSelected();
			}
		};
		OverlayCheck.PropertyChanged += (_, e) =>
		{
			if (e.Property == CheckBox.IsCheckedProperty)
			{
				OpenSelected();
			}
		};
		GuidesCheck.PropertyChanged += (_, e) =>
		{
			if (e.Property == CheckBox.IsCheckedProperty)
			{
				UpdatePreviews();
			}
		};
		this.DetachedFromVisualTree += (_, _) => m_timer.Stop();
		Rescan();
	}

	/// <summary>桌面把数据目录放在程序旁，Android 等沙盒平台回退到应用私有数据目录。</summary>
	private static string ResolveDataDirectory(string name)
	{
		try
		{
			string root = OperatingSystem.IsAndroid() || OperatingSystem.IsIOS()
				? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
				: AppContext.BaseDirectory;
			if (string.IsNullOrWhiteSpace(root))
			{
				root = AppContext.BaseDirectory;
			}
			string directory = Path.Combine(root, name);
			Directory.CreateDirectory(directory);
			return directory;
		}
		catch
		{
			return AppContext.BaseDirectory;
		}
	}

	private void OnTimerTick(object? sender, EventArgs e)
	{
		if (AutoRotateCheck.IsChecked != true || m_skin == null)
		{
			return;
		}
		m_yawDegrees = (m_yawDegrees + 2.5) % 360.0;
		m_suppressYawEvent = true;
		YawSlider.Value = m_yawDegrees;
		m_suppressYawEvent = false;
		RenderModel();
	}

	private void OnYawSliderChanged(double value)
	{
		if (m_suppressYawEvent)
		{
			return;
		}
		m_yawDegrees = value;
		RenderModel();
	}

	private void OnFileSelected(object? sender, SelectionChangedEventArgs e) => OpenSelected();

	private void OnConvertOptionChanged(object? sender, RoutedEventArgs e) => OpenSelected();

	private void OnGuidesChanged(object? sender, RoutedEventArgs e) => UpdatePreviews();

	private void OnListDragOver(object? sender, DragEventArgs e)
	{
		e.DragEffects = e.DataTransfer.Contains(DataFormat.File) ? DragDropEffects.Copy : DragDropEffects.None;
	}

	private async void OnListDrop(object? sender, DragEventArgs e)
	{
		foreach (DataTransferItem item in e.DataTransfer.Items)
		{
			if (item.TryGetFile() is IStorageFile file)
			{
				await ImportStorageFileAsync(file);
			}
		}
		Rescan();
	}

	private async void OnOpenFilesClick(object? sender, RoutedEventArgs e)
	{
		TopLevel? topLevel = TopLevel.GetTopLevel(this);
		if (topLevel == null)
		{
			return;
		}
		try
		{
			IReadOnlyList<IStorageFile> files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
			{
				Title = "选择 Minecraft 皮肤",
				AllowMultiple = true,
				FileTypeFilter = new FilePickerFileType[]
				{
					new("PNG 图像") { Patterns = new[] { "*.png" } }
				}
			});
			foreach (IStorageFile file in files)
			{
				await ImportStorageFileAsync(file);
			}
			Rescan();
		}
		catch (Exception ex)
		{
			Trace.WriteLine($"[CharacterSkinsMC] 打开皮肤失败 类型={ex.GetType().FullName} 消息={ex.Message}\n{ex}");
			SetStatus($"打开皮肤失败：{ex.Message}");
		}
	}

	private async Task ImportStorageFileAsync(IStorageFile file)
	{
		string fileName = file.Name;
		if (!Path.GetExtension(fileName).Equals(".png", StringComparison.OrdinalIgnoreCase))
		{
			return;
		}
		string target = Path.Combine(m_inputDirectory, fileName);
		string? localPath = file.TryGetLocalPath();
		if (localPath != null && string.Equals(Path.GetFullPath(localPath), Path.GetFullPath(target), StringComparison.OrdinalIgnoreCase))
		{
			return;
		}
		await using Stream source = await file.OpenReadAsync();
		await using Stream output = File.Create(target);
		await source.CopyToAsync(output);
	}

	private void OnRescanClick(object? sender, RoutedEventArgs e) => Rescan();

	private void Rescan()
	{
		FilesList.Items.Clear();
		foreach (string path in Directory.GetFiles(m_inputDirectory, "*.png").OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
		{
			FilesList.Items.Add(path);
		}
		if (FilesList.Items.Count > 0)
		{
			FilesList.SelectedIndex = 0;
		}
		else
		{
			ClearPreviews();
			SetStatus($"输入目录为空：{m_inputDirectory}，请把 Minecraft 的 skin.png 放进去或拖入列表。");
		}
	}

	private void OpenSelected()
	{
		if (FilesList.SelectedItem is not string path)
		{
			ClearPreviews();
			return;
		}
		try
		{
			McArmModel armModel = SlimCheck.IsChecked == true ? McArmModel.Slim : McArmModel.Classic;
			m_skin = MinecraftSkin.Load(path, armModel);
			m_atlas = MinecraftSkinConverter.Convert(m_skin, OverlayCheck.IsChecked == true);
			UpdatePreviews();
			SetStatus($"{Path.GetFileName(path)} | {m_skin.SourceDescription} | 手臂={(m_skin.ArmModel == McArmModel.Slim ? "Slim" : "Classic")} | 第二层={(m_skin.HasOverlay && OverlayCheck.IsChecked == true ? "已合成" : "未合成")} | 输出={m_outputDirectory}");
		}
		catch (Exception ex)
		{
			Trace.WriteLine($"[CharacterSkinsMC] 皮肤读取失败 文件={path} 类型={ex.GetType().FullName} 消息={ex.Message}\n{ex}");
			SetStatus($"读取失败：{ex.Message}");
			ClearPreviews();
		}
	}

	private void UpdatePreviews()
	{
		if (m_skin == null || m_atlas == null)
		{
			return;
		}
		SetImage(McPreviewImage, SkinPreview.RenderMcSkin(m_skin, 7, GuidesCheck.IsChecked == true));
		SetImage(ScPreviewImage, SkinPreview.RenderAtlas(m_atlas, 7, GuidesCheck.IsChecked == true));
		RenderModel();
	}

	private void RenderModel()
	{
		if (m_skin == null)
		{
			return;
		}
		SetImage(ModelPreviewImage, SkinPreview.Render3D(m_skin, OverlayCheck.IsChecked == true, 460, 540, m_yawDegrees, 18));
	}

	private void ClearPreviews()
	{
		m_skin = null;
		m_atlas = null;
		SetImage(McPreviewImage, null);
		SetImage(ScPreviewImage, null);
		SetImage(ModelPreviewImage, null);
	}

	/// <summary>RgbaImage 经 PNG 解码为 Avalonia 位图；替换前释放旧图。</summary>
	private static void SetImage(Image target, RgbaImage? source)
	{
		Bitmap? previous = target.Source as Bitmap;
		target.Source = null;
		previous?.Dispose();
		if (source == null)
		{
			return;
		}
		byte[] png = source.ToPngBytes();
		target.Source = new Bitmap(new MemoryStream(png));
	}

	private void OnSaveCurrentClick(object? sender, RoutedEventArgs e) => SaveCurrent();

	private void SaveCurrent()
	{
		if (m_atlas == null || FilesList.SelectedItem is not string path)
		{
			SetStatus("请先在列表中选择一个皮肤。");
			return;
		}
		string output = Path.Combine(m_outputDirectory, Path.GetFileNameWithoutExtension(path) + ".scskin");
		try
		{
			string saved = MinecraftSkinConverter.SaveScSkin(m_atlas, output);
			SetStatus($"已保存：{saved}");
		}
		catch (Exception ex)
		{
			Trace.WriteLine($"[CharacterSkinsMC] 保存失败 路径={output} 类型={ex.GetType().FullName} 消息={ex.Message}\n{ex}");
			SetStatus($"保存失败：{ex.Message}");
		}
	}

	private void OnBatchConvertClick(object? sender, RoutedEventArgs e) => BatchConvert();

	private void BatchConvert()
	{
		var paths = FilesList.Items.OfType<string>().ToList();
		if (paths.Count == 0)
		{
			SetStatus("输入目录中没有 PNG 皮肤。");
			return;
		}
		int success = 0;
		var failures = new List<string>();
		foreach (string path in paths)
		{
			try
			{
				McArmModel armModel = SlimCheck.IsChecked == true ? McArmModel.Slim : McArmModel.Classic;
				MinecraftSkin skin = MinecraftSkin.Load(path, armModel);
				RgbaImage atlas = MinecraftSkinConverter.Convert(skin, OverlayCheck.IsChecked == true);
				string output = Path.Combine(m_outputDirectory, Path.GetFileNameWithoutExtension(path) + ".scskin");
				MinecraftSkinConverter.SaveScSkin(atlas, output);
				success++;
			}
			catch (Exception ex)
			{
				Trace.WriteLine($"[CharacterSkinsMC] 批量转换失败 文件={path} 类型={ex.GetType().FullName} 消息={ex.Message}\n{ex}");
				failures.Add($"{Path.GetFileName(path)}：{ex.Message}");
			}
		}
		string message = $"批量转换完成 {success}/{paths.Count}，输出目录：{m_outputDirectory}";
		if (failures.Count > 0)
		{
			message += "；失败：" + string.Join("；", failures.Take(6));
		}
		SetStatus(message);
	}

	private void OnOpenInputDirectoryClick(object? sender, RoutedEventArgs e)
	{
		if (!OperatingSystem.IsWindows())
		{
			SetStatus($"当前平台不支持打开资源管理器，输入目录：{m_inputDirectory}");
			return;
		}
		try
		{
			Process.Start(new ProcessStartInfo("explorer.exe", m_inputDirectory) { UseShellExecute = true });
		}
		catch (Exception ex)
		{
			Trace.WriteLine($"[CharacterSkinsMC] 打开目录失败 路径={m_inputDirectory} 类型={ex.GetType().FullName} 消息={ex.Message}");
			SetStatus($"打开目录失败：{ex.Message}");
		}
	}

	private async void OnChooseOutputClick(object? sender, RoutedEventArgs e)
	{
		TopLevel? topLevel = TopLevel.GetTopLevel(this);
		if (topLevel == null)
		{
			return;
		}
		try
		{
			IReadOnlyList<IStorageFolder> folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
			{
				Title = "选择 .scskin 输出目录",
				AllowMultiple = false
			});
			string? path = folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
			if (string.IsNullOrEmpty(path))
			{
				SetStatus($"当前平台无法选择自定义目录，输出目录仍为：{m_outputDirectory}");
				return;
			}
			m_outputDirectory = path!;
			Directory.CreateDirectory(m_outputDirectory);
			SetStatus($"输出目录已设为：{m_outputDirectory}");
		}
		catch (Exception ex)
		{
			Trace.WriteLine($"[CharacterSkinsMC] 选择输出目录失败 类型={ex.GetType().FullName} 消息={ex.Message}\n{ex}");
			SetStatus($"选择输出目录失败：{ex.Message}");
		}
	}

	private void SetStatus(string text) => StatusBar.Text = text;
}
