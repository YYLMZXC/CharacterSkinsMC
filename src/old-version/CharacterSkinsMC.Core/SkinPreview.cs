#nullable enable
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Linq;

namespace CharacterSkinsMC.Core;

/// <summary>三种预览：Minecraft 原图、转换后的 SC 图集、Minecraft 皮肤 3D 立方体预览。</summary>
public static class SkinPreview
{
	public static Bitmap RenderMcSkin(MinecraftSkin skin, int scale, bool guides)
	{
		ArgumentNullException.ThrowIfNull(skin);
		int size = MinecraftSkin.Size * Math.Max(1, scale);
		var result = new Bitmap(size, size, PixelFormat.Format32bppArgb);
		using (Graphics graphics = Graphics.FromImage(result))
		{
			DrawCheckerboard(graphics, size, Math.Max(4, scale * 8));
			using var source = skin.Base.ToBitmap();
			DrawNearest(graphics, source, new Rectangle(0, 0, size, size));
			DrawGrid(graphics, size, scale);
			if (guides)
			{
				foreach (McBodyPart part in Enum.GetValues<McBodyPart>())
				{
					foreach (McFace face in Enum.GetValues<McFace>())
					{
						DrawRegion(graphics, skin.GetFaceRect(part, face), scale, Color.FromArgb(150, 60, 200, 255));
						if (skin.HasOverlay)
						{
							DrawRegion(graphics, skin.GetFaceRect(part, face, overlay: true), scale, Color.FromArgb(150, 255, 180, 60));
						}
					}
				}
			}
		}
		return result;
	}

	public static Bitmap RenderAtlas(RgbaImage atlas, int scale, bool guides)
	{
		ArgumentNullException.ThrowIfNull(atlas);
		int size = ScSkinAtlas.Size * Math.Max(1, scale);
		var result = new Bitmap(size, size, PixelFormat.Format32bppArgb);
		using (Graphics graphics = Graphics.FromImage(result))
		{
			DrawCheckerboard(graphics, size, Math.Max(4, scale * 8));
			using var source = atlas.ToBitmap();
			DrawNearest(graphics, source, new Rectangle(0, 0, size, size));
			DrawGrid(graphics, size, scale);
			if (guides)
			{
				foreach (ScRegion region in ScSkinAtlas.Regions)
				{
					DrawRegion(graphics, region.Dest, scale, Color.FromArgb(130, 120, 255, 140));
				}
			}
		}
		return result;
	}

	/// <summary>用正交投影把 Minecraft 皮肤六个盒面贴到立方体上，支持旋转与双层外壳预览。</summary>
	public static Bitmap Render3D(MinecraftSkin skin, bool composeOverlay, int width, int height, double yawDegrees, double pitchDegrees)
	{
		ArgumentNullException.ThrowIfNull(skin);
		width = Math.Max(160, width);
		height = Math.Max(200, height);
		var result = new Bitmap(width, height, PixelFormat.Format32bppArgb);
		using (Graphics graphics = Graphics.FromImage(result))
		{
			graphics.Clear(Color.FromArgb(255, 28, 32, 40));
			graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
			graphics.PixelOffsetMode = PixelOffsetMode.Half;
			graphics.SmoothingMode = SmoothingMode.None;
			graphics.CompositingMode = CompositingMode.SourceOver;

			double scale = Math.Min(width / 22.0, height / 40.0);
			double centerX = width / 2.0;
			double centerY = height / 2.0 + 3.0 * scale;
			using var shadow = new SolidBrush(Color.FromArgb(60, 0, 0, 0));
			graphics.FillEllipse(shadow, (float)(centerX - 7 * scale), (float)(centerY + 9 * scale), (float)(14 * scale), (float)(3 * scale));

			double yaw = yawDegrees * Math.PI / 180.0;
			double pitch = pitchDegrees * Math.PI / 180.0;
			double cosYaw = Math.Cos(yaw), sinYaw = Math.Sin(yaw);
			double cosPitch = Math.Cos(pitch), sinPitch = Math.Sin(pitch);

			Cube[] cubes = BuildCubes(skin.ArmModel);
			using var baseBitmap = skin.Base.ToBitmap();
			DrawPass(graphics, baseBitmap, cubes, 1.0, cosYaw, sinYaw, cosPitch, sinPitch, scale, centerX, centerY, skin);
			if (composeOverlay && skin.HasOverlay)
			{
				using var overlayBitmap = skin.OverlayLayer.ToBitmap();
				DrawPass(graphics, overlayBitmap, cubes, 1.06, cosYaw, sinYaw, cosPitch, sinPitch, scale, centerX, centerY, skin);
			}
		}
		return result;
	}

