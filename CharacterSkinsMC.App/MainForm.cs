#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using CharacterSkinsMC.Core;

namespace CharacterSkinsMC.App;

public sealed class MainForm : Form
{
	private readonly ListBox m_files = new();
	private readonly BufferedPictureBox m_mcPreview = new();
	private readonly BufferedPictureBox m_scPreview = new();
	private readonly BufferedPictureBox m_modelPreview = new();
	private readonly CheckBox m_slim = new();
	private readonly CheckBox m_overlay = new();
	private readonly CheckBox m_guides = new();
	private readonly CheckBox m_autoRotate = new();
	private readonly TrackBar m_yaw = new();
	private readonly ToolStripStatusLabel m_status = new() { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
	private readonly System.Windows.Forms.Timer m_timer = new();
	private readonly string m_inputDirectory;
	private string m_outputDirectory;
	private MinecraftSkin? m_skin;
	private RgbaImage? m_atlas;
	private double m_yawDegrees = 35;
	private bool m_suppressYawEvent;

	public MainForm()
	{
		m_inputDirectory = Path.Combine(AppContext.BaseDirectory, "CharacterSkinsMC");
		m_outputDirectory = Path.Combine(AppContext.BaseDirectory, "CharacterSkins");
		Directory.CreateDirectory(m_inputDirectory);
		Directory.CreateDirectory(m_outputDirectory);

		Text = "Minecraft 皮肤 → Survivalcraft 皮肤转换工具";
		StartPosition = FormStartPosition.CenterScreen;
		Size = new Size(1240, 780);
		MinimumSize = new Size(960, 640);
		AutoScaleMode = AutoScaleMode.Dpi;

		var root = new Panel { Dock = DockStyle.Fill };
		var split = new SplitContainer { Dock = DockStyle.Fill, SplitterDistance = 290, FixedPanel = FixedPanel.Panel1 };
		root.Controls.Add(split);
		root.Controls.Add(BuildToolBar());
		root.Controls.Add(BuildStatusBar());
		Controls.Add(root);

		split.Panel1.Controls.Add(BuildLeftPanel());
		split.Panel2.Controls.Add(BuildPreviewTabs());

		WireEvents();
		m_timer.Interval = 60;
		m_timer.Tick += OnTimerTick;
		m_timer.Start();
		Rescan();
	}

	private ToolStrip BuildToolBar()
	{
		var toolbar = new ToolStrip { Dock = DockStyle.Top, GripStyle = ToolStripGripStyle.Hidden, Padding = new Padding(4) };
		var open = new ToolStripButton("打开皮肤") { DisplayStyle = ToolStripItemDisplayStyle.Text };
		var save = new ToolStripButton("保存当前 .scskin") { DisplayStyle = ToolStripItemDisplayStyle.Text };
		var batch = new ToolStripButton("批量转换") { DisplayStyle = ToolStripItemDisplayStyle.Text };
		var refresh = new ToolStripButton("刷新输入目录") { DisplayStyle = ToolStripItemDisplayStyle.Text };
		var openInput = new ToolStripButton("打开输入目录") { DisplayStyle = ToolStripItemDisplayStyle.Text };
		var chooseOutput = new ToolStripButton("选择输出目录") { DisplayStyle = ToolStripItemDisplayStyle.Text };
		open.Click += (_, _) => OpenFiles();
		save.Click += (_, _) => SaveCurrent();
		batch.Click += (_, _) => BatchConvert();
		refresh.Click += (_, _) => Rescan();
		openInput.Click += (_, _) => OpenInputDirectory();
		chooseOutput.Click += (_, _) => ChooseOutputDirectory();
		toolbar.Items.AddRange(new ToolStripItem[]
		{
			open, save, batch, new ToolStripSeparator(), refresh, openInput, chooseOutput
		});
		return toolbar;
	}

	private StatusStrip BuildStatusBar()
	{
		var status = new StatusStrip { Dock = DockStyle.Bottom };
		status.Items.Add(m_status);
		return status;
	}

	private Control BuildLeftPanel()
	{
		var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(6) };
		m_files.Dock = DockStyle.Fill;
		m_files.AllowDrop = true;
		m_files.IntegralHeight = false;
		panel.Controls.Add(m_files);
		panel.Controls.Add(BuildOptions());
		var hint = new Label
		{
			Dock = DockStyle.Top,
			Height = 42,
			Text = "输入目录：CharacterSkinsMC（自动创建）\n也可把 PNG 直接拖入下方列表",
			ForeColor = Color.FromArgb(255, 80, 88, 100)
		};
		panel.Controls.Add(hint);
		return panel;
	}

