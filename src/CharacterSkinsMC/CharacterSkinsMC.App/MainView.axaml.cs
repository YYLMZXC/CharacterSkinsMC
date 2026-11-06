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
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CharacterSkinsMC.App.Infrastructure;
using CharacterSkinsMC.App.Presentation;
using CharacterSkinsMC.App.Services;
using CharacterSkinsMC.Core;
// 旋转 / 布局 / 预览尺寸常量统一定义在 PreviewLayout，视图内直接引用。
using static CharacterSkinsMC.App.Presentation.PreviewLayout;

namespace CharacterSkinsMC.App;

/// <summary>
/// 主界面只负责控件交互与预览状态：皮肤来源、转换、持久化由 SkinWorkspace 编排，
/// 平台能力由 IAppEnvironment 提供，像素图到位图的转换由 PreviewImages 承担。
/// </summary>
public partial class MainView : UserControl
{
	private readonly IAppEnvironment m_environment = new AppEnvironment();
	private readonly SkinWorkspace m_workspace;
	private MinecraftSkin? m_skin;
	private RgbaImage? m_atlas;
	private RgbaImage? m_selectedDefaultAtlas;
	private RgbaImage? m_referenceAtlas;
	private ScDefaultSkin? m_referenceSkin;
	private double m_yawDegrees = 35;
	private double m_pitchDegrees = 18;
	private bool m_suppressYawEvent;
	private bool m_uiReady;
	private bool m_compactLayout;
	// 3D 预览拖拽旋转状态：按住预览图拖动即旋转（yaw 全向 360°，pitch 上下限位）。
	private bool m_isDragging;
	private Point m_dragLastPoint;
	private readonly Dictionary<ScPlayerClass, ScModel> m_models = new();
	private readonly DispatcherTimer m_timer;
	private readonly Stopwatch m_rotationClock = Stopwatch.StartNew();

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
		m_workspace = new SkinWorkspace(
			new SkinFileRepository(new AppPaths(m_environment)),
			new SkinConversionService());

		m_timer = new DispatcherTimer(TimeSpan.FromMilliseconds(80), DispatcherPriority.Background, OnTimerTick);
		m_timer.Start();