	private readonly record struct V3(double X, double Y, double Z);

	private sealed record Cube(double X, double Y, double Z, double W, double H, double D, McBodyPart Part);

	private sealed record FaceDraw(PointF P00, PointF P10, PointF P01, Rectangle Source, double Depth, float Brightness);

	private static Cube[] BuildCubes(McArmModel armModel)
	{
		int armWidth = armModel == McArmModel.Slim ? 3 : 4;
		return new Cube[]
		{
			new(0, 20, 0, 8, 8, 8, McBodyPart.Head),
			new(0, 8, 0, 8, 12, 4, McBodyPart.Body),
			new(6, 8, 0, armWidth, 12, 4, McBodyPart.RightArm),
			new(-6, 8, 0, armWidth, 12, 4, McBodyPart.LeftArm),
			new(2, -4, 0, 4, 12, 4, McBodyPart.RightLeg),
			new(-2, -4, 0, 4, 12, 4, McBodyPart.LeftLeg)
		};
	}

	private static void DrawPass(Graphics graphics, Bitmap texture, Cube[] cubes, double factor,
		double cosYaw, double sinYaw, double cosPitch, double sinPitch, double scale, double centerX, double centerY,
		MinecraftSkin skin)
	{
		var faces = new List<FaceDraw>();
		foreach (Cube cube in cubes)
		{
			foreach (McFace face in Enum.GetValues<McFace>())
			{
				Rect rect = skin.GetFaceRect(cube.Part, face);
				V3[] corners = GetCorners(cube, face, factor);
				PointF p00 = Project(corners[0], cosYaw, sinYaw, cosPitch, sinPitch, scale, centerX, centerY);
				PointF p10 = Project(corners[1], cosYaw, sinYaw, cosPitch, sinPitch, scale, centerX, centerY);
				PointF p01 = Project(corners[2], cosYaw, sinYaw, cosPitch, sinPitch, scale, centerX, centerY);
				double depth = corners.Average(c => DepthOf(c, cosYaw, sinYaw, cosPitch, sinPitch));
				faces.Add(new FaceDraw(p00, p10, p01, new Rectangle(rect.X, rect.Y, rect.W, rect.H), depth, Brightness(face)));
			}
		}
		// 正交相机位于 +Z，depth 越小越远，先画远面。
		faces.Sort((a, b) => a.Depth.CompareTo(b.Depth));
		foreach (FaceDraw face in faces)
		{
			using var attributes = new ImageAttributes();
			float brightness = face.Brightness;
			var matrix = new ColorMatrix(new float[][]
			{
				new float[] { brightness, 0, 0, 0, 0 },
				new float[] { 0, brightness, 0, 0, 0 },
				new float[] { 0, 0, brightness, 0, 0 },
				new float[] { 0, 0, 0, 1, 0 },
				new float[] { 0, 0, 0, 0, 1 }
			});
			attributes.SetColorMatrix(matrix);
			graphics.DrawImage(texture, new[] { face.P00, face.P10, face.P01 }, face.Source, GraphicsUnit.Pixel, attributes);
		}
	}