	private Control BuildOptions()
	{
		var group = new GroupBox { Dock = DockStyle.Bottom, Height = 180, Text = "转换选项" };
		m_slim.Text = "Slim 手臂（3px）";
		m_slim.AutoSize = true;
		m_slim.Location = new Point(12, 24);
		m_overlay.Text = "合成第二层（帽子/外套）";
		m_overlay.AutoSize = true;
		m_overlay.Checked = true;
		m_overlay.Location = new Point(12, 50);
		m_guides.Text = "显示 UV 分区";
		m_guides.AutoSize = true;
		m_guides.Checked = true;
		m_guides.Location = new Point(12, 76);
		m_autoRotate.Text = "自动旋转 3D 预览";
		m_autoRotate.AutoSize = true;
		m_autoRotate.Location = new Point(12, 102);
		var yawLabel = new Label { Text = "视角", AutoSize = true, Location = new Point(12, 134) };
		m_yaw.Minimum = 0;
		m_yaw.Maximum = 360;
		m_yaw.TickFrequency = 45;
		m_yaw.Value = 35;
		m_yaw.Width = 190;
		m_yaw.Location = new Point(56, 126);
		group.Controls.AddRange(new Control[] { m_slim, m_overlay, m_guides, m_autoRotate, yawLabel, m_yaw });
		return group;
	}

	private Control BuildPreviewTabs()
	{
		var tabs = new TabControl { Dock = DockStyle.Fill };
		tabs.TabPages.Add(BuildPage("MC 皮肤", m_mcPreview));
		tabs.TabPages.Add(BuildPage("SC 图集", m_scPreview));
		tabs.TabPages.Add(BuildPage("3D 预览", m_modelPreview));
		return tabs;
	}