		if (!m_environment.CanOpenDirectory)
		{
			OpenInputButton.IsVisible = false;
		}
		FilesList.AddHandler(DragDrop.DragOverEvent, OnListDragOver);
		FilesList.AddHandler(DragDrop.DropEvent, OnListDrop);
		HookPreviewDragRotate(ModelPreviewImage);
		HookPreviewDragRotate(ScModelPreviewImage);
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
		// 透明填充只影响两条 3D 预览通道，切换后原地重绘即可。
		FillTransparentCheck.PropertyChanged += (_, e) =>
		{
			if (e.Property == CheckBox.IsCheckedProperty && m_uiReady)
			{
				RenderModel();
				RenderScModel();
			}
		};
		// SC 鞘翅补齐改变转译产物本身，须走 OpenSelected 重新转换并刷新全部预览。
		ElytraFillCheck.PropertyChanged += (_, e) =>
		{
			if (e.Property == CheckBox.IsCheckedProperty && m_uiReady)
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
		AboutVersionText.Text = $"版本 {AppVersion.Full}（按构建时间自动生成）";
		ProjectLinksList.ItemsSource = ProjectLinks.All;
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

	private void OnTimerTick(object? sender, EventArgs e)
	{
		// 按两次回调之间的真实时长推进，界面繁忙时旋转速度不随帧率变化；
		// 卡顿或系统休眠造成的超长间隔不补帧，避免恢复时视角突跳。
		double elapsedSeconds = Math.Clamp(m_rotationClock.Elapsed.TotalSeconds, 0.0, 0.5);
		m_rotationClock.Restart();
		if (m_isDragging || AutoRotateCheck.IsChecked != true || (m_skin == null && m_selectedDefaultAtlas == null))
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

	/// <summary>为 3D 预览图挂接拖拽旋转（Pointer 捕获，鼠标与触屏通用）。</summary>
	private void HookPreviewDragRotate(Image previewImage)
	{
		previewImage.Cursor = new Avalonia.Input.Cursor(StandardCursorType.SizeAll);
		previewImage.PointerPressed += OnPreviewPointerPressed;
		previewImage.PointerMoved += OnPreviewPointerMoved;
		previewImage.PointerReleased += OnPreviewPointerReleased;
		previewImage.PointerCaptureLost += OnPreviewPointerCaptureLost;
	}

	private void OnPreviewPointerPressed(object? sender, PointerPressedEventArgs e)
	{
		PointerPoint point = e.GetCurrentPoint((Visual)sender!);
		if (!point.Properties.IsLeftButtonPressed)
		{
			return;
		}
		m_isDragging = true;
		m_dragLastPoint = point.Position;
		e.Pointer.Capture((IInputElement)sender!);
	}

	private void OnPreviewPointerMoved(object? sender, PointerEventArgs e)
	{
		if (!m_isDragging)
		{
			return;
		}
		Point position = e.GetPosition((Visual)sender!);
		double deltaX = position.X - m_dragLastPoint.X;
		double deltaY = position.Y - m_dragLastPoint.Y;
		m_dragLastPoint = position;
		// 横向拖动旋转 yaw（360° 全向），纵向拖动调整俯仰并限位防止模型翻转过头。
		RotatePreview(deltaX * DragYawDegreesPerPixel, -deltaY * DragPitchDegreesPerPixel);
	}

	private void OnPreviewPointerReleased(object? sender, PointerReleasedEventArgs e)
	{
		m_isDragging = false;
		e.Pointer.Capture(null);
	}

	private void OnPreviewPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
	{
		m_isDragging = false;
	}

	/// <summary>按拖拽增量更新视角并同步 yaw 滑块，随后重绘两路 3D 预览。</summary>
	private void RotatePreview(double yawDelta, double pitchDelta)
	{
		m_yawDegrees = (m_yawDegrees + yawDelta) % 360.0;
		if (m_yawDegrees < 0)
		{
			m_yawDegrees += 360.0;
		}
		m_pitchDegrees = Math.Clamp(m_pitchDegrees + pitchDelta, MinPitchDegrees, MaxPitchDegrees);
		m_suppressYawEvent = true;
		YawSlider.Value = m_yawDegrees;
		m_suppressYawEvent = false;
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
			SetStatus(AppLog.Failure("打开皮肤失败", "打开皮肤失败", ex));
		}
	}

	private async Task ImportStorageFileAsync(IStorageFile file)
	{
		await using Stream source = await file.OpenReadAsync();
		await m_workspace.ImportAsync(file.Name, source, file.TryGetLocalPath());
	}

	private void OnRescanClick(object? sender, RoutedEventArgs e) => Rescan();

	private void Rescan()
	{
		FilesList.Items.Clear();
		foreach (SkinSource source in m_workspace.GetSources())
		{
			FilesList.Items.Add(source);
		}
		if (FilesList.Items.Count > 0)
		{
			FilesList.SelectedIndex = 0;
		}
		else
		{
			ClearPreviews();
			SetStatus($"输入目录为空：{m_workspace.InputDirectory}，请把 Minecraft 的 skin.png 放进去或拖入列表。");
		}
	}

	private SkinSource? CurrentEntry() => FilesList.SelectedItem as SkinSource;

	/// <summary>界面选项 → 服务层参数，业务逻辑不直接读控件。</summary>
	private ConversionOptions CurrentOptions() => new(
		ArmSlimRadio.IsChecked == true ? McArmModel.Slim : McArmModel.Classic,
		OverlayCheck.IsChecked == true,
		ElytraFillCheck.IsChecked == true);

	private void OpenSelected()
	{
		SkinSource? entry = CurrentEntry();
		if (entry == null)
		{
			ClearPreviews();
			return;
		}
		try
		{
			ConversionOptions options = CurrentOptions();
			SkinConversionResult result = m_workspace.Open(entry, options);
			m_skin = result.Skin;
			m_atlas = result.Atlas;
			// 选中转换皮肤后，SC 3D 预览回到「当前转换结果」；选中内置皮肤时另由 OnDefaultSkinSelected 覆盖。
			m_selectedDefaultAtlas = null;
			UpdatePreviews();
			SetStatus($"{entry.DisplayName} | {m_skin.SourceDescription} | 手臂={(m_skin.ArmModel == McArmModel.Slim ? "细手（Alex）" : "宽手（Steve）")} | 第二层={(m_skin.HasOverlay && options.ComposeOverlay ? "已合成" : "未合成")} | 鞘翅补齐={(options.FillElytra ? "开" : "关")} | 输出={m_workspace.OutputDirectory}");
		}
		catch (Exception ex)
		{
			SetStatus(AppLog.Failure("读取失败", $"皮肤读取失败 文件={entry.DisplayName}", ex));
			ClearPreviews();
		}
	}

	private void UpdatePreviews()
	{
		if (m_skin == null || m_atlas == null)
		{
			return;
		}
		PreviewImages.RenderMcSkin(McPreviewImage, m_skin, AtlasScale, GuidesCheck.IsChecked == true);
		RefreshScAtlasPreviews();
		RenderModel();
		RenderScModel();
	}

	/// <summary>「显示 UV 分区」开关变化：MC 页用 MC 分区，SC 各页按各自对象切换 SC 分区。</summary>
	private void RefreshGuides()
	{
		if (m_skin != null)
		{
			PreviewImages.RenderMcSkin(McPreviewImage, m_skin, AtlasScale, GuidesCheck.IsChecked == true);
		}
		RefreshScAtlasPreviews();
	}

	/// <summary>刷新所有 SC 平面图：转译结果、内置默认皮肤与三栏对比各取对应的 SC 原版分区。</summary>
	private void RefreshScAtlasPreviews()
	{
		PreviewImages.RenderAtlas(ScPreviewImage, m_atlas, AtlasScale,
			GuidesCheck.IsChecked == true, CurrentUvLayout);
		RefreshDefaultSkinPreview();
		UpdateAtlasComparison();
	}

	private void RefreshDefaultSkinPreview() =>
		PreviewImages.RenderAtlas(DefaultSkinPreviewImage, m_selectedDefaultAtlas, AtlasScale,
			GuidesCheck.IsChecked == true, DefaultSkinUvLayout);

	private void RenderModel()
	{
		if (m_skin == null)
		{
			return;
		}
		PreviewImages.RenderMcModel(ModelPreviewImage, m_skin, OverlayCheck.IsChecked == true,
			ModelWidth, ModelHeight, m_yawDegrees, m_pitchDegrees, FillTransparentCheck.IsChecked == true);
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
			m_referenceAtlas = null;
			SetStatus(AppLog.Failure("对照皮肤读取失败", $"对照皮肤读取失败 标识={caption}", ex));
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
			PreviewImages.RenderAtlas(CompareCurrentImage, current, CompareScale, guides, currentLayout);
			PreviewImages.RenderAtlas(CompareReferenceImage, reference, CompareScale, guides, referenceLayout);
			ScAtlasDiff diff = ScAtlasComparison.Compare(current, reference);
			PreviewImages.RenderDifference(CompareDiffImage, current, reference, CompareScale, guides, currentLayout);
			CompareStats.Text = $"对照 {m_referenceSkin?.Caption ?? "默认皮肤"}：不同 {diff.DifferentPixels}/{diff.TotalPixels} 像素，相似度 {diff.Similarity:P1}；UV 分区 转译结果={currentLayout.Name} / 对照={referenceLayout.Name}";
		}
		catch (Exception ex)
		{
			CompareStats.Text = AppLog.Failure("图集对比失败", "图集对比失败", ex);
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
			SetStatus($"内置皮肤 {skin.Caption} | SC 模型={(CurrentPlayerClass == ScPlayerClass.Male ? "男" : "女")} | UV 分区={DefaultSkinUvLayout.Name} | SC 3D 预览已切换为该皮肤 | 输出={m_workspace.OutputDirectory}");
		}
		catch (Exception ex)
		{
			SetStatus(AppLog.Failure("内置皮肤读取失败", $"内置皮肤读取失败 标识={skin.Id}", ex));
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
			PreviewImages.RenderScModel(ScModelPreviewImage, atlas, GetModel(CurrentPlayerClass),
				ModelWidth, ModelHeight, m_yawDegrees, m_pitchDegrees, FillTransparentCheck.IsChecked == true);
		}
		catch (Exception ex)
		{
			SetStatus(AppLog.Failure("SC 模型预览失败", $"SC 模型预览失败 性别={CurrentPlayerClass}", ex));
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
		try
		{
			SetStatus($"已导出内置皮肤：{m_workspace.ExportDefault(skin)}");
		}
		catch (Exception ex)
		{
			SetStatus(AppLog.Failure("导出内置皮肤失败", $"导出内置皮肤失败 标识={skin.Id}", ex));
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

	/// <summary>位图转换与旧图释放由 PreviewImages 统一负责。</summary>
	private static void SetImage(Image target, RgbaImage? source) => PreviewImages.Set(target, source);

	private void OnSaveCurrentClick(object? sender, RoutedEventArgs e) => SaveCurrent();

	private void SaveCurrent()
	{
		SkinSource? entry = CurrentEntry();
		if (m_atlas == null || entry == null)
		{
			SetStatus("请先在列表中选择一个皮肤。");
			return;
		}
		try
		{
			SetStatus($"已保存：{m_workspace.Save(entry, m_atlas)}");
		}
		catch (Exception ex)
		{
			SetStatus(AppLog.Failure("保存失败", $"保存失败 文件={entry.DisplayName}", ex));
		}
	}

	private void OnBatchConvertClick(object? sender, RoutedEventArgs e) => BatchConvert();

	private void BatchConvert()
	{
		var entries = FilesList.Items.OfType<SkinSource>().ToList();
		if (entries.Count == 0)
		{
			SetStatus("列表中没有 PNG 皮肤。");
			return;
		}
		BatchConversionReport report = m_workspace.SaveAll(entries, CurrentOptions());
		string message = $"批量转换完成 {report.Succeeded}/{report.Total}，输出目录：{m_workspace.OutputDirectory}";
		if (report.Failures.Count > 0)
		{
			message += "；失败：" + string.Join("；", report.Failures.Take(6));
		}
		SetStatus(message);
	}

	private void OnOpenInputDirectoryClick(object? sender, RoutedEventArgs e)
	{
		if (!m_environment.CanOpenDirectory)
		{
			SetStatus($"当前平台不支持打开资源管理器，输入目录：{m_workspace.InputDirectory}");
			return;
		}
		try
		{
			m_environment.OpenDirectory(m_workspace.InputDirectory);
		}
		catch (Exception ex)
		{
			SetStatus(AppLog.Failure("打开目录失败", $"打开目录失败 路径={m_workspace.InputDirectory}", ex));
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
				SetStatus($"当前平台无法选择自定义目录，输出目录仍为：{m_workspace.OutputDirectory}");
				return;
			}
			m_workspace.SetOutputDirectory(path!);
			SetStatus($"输出目录已设为：{m_workspace.OutputDirectory}");
		}
		catch (Exception ex)
		{
			SetStatus(AppLog.Failure("选择输出目录失败", "选择输出目录失败", ex));
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

	/// <summary>平台卡片整块可点，链接由共享清单与启动器统一处理。</summary>
	private async void OnProjectUrlPressed(object? sender, PointerPressedEventArgs e)
	{
		if ((sender as Border)?.DataContext is not ProjectLink link)
		{
			return;
		}
		bool opened = await ProjectLinkLauncher.OpenAsync(this, link.Url);
		if (!opened)
		{
			SetStatus($"无法在浏览器打开 {link.Platform}：{link.Url}");
		}
	}

	private void SetStatus(string text) => StatusBar.Text = text;
}