	private static V3[] GetCorners(Cube cube, McFace face, double factor)
	{
		double hx = cube.W * factor / 2.0;
		double hy = cube.H * factor / 2.0;
		double hz = cube.D * factor / 2.0;
		double x = cube.X, y = cube.Y, z = cube.Z;
		return face switch
		{
			McFace.Top => new[]
			{
				new V3(x - hx, y + hy, z - hz), new V3(x + hx, y + hy, z - hz),
				new V3(x - hx, y + hy, z + hz), new V3(x + hx, y + hy, z + hz)
			},
			McFace.Bottom => new[]
			{
				new V3(x - hx, y - hy, z + hz), new V3(x + hx, y - hy, z + hz),
				new V3(x - hx, y - hy, z - hz), new V3(x + hx, y - hy, z - hz)
			},
			McFace.Front => new[]
			{
				new V3(x - hx, y + hy, z + hz), new V3(x + hx, y + hy, z + hz),
				new V3(x - hx, y - hy, z + hz), new V3(x + hx, y - hy, z + hz)
			},
			McFace.Back => new[]
			{
				new V3(x + hx, y + hy, z - hz), new V3(x - hx, y + hy, z - hz),
				new V3(x + hx, y - hy, z - hz), new V3(x - hx, y - hy, z - hz)
			},
			McFace.Right => new[]
			{
				new V3(x + hx, y + hy, z + hz), new V3(x + hx, y + hy, z - hz),
				new V3(x + hx, y - hy, z + hz), new V3(x + hx, y - hy, z - hz)
			},
			McFace.Left => new[]
			{
				new V3(x - hx, y + hy, z - hz), new V3(x - hx, y + hy, z + hz),
				new V3(x - hx, y - hy, z - hz), new V3(x - hx, y - hy, z + hz)
			},
			_ => throw new ArgumentOutOfRangeException(nameof(face))
		};
	}

	private static PointF Project(V3 point, double cosYaw, double sinYaw, double cosPitch, double sinPitch,
		double scale, double centerX, double centerY)
	{
		double x1 = point.X * cosYaw + point.Z * sinYaw;
		double z1 = -point.X * sinYaw + point.Z * cosYaw;
		double y1 = point.Y * cosPitch - z1 * sinPitch;
		return new PointF((float)(centerX + x1 * scale), (float)(centerY - y1 * scale));
	}

	private static double DepthOf(V3 point, double cosYaw, double sinYaw, double cosPitch, double sinPitch)
	{
		double z1 = -point.X * sinYaw + point.Z * cosYaw;
		return point.Y * sinPitch + z1 * cosPitch;
	}

	private static float Brightness(McFace face) => face switch
	{
		McFace.Top => 1.0f,
		McFace.Front => 0.92f,
		McFace.Right => 0.86f,
		McFace.Left => 0.82f,
		McFace.Back => 0.76f,
		McFace.Bottom => 0.70f,
		_ => 1.0f
	};

	private static void DrawCheckerboard(Graphics graphics, int size, int tile)
	{
		using var light = new SolidBrush(Color.FromArgb(255, 205, 208, 214));
		using var dark = new SolidBrush(Color.FromArgb(255, 170, 174, 182));
		for (int y = 0; y < size; y += tile)
		{
			for (int x = 0; x < size; x += tile)
			{
				graphics.FillRectangle(((x / tile + y / tile) & 1) == 0 ? light : dark, x, y, tile, tile);
			}
		}
	}

	private static void DrawNearest(Graphics graphics, Bitmap source, Rectangle destination)
	{
		graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
		graphics.PixelOffsetMode = PixelOffsetMode.Half;
		graphics.DrawImage(source, destination, new Rectangle(0, 0, source.Width, source.Height), GraphicsUnit.Pixel);
	}

	private static void DrawGrid(Graphics graphics, int size, int scale)
	{
		int step = 8 * Math.Max(1, scale);
		using var pen = new Pen(Color.FromArgb(70, 40, 44, 52));
		for (int position = 0; position <= size; position += step)
		{
			graphics.DrawLine(pen, position, 0, position, size);
			graphics.DrawLine(pen, 0, position, size, position);
		}
	}

	private static void DrawRegion(Graphics graphics, Rect rect, int scale, Color color)
	{
		using var pen = new Pen(color);
		graphics.DrawRectangle(pen, rect.X * scale, rect.Y * scale, rect.W * scale, rect.H * scale);
	}
}