	private static TabPage BuildPage(string title, PictureBox preview)
	{
		var page = new TabPage(title);
		var container = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Color.White };
		preview.SizeMode = PictureBoxSizeMode.AutoSize;
		preview.Location = new Point(8, 8);
		container.Controls.Add(preview);
		page.Controls.Add(container);
		return page;
	}

	private void WireEvents()
	{
		m_files.SelectedIndexChanged += (_, _) => OpenSelected();
		m_files.DragEnter += OnDragEnter;
		m_files.DragDrop += OnDragDrop;
		m_slim.CheckedChanged += (_, _) => OpenSelected();
		m_overlay.CheckedChanged += (_, _) => OpenSelected();
		m_guides.CheckedChanged += (_, _) => UpdatePreviews();
		m_yaw.ValueChanged += (_, _) =>
		{
			if (m_suppressYawEvent)
			{
				return;
			}
			m_yawDegrees = m_yaw.Value;
			RenderModel();
		};
	}

	private void OnTimerTick(object? sender, EventArgs e)
	{
		if (!m_autoRotate.Checked || m_skin == null)
		{
			return;
		}
		m_yawDegrees = (m_yawDegrees + 2.5) % 360.0;
		m_suppressYawEvent = true;
		m_yaw.Value = (int)m_yawDegrees;
		m_suppressYawEvent = false;
		RenderModel();
	}

	private void OnDragEnter(object? sender, DragEventArgs e)
	{
		if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true)
		{
			e.Effect = DragDropEffects.Copy;
		}
	}

	private void OnDragDrop(object? sender, DragEventArgs e)
	{
		if (e.Data?.GetData(DataFormats.FileDrop) is string[] paths)
		{
			AddFiles(paths);
		}
	}

	private void OpenFiles()
	{
		using var dialog = new OpenFileDialog
		{
			Filter = "PNG 图像|*.png|所有文件|*.*",
			Multiselect = true,
			Title = "选择 Minecraft 皮肤"
		};
		if (dialog.ShowDialog(this) == DialogResult.OK)
		{
			AddFiles(dialog.FileNames);
		}
	}

	private void AddFiles(IEnumerable<string> paths)
	{
		int added = 0;
		foreach (string path in paths)
		{
			if (!File.Exists(path))
			{
				continue;
			}
			string extension = Path.GetExtension(path).ToLowerInvariant();
			if (extension != ".png")
			{
				continue;
			}
			string target = Path.Combine(m_inputDirectory, Path.GetFileName(path));
			if (!string.Equals(Path.GetFullPath(target), Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase))
			{
				try
				{
					File.Copy(path, target, overwrite: true);
				}
				catch (Exception ex)
				{
					Trace.WriteLine($"[CharacterSkinsMC] 复制皮肤失败 源={path} 目标={target} 类型={ex.GetType().FullName} 消息={ex.Message}");
					continue;
				}
			}
			if (!m_files.Items.Contains(target))
			{
				m_files.Items.Add(target);
				added++;
			}
		}
		if (added > 0)
		{
			m_files.SelectedIndex = m_files.Items.Count - added;
			SetStatus($"已加入 {added} 个皮肤，输入目录：{m_inputDirectory}");
		}
	}

	private void Rescan()
	{
		m_files.Items.Clear();
		foreach (string path in Directory.GetFiles(m_inputDirectory, "*.png").OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
		{
			m_files.Items.Add(path);
		}
		if (m_files.Items.Count > 0)
		{
			m_files.SelectedIndex = 0;
		}
		else
		{
			ClearPreviews();
			SetStatus($"输入目录为空：{m_inputDirectory}，请把 Minecraft 的 skin.png 放进去或拖入列表。");
		}
	}

	private void OpenSelected()
	{
		if (m_files.SelectedItem is not string path)
		{
			ClearPreviews();
			return;
		}
		try
		{
			McArmModel armModel = m_slim.Checked ? McArmModel.Slim : McArmModel.Classic;
			m_skin = MinecraftSkin.Load(path, armModel);
			m_atlas = MinecraftSkinConverter.Convert(m_skin, m_overlay.Checked);
			UpdatePreviews();
			SetStatus($"{Path.GetFileName(path)} | {m_skin.SourceDescription} | 手臂={(m_skin.ArmModel == McArmModel.Slim ? "Slim" : "Classic")} | 第二层={(m_skin.HasOverlay && m_overlay.Checked ? "已合成" : "未合成")} | 输出={m_outputDirectory}");
		}
		catch (Exception ex)
		{
			Trace.WriteLine($"[CharacterSkinsMC] 皮肤读取失败 文件={path} 类型={ex.GetType().FullName} 消息={ex.Message}\n{ex}");
			MessageBox.Show(this, ex.Message, "皮肤读取失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
		ReplaceImage(m_mcPreview, SkinPreview.RenderMcSkin(m_skin, 7, m_guides.Checked));
		ReplaceImage(m_scPreview, SkinPreview.RenderAtlas(m_atlas, 7, m_guides.Checked));
		RenderModel();
	}

	private void RenderModel()
	{
		if (m_skin == null)
		{
			return;
		}
		ReplaceImage(m_modelPreview, SkinPreview.Render3D(m_skin, m_overlay.Checked, 460, 540, m_yawDegrees, 18));
	}

	private void ClearPreviews()
	{
		m_skin = null;
		m_atlas = null;
		ReplaceImage(m_mcPreview, null);
		ReplaceImage(m_scPreview, null);
		ReplaceImage(m_modelPreview, null);
	}

	private static void ReplaceImage(PictureBox box, Bitmap? image)
	{
		Image? previous = box.Image;
		box.Image = image;
		previous?.Dispose();
	}

	private void SaveCurrent()
	{
		if (m_atlas == null || m_files.SelectedItem is not string path)
		{
			MessageBox.Show(this, "请先在列表中选择一个皮肤。", "无法保存", MessageBoxButtons.OK, MessageBoxIcon.Information);
			return;
		}
		string output = Path.Combine(m_outputDirectory, Path.GetFileNameWithoutExtension(path) + ".scskin");
		try
		{
			MinecraftSkinConverter.SaveScSkin(m_atlas, output);
			SetStatus($"已保存：{output}");
		}
		catch (Exception ex)
		{
			Trace.WriteLine($"[CharacterSkinsMC] 保存失败 路径={output} 类型={ex.GetType().FullName} 消息={ex.Message}\n{ex}");
			MessageBox.Show(this, ex.Message, "保存失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
		}
	}

	private void BatchConvert()
	{
		if (m_files.Items.Count == 0)
		{
			MessageBox.Show(this, "输入目录中没有 PNG 皮肤。", "无法批量转换", MessageBoxButtons.OK, MessageBoxIcon.Information);
			return;
		}
		int success = 0;
		var failures = new List<string>();
		foreach (string path in m_files.Items)
		{
			try
			{
				McArmModel armModel = m_slim.Checked ? McArmModel.Slim : McArmModel.Classic;
				MinecraftSkin skin = MinecraftSkin.Load(path, armModel);
				RgbaImage atlas = MinecraftSkinConverter.Convert(skin, m_overlay.Checked);
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
		string message = $"完成 {success}/{m_files.Items.Count} 个。\n输出目录：{m_outputDirectory}";
		if (failures.Count > 0)
		{
			message += "\n\n失败项：\n" + string.Join("\n", failures.Take(6));
		}
		MessageBox.Show(this, message, "批量转换结果", MessageBoxButtons.OK,
			failures.Count > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
		SetStatus($"批量转换完成 {success}/{m_files.Items.Count}，输出目录：{m_outputDirectory}");
	}

	private void OpenInputDirectory()
	{
		try
		{
			Process.Start(new ProcessStartInfo("explorer.exe", m_inputDirectory) { UseShellExecute = true });
		}
		catch (Exception ex)
		{
			Trace.WriteLine($"[CharacterSkinsMC] 打开目录失败 路径={m_inputDirectory} 类型={ex.GetType().FullName} 消息={ex.Message}");
			MessageBox.Show(this, ex.Message, "打开目录失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
		}
	}

	private void ChooseOutputDirectory()
	{
		using var dialog = new FolderBrowserDialog { SelectedPath = m_outputDirectory, Description = "选择 .scskin 输出目录" };
		if (dialog.ShowDialog(this) == DialogResult.OK)
		{
			m_outputDirectory = dialog.SelectedPath;
			Directory.CreateDirectory(m_outputDirectory);
			SetStatus($"输出目录已设为：{m_outputDirectory}");
		}
	}

	private void SetStatus(string text) => m_status.Text = text;

	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			m_timer.Stop();
			m_timer.Dispose();
			m_mcPreview.Image?.Dispose();
			m_scPreview.Image?.Dispose();
			m_modelPreview.Image?.Dispose();
		}
		base.Dispose(disposing);
	}

	private sealed class BufferedPictureBox : PictureBox
	{
		public BufferedPictureBox()
		{
			SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
			DoubleBuffered = true;
		}
	}
}
