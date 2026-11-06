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
using RadioButton = Avalonia.Controls.RadioButton;
using ScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility;
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
	private RgbaImage? m_selectedDefaultAtlas;
	private RgbaImage? m_referenceAtlas;
	private ScDefaultSkin? m_referenceSkin;
	private double m_yawDegrees = 35;
	private bool m_suppressYawEvent;
	private bool m_uiReady;
	private bool m_compactLayout;
	private readonly Dictionary<ScPlayerClass, ScModel> m_models = new();
	private readonly DispatcherTimer m_timer;
	private readonly Stopwatch m_rotationClock = Stopwatch.StartNew();

	private const double MinRotationSpeed = 5.0;
	private const double MaxRotationSpeed = 180.0;
	private const double CompactLayoutThreshold = 800.0;
	private const double SidePanelWidth = 320.0;

	/// <summary>输入列表条目：内置皮肤（无磁盘路径）或输入目录中的 PNG 文件。</summary>
	private sealed class SkinListEntry
	{
		public SkinListEntry(string displayName, string? filePath, McBuiltinSkin? builtin)
		{
			DisplayName = displayName;
			FilePath = filePath;
			Builtin = builtin;
		}

		public string DisplayName { get; }
		public string? FilePath { get; }
		public McBuiltinSkin? Builtin { get; }

		/// <summary>保存 .scskin 时的文件名（不含扩展名）。</summary>
		public string OutputName => Builtin == null
			? Path.GetFileNameWithoutExtension(FilePath!)
			: Builtin.DisplayName;

		public override string ToString() => DisplayName;
	}

	private ScPlayerClass CurrentPlayerClass =>
		PlayerClassCombo.SelectedIndex == 1 ? ScPlayerClass.Female : ScPlayerClass.Male;

	/// <summary>转译结果页按左侧性别选择 SC 原版分区表。</summary>
	private ScUvLayout CurrentUvLayout => ScUvLayouts.Get(CurrentPlayerClass);

	/// <summary>内置默认皮肤页按选中皮肤自身的性别选择分区表。</summary>
	private ScUvLayout DefaultSkinUvLayout =>
		ScUvLayouts.Get(CurrentDefaultSkin()?.PlayerClass ?? CurrentPlayerClass);

	/// <summary>对照皮肤列按对照皮肤自身的性别选择分区表。</summary>
	private ScUvLayout ReferenceUvLayout =>
		ScUvLayouts.Get(m_referenceSkin?.PlayerClass ?? CurrentPlayerClass);

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
		SpeedSlider.ValueChanged += (_, e) =>
		{
			if (m_uiReady)
			{
				UpdateSpeedLabel(e.NewValue);
			}
		};
		// 宽手（Steve）/ 细手（Alex）单选：先设默认选中态再订阅事件，避免构造期间触发刷新。
		// Avalonia 12 的 RadioButton 没有 Checked 事件，须通过 PropertyChanged 监听 IsCheckedProperty。
		ArmClassicRadio.IsChecked = true;
		ArmClassicRadio.PropertyChanged += (_, e) =>
		{
			if (e.Property == RadioButton.IsCheckedProperty)
			{
				OpenSelected();
			}
		};
		ArmSlimRadio.PropertyChanged += (_, e) =>
		{
			if (e.Property == RadioButton.IsCheckedProperty)
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
				RefreshGuides();
			}
		};
		this.DetachedFromVisualTree += (_, _) => m_timer.Stop();
		this.PropertyChanged += (_, e) =>
		{
			if (e.Property == BoundsProperty)
			{
				ApplyResponsiveLayout(Bounds.Width);
			}
		};
		this.AttachedToVisualTree += (_, _) => ApplyResponsiveLayout(Bounds.Width);
		PlayerClassCombo.SelectionChanged += OnPlayerClassChanged;
		AboutVersionText.Text = $"版本 {AboutWindow.GetVersion()}（按构建时间自动生成）";
		// 所有 XAML 命名控件都已填充，才允许事件处理器访问列表与预览控件。
		m_uiReady = true;
		UpdateSpeedLabel(SpeedSlider.Value);
		PlayerClassCombo.SelectedIndex = 0;
		InitializeComparison();
		Rescan();
	}

	/// <summary>对比控件的事件与初始项同样延后到界面就绪之后，避免 XAML 初始化期空引用。</summary>
	private void InitializeComparison()
	{
		CompareCheck.PropertyChanged += (_, e) =>
		{
			if (e.Property == CheckBox.IsCheckedProperty)
			{
				UpdateAtlasComparison();
			}
		};
		CompareSkinCombo.SelectionChanged += OnCompareSkinChanged;
		foreach (ScDefaultSkin skin in ScDefaultSkins.All)
		{
			CompareSkinCombo.Items.Add(skin.Caption);
		}
		CompareSkinCombo.SelectedIndex = 0;
	}

	/// <summary>窄屏改为「侧栏在上、标签页在下」，宽屏恢复左右两栏，避免固定侧栏把内容区挤没。</summary>
	private void ApplyResponsiveLayout(double width)
	{
		if (!m_uiReady || width <= 0)
		{
			return;
		}
		bool compact = width < CompactLayoutThreshold;
		if (compact == m_compactLayout)
		{
			return;
		}
		m_compactLayout = compact;
		if (compact)
		{
			BodyGrid.ColumnDefinitions[0].Width = new GridLength(0);
			BodyGrid.ColumnDefinitions[1].Width = GridLength.Star;
			// 侧栏与标签页都改为按内容取高，整页交给外层滚动容器，矮视口下可滑到全部内容。
			BodyGrid.RowDefinitions[0].Height = GridLength.Auto;
			BodyGrid.RowDefinitions[1].Height = GridLength.Auto;
			BodyScroll.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
			Grid.SetColumnSpan(SidePanel, 2);
			Grid.SetRow(SidePanel, 0);
			Grid.SetColumn(SidePanel, 0);
			Grid.SetColumnSpan(MainTabs, 2);
			Grid.SetRow(MainTabs, 1);
			Grid.SetColumn(MainTabs, 0);
			MainTabs.Margin = new Thickness(4, 4, 0, 0);
		}
		else
		{
			BodyGrid.ColumnDefinitions[0].Width = new GridLength(SidePanelWidth);
			BodyGrid.ColumnDefinitions[1].Width = GridLength.Star;
			BodyGrid.RowDefinitions[0].Height = GridLength.Star;
			BodyGrid.RowDefinitions[1].Height = GridLength.Auto;
			Grid.SetColumnSpan(SidePanel, 1);
			Grid.SetRow(SidePanel, 0);
			Grid.SetColumn(SidePanel, 0);
			Grid.SetColumnSpan(MainTabs, 1);
			Grid.SetRow(MainTabs, 0);
			Grid.SetColumn(MainTabs, 1);
			MainTabs.Margin = new Thickness(4, 0, 0, 0);
			// 宽屏时禁用整页滚动，保持左右两栏各自内部的滚动行为。
			BodyScroll.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
		}
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
		// 按两次回调之间的真实时长推进，界面繁忙时旋转速度不随帧率变化；
		// 卡顿或系统休眠造成的超长间隔不补帧，避免恢复时视角突跳。
		double elapsedSeconds = Math.Clamp(m_rotationClock.Elapsed.TotalSeconds, 0.0, 0.5);
		m_rotationClock.Restart();
		if (AutoRotateCheck.IsChecked != true || (m_skin == null && m_selectedDefaultAtlas == null))
		{
			return;
		}
		double degreesPerSecond = Math.Clamp(SpeedSlider.Value, MinRotationSpeed, MaxRotationSpeed);
		m_yawDegrees = (m_yawDegrees + degreesPerSecond * elapsedSeconds) % 360.0;
		m_suppressYawEvent = true;
		YawSlider.Value = m_yawDegrees;
		m_suppressYawEvent = false;
		if (m_skin != null)
		{
			RenderModel();
		}
		RenderScModel();
	}

	/// <summary>速度滑块以「度/秒」显示，范围与 MinRotationSpeed / MaxRotationSpeed 一致。</summary>
	private void UpdateSpeedLabel(double degreesPerSecond)
	{
		SpeedValue.Text = $"{degreesPerSecond:F0} 度/秒";
	}

	private void OnYawSliderChanged(double value)
	{
		if (m_suppressYawEvent)
		{
			return;
		}
		m_yawDegrees = value;
		if (m_skin != null)
		{
			RenderModel();
		}
		RenderScModel();
	}

	private void OnFileSelected(object? sender, SelectionChangedEventArgs e) => OpenSelected();

	private void OnConvertOptionChanged(object? sender, RoutedEventArgs e) => OpenSelected();

	private void OnGuidesChanged(object? sender, RoutedEventArgs e) => RefreshGuides();

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
		// 内置示例皮肤始终排在最前，空目录或 Android 沙盒下也能直接转换预览。
		foreach (McBuiltinSkin builtin in McBuiltinSkins.All)
		{
			FilesList.Items.Add(new SkinListEntry($"内置：{builtin.DisplayName}", null, builtin));
		}
		foreach (string path in Directory.GetFiles(m_inputDirectory, "*.png").OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
		{
			FilesList.Items.Add(new SkinListEntry(Path.GetFileName(path), path, null));
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

	private SkinListEntry? CurrentEntry() => FilesList.SelectedItem as SkinListEntry;

	private void OpenSelected()
	{
		SkinListEntry? entry = CurrentEntry();
		if (entry == null)
		{
			ClearPreviews();
			return;
		}
		try
		{
			McArmModel armModel = ArmSlimRadio.IsChecked == true ? McArmModel.Slim : McArmModel.Classic;
			m_skin = entry.Builtin == null
				? MinecraftSkin.Load(entry.FilePath!, armModel)
				: McBuiltinSkins.Load(entry.Builtin, armModel);
			m_atlas = MinecraftSkinConverter.Convert(m_skin, OverlayCheck.IsChecked == true);
			// 选中转换皮肤后，SC 3D 预览回到「当前转换结果」；选中内置皮肤时另由 OnDefaultSkinSelected 覆盖。
			m_selectedDefaultAtlas = null;
			UpdatePreviews();
			SetStatus($"{entry.DisplayName} | {m_skin.SourceDescription} | 手臂={(m_skin.ArmModel == McArmModel.Slim ? "细手（Alex）" : "宽手（Steve）")} | 第二层={(m_skin.HasOverlay && OverlayCheck.IsChecked == true ? "已合成" : "未合成")} | 输出={m_outputDirectory}");
		}
		catch (Exception ex)
		{
			Trace.WriteLine($"[CharacterSkinsMC] 皮肤读取失败 文件={entry.DisplayName} 类型={ex.GetType().FullName} 消息={ex.Message}\n{ex}");
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
		RefreshScAtlasPreviews();
		RenderModel();
		RenderScModel();
	}

	/// <summary>「显示 UV 分区」开关变化：MC 页用 MC 分区，SC 各页按各自对象切换 SC 分区。</summary>
	private void RefreshGuides()
	{
		if (m_skin != null)
		{
			SetImage(McPreviewImage, SkinPreview.RenderMcSkin(m_skin, 7, GuidesCheck.IsChecked == true));
		}
		RefreshScAtlasPreviews();
	}

	/// <summary>刷新所有 SC 平面图：转译结果、内置默认皮肤与三栏对比各取对应的 SC 原版分区。</summary>
	private void RefreshScAtlasPreviews()
	{
		if (m_atlas == null)
		{
			SetImage(ScPreviewImage, null);
		}
		else
		{
			SetImage(ScPreviewImage, SkinPreview.RenderAtlas(m_atlas, 7, GuidesCheck.IsChecked == true, CurrentUvLayout));
		}
		RefreshDefaultSkinPreview();
		UpdateAtlasComparison();
	}

	private void RefreshDefaultSkinPreview()
	{
		if (m_selectedDefaultAtlas == null)
		{
			SetImage(DefaultSkinPreviewImage, null);
			return;
		}
		SetImage(DefaultSkinPreviewImage,
			SkinPreview.RenderAtlas(m_selectedDefaultAtlas, 7, GuidesCheck.IsChecked == true, DefaultSkinUvLayout));
	}

	private void RenderModel()
	{
		if (m_skin == null)
		{
			return;
		}
		SetImage(ModelPreviewImage, SkinPreview.Render3D(m_skin, OverlayCheck.IsChecked == true, 460, 540, m_yawDegrees, 18));
	}

	private void OnCompareSkinChanged(object? sender, SelectionChangedEventArgs e)
	{
		if (!m_uiReady)
		{
			return;
		}
		RefreshReferenceAtlas();
		UpdateAtlasComparison();
	}

	private void RefreshReferenceAtlas()
	{
		if (CompareSkinCombo.SelectedItem is not string caption)
		{
			m_referenceSkin = null;
			m_referenceAtlas = null;
			return;
		}
		m_referenceSkin = ScDefaultSkins.All.FirstOrDefault(skin => string.Equals(skin.Caption, caption, StringComparison.Ordinal));
		try
		{
			m_referenceAtlas = m_referenceSkin == null ? null : ScDefaultSkins.LoadSkin(m_referenceSkin);
		}
		catch (Exception ex)
		{
			Trace.WriteLine($"[CharacterSkinsMC] 对照皮肤读取失败 标识={caption} 类型={ex.GetType().FullName} 消息={ex.Message}\n{ex}");
			m_referenceAtlas = null;
			SetStatus($"对照皮肤读取失败：{ex.Message}");
		}
	}

	/// <summary>SC 图集对比：转译结果、内置默认皮肤、差异高亮三栏并列，并给出差异统计。</summary>
	private void UpdateAtlasComparison()
	{
		bool enabled = CompareCheck.IsChecked == true && m_atlas != null && m_referenceAtlas != null;
		SingleAtlasView.IsVisible = !enabled;
		CompareAtlasView.IsVisible = enabled;
		if (!enabled)
		{
			SetImage(CompareCurrentImage, null);
			SetImage(CompareReferenceImage, null);
			SetImage(CompareDiffImage, null);
			CompareStats.Text = m_referenceAtlas == null ? "未选择对照皮肤。" : "尚无转译结果。";
			return;
		}
		RgbaImage current = m_atlas!;
		RgbaImage reference = m_referenceAtlas!;
		bool guides = GuidesCheck.IsChecked == true;
		ScUvLayout currentLayout = CurrentUvLayout;
		ScUvLayout referenceLayout = ReferenceUvLayout;
		try
		{
			SetImage(CompareCurrentImage, SkinPreview.RenderAtlas(current, 5, guides, currentLayout));
			SetImage(CompareReferenceImage, SkinPreview.RenderAtlas(reference, 5, guides, referenceLayout));
			ScAtlasDiff diff = ScAtlasComparison.Compare(current, reference);
			SetImage(CompareDiffImage, ScAtlasComparison.RenderDifference(current, reference, 5, guides, currentLayout));
			CompareStats.Text = $"对照 {m_referenceSkin?.Caption ?? "默认皮肤"}：不同 {diff.DifferentPixels}/{diff.TotalPixels} 像素，相似度 {diff.Similarity:P1}；UV 分区 转译结果={currentLayout.Name} / 对照={referenceLayout.Name}";
		}
		catch (Exception ex)
		{
			Trace.WriteLine($"[CharacterSkinsMC] 图集对比失败 类型={ex.GetType().FullName} 消息={ex.Message}\n{ex}");
			CompareStats.Text = $"图集对比失败：{ex.Message}";
		}
	}

	/// <summary>按当前性别填充内置默认皮肤列表，选中第一张。</summary>
	private void LoadDefaultSkins()
	{
		if (!m_uiReady)
		{
			return;
		}
		DefaultSkinList.Items.Clear();
		foreach (ScDefaultSkin skin in ScDefaultSkins.GetByClass(CurrentPlayerClass))
		{
			DefaultSkinList.Items.Add(skin.Caption);
		}
		if (DefaultSkinList.Items.Count > 0)
		{
			DefaultSkinList.SelectedIndex = 0;
		}
	}

	private void OnPlayerClassChanged(object? sender, SelectionChangedEventArgs e)
	{
		if (!m_uiReady)
		{
			return;
		}
		LoadDefaultSkins();
		RefreshScAtlasPreviews();
		RenderScModel();
	}

	private ScDefaultSkin? CurrentDefaultSkin()
	{
		if (DefaultSkinList.SelectedItem is not string caption)
		{
			return null;
		}
		return ScDefaultSkins.All.FirstOrDefault(skin => string.Equals(skin.Caption, caption, StringComparison.Ordinal));
	}

	private void OnDefaultSkinSelected(object? sender, SelectionChangedEventArgs e)
	{
		if (!m_uiReady)
		{
			return;
		}
		ScDefaultSkin? skin = CurrentDefaultSkin();
		if (skin == null)
		{
			m_selectedDefaultAtlas = null;
			RefreshDefaultSkinPreview();
			RenderScModel();
			return;
		}
		try
		{
			m_selectedDefaultAtlas = ScDefaultSkins.LoadSkin(skin);
			RefreshDefaultSkinPreview();
			RenderScModel();
			SetStatus($"内置皮肤 {skin.Caption} | SC 模型={(CurrentPlayerClass == ScPlayerClass.Male ? "男" : "女")} | UV 分区={DefaultSkinUvLayout.Name} | SC 3D 预览已切换为该皮肤 | 输出={m_outputDirectory}");
		}
		catch (Exception ex)
		{
			Trace.WriteLine($"[CharacterSkinsMC] 内置皮肤读取失败 标识={skin.Id} 类型={ex.GetType().FullName} 消息={ex.Message}\n{ex}");
			SetStatus($"内置皮肤读取失败：{ex.Message}");
			m_selectedDefaultAtlas = null;
			RefreshDefaultSkinPreview();
		}
	}

	/// <summary>SC 专属 3D 预览：优先显示选中的内置默认皮肤，否则显示当前转换结果。</summary>
	private void RenderScModel()
	{
		RgbaImage? atlas = m_selectedDefaultAtlas ?? m_atlas;
		if (atlas == null)
		{
			SetImage(ScModelPreviewImage, null);
			return;
		}
		try
		{
			ScModel model = GetModel(CurrentPlayerClass);
			SetImage(ScModelPreviewImage, ScModelPreview.Render(atlas, model, 460, 540, m_yawDegrees, 18));
		}
		catch (Exception ex)
		{
			Trace.WriteLine($"[CharacterSkinsMC] SC 模型预览失败 性别={CurrentPlayerClass} 类型={ex.GetType().FullName} 消息={ex.Message}\n{ex}");
			SetStatus($"SC 模型预览失败：{ex.Message}");
			SetImage(ScModelPreviewImage, null);
		}
	}

	private ScModel GetModel(ScPlayerClass playerClass)
	{
		if (!m_models.TryGetValue(playerClass, out ScModel? model))
		{
			model = ScDefaultSkins.LoadModel(playerClass);
			m_models[playerClass] = model;
		}
		return model;
	}

	private void OnExportDefaultSkinClick(object? sender, RoutedEventArgs e)
	{
		ScDefaultSkin? skin = CurrentDefaultSkin();
		if (skin == null)
		{
			SetStatus("请先选择一张内置默认皮肤。");
			return;
		}
		string output = Path.Combine(m_outputDirectory, $"Survivalcraft-{skin.DisplayName}.scskin");
		try
		{
			string saved = ScDefaultSkins.SaveSkin(skin, output);
			SetStatus($"已导出内置皮肤：{saved}");
		}
		catch (Exception ex)
		{
			Trace.WriteLine($"[CharacterSkinsMC] 导出内置皮肤失败 路径={output} 类型={ex.GetType().FullName} 消息={ex.Message}\n{ex}");
			SetStatus($"导出内置皮肤失败：{ex.Message}");
		}
	}

	private void ClearPreviews()
	{
		m_skin = null;
		m_atlas = null;
		SetImage(McPreviewImage, null);
		SetImage(ModelPreviewImage, null);
		// 内置默认皮肤与 SC 3D 预览不依赖输入目录，保持可见。
		RefreshScAtlasPreviews();
		RenderScModel();
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
		SkinListEntry? entry = CurrentEntry();
		if (m_atlas == null || entry == null)
		{
			SetStatus("请先在列表中选择一个皮肤。");
			return;
		}
		string output = Path.Combine(m_outputDirectory, entry.OutputName + ".scskin");
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
		var entries = FilesList.Items.OfType<SkinListEntry>().ToList();
		if (entries.Count == 0)
		{
			SetStatus("列表中没有 PNG 皮肤。");
			return;
		}
		int success = 0;
		var failures = new List<string>();
		foreach (SkinListEntry entry in entries)
		{
			try
			{
				McArmModel armModel = ArmSlimRadio.IsChecked == true ? McArmModel.Slim : McArmModel.Classic;
				MinecraftSkin skin = entry.Builtin == null
					? MinecraftSkin.Load(entry.FilePath!, armModel)
					: McBuiltinSkins.Load(entry.Builtin, armModel);
				RgbaImage atlas = MinecraftSkinConverter.Convert(skin, OverlayCheck.IsChecked == true);
				string output = Path.Combine(m_outputDirectory, entry.OutputName + ".scskin");
				MinecraftSkinConverter.SaveScSkin(atlas, output);
				success++;
			}
			catch (Exception ex)
			{
				Trace.WriteLine($"[CharacterSkinsMC] 批量转换失败 文件={entry.DisplayName} 类型={ex.GetType().FullName} 消息={ex.Message}\n{ex}");
				failures.Add($"{entry.DisplayName}：{ex.Message}");
			}
		}
		string message = $"批量转换完成 {success}/{entries.Count}，输出目录：{m_outputDirectory}";
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

	/// <summary>「关于」统一以右侧抽屉打开：桌面与 Android 同一交互，Android 上不再弹独立窗口。</summary>
	private void OnAboutClick(object? sender, RoutedEventArgs e)
	{
		AboutDrawer.IsPaneOpen = true;
	}

	private void OnCloseAboutClick(object? sender, RoutedEventArgs e)
	{
		AboutDrawer.IsPaneOpen = false;
	}

	/// <summary>项目地址本身就是链接，点击后经 TopLevel 启动器打开（桌面走系统默认浏览器，Android 走浏览器 Intent）。</summary>
	private async void OnProjectUrlPressed(object? sender, PointerPressedEventArgs e)
	{
		string? url = ProjectUrlText.Text;
		if (string.IsNullOrWhiteSpace(url))
		{
			return;
		}
		try
		{
			TopLevel? topLevel = TopLevel.GetTopLevel(this);
			bool opened = topLevel != null && await topLevel.Launcher.LaunchUriAsync(new Uri(url));
			if (!opened)
			{
				SetStatus($"无法在浏览器打开项目地址：{url}");
			}
		}
		catch (Exception ex)
		{
			Trace.WriteLine($"[CharacterSkinsMC] 打开项目主页失败 地址={url} 类型={ex.GetType().FullName} 消息={ex.Message}\n{ex}");
			SetStatus($"打开项目地址失败：{ex.Message}");
		}
	}

	private void SetStatus(string text) => StatusBar.Text = text;
}
