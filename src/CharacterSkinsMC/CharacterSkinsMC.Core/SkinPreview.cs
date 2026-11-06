#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace CharacterSkinsMC.Core;

/// <summary>
/// 三种预览：Minecraft 原图、转换后的 SC 图集、Minecraft 皮肤 3D 立方体预览。
/// 原实现依赖 System.Drawing（Android 不可用），现改为纯托管软件光栅化，统一输出 RgbaImage。
/// 平面图的两套参考线互不共用：MC 皮肤页取 McUvLayouts，SC 图集页取调用方传入的 ScUvLayout。
/// </summary>
public static class SkinPreview
{
	public static RgbaImage RenderMcSkin(MinecraftSkin skin, int scale, bool guides)
	{
		ArgumentNullException.ThrowIfNull(skin);
		int size = MinecraftSkin.Size * Math.Max(1, scale);
		var result = new RgbaImage(size, size);
		DrawCheckerboard(result, size, Math.Max(4, scale * 8));
		BlitNearest(result, skin.Base, new Rect(0, 0, size, size));
		DrawGrid(result, size, scale);
		if (guides)
		{
			foreach (McUvRegion region in McUvLayouts.Create(skin).Regions)
			{
				RgbaColor color = region.IsOverlay
					? new RgbaColor(255, 180, 60, 150)
					: new RgbaColor(60, 200, 255, 150);
				DrawRegionOutline(result, region.Rect, scale, color);
			}
		}
		return result;
	}

	/// <summary>
	/// 渲染 SC 图集平面图。layout 必须由调用方按预览对象（转译结果 / 男默认皮肤 / 女默认皮肤）选定，
	/// 不设默认值，防止把 Minecraft 分区或转译岛矩形误套到 SC 原版图集上。
	/// </summary>
	public static RgbaImage RenderAtlas(RgbaImage atlas, int scale, bool guides, ScUvLayout layout)
	{
		ArgumentNullException.ThrowIfNull(atlas);
		ArgumentNullException.ThrowIfNull(layout);
		int size = ScSkinAtlas.Size * Math.Max(1, scale);
		var result = new RgbaImage(size, size);
		DrawCheckerboard(result, size, Math.Max(4, scale * 8));
		BlitNearest(result, atlas, new Rect(0, 0, size, size));
		DrawGrid(result, size, scale);
		if (guides)
		{
			foreach (ScUvRegion region in layout.Regions)
			{
				DrawRegionOutline(result, region.Rect, scale, new RgbaColor(120, 255, 140, 130));
			}
		}
		return result;
	}

	/// <summary>用正交投影把 Minecraft 皮肤六个盒面贴到立方体上，支持旋转与双层外壳预览。</summary>
	public static RgbaImage Render3D(MinecraftSkin skin, bool composeOverlay, int width, int height, double yawDegrees, double pitchDegrees,
		bool fillTransparentAsBlack = false)
	{
		ArgumentNullException.ThrowIfNull(skin);
		width = Math.Max(160, width);
		height = Math.Max(200, height);
		var result = new RgbaImage(width, height);
		FillRect(result, new Rect(0, 0, width, height), new RgbaColor(28, 32, 40, 255));

		double scale = Math.Min(width / 22.0, height / 40.0);
		double centerX = width / 2.0;
		double centerY = height / 2.0 + 3.0 * scale;
		FillEllipseBlend(result, centerX, centerY + 10.5 * scale, 7 * scale, 1.5 * scale, new RgbaColor(0, 0, 0, 60));

		double yaw = yawDegrees * Math.PI / 180.0;
		double pitch = pitchDegrees * Math.PI / 180.0;
		double cosYaw = Math.Cos(yaw), sinYaw = Math.Sin(yaw);
		double cosPitch = Math.Cos(pitch), sinPitch = Math.Sin(pitch);

		Cube[] cubes = BuildCubes(skin.ArmModel);
		// 只填充基础层：第二层外壳的透明像素本意是露出下层，填黑会破坏叠加效果。
		RgbaImage baseTexture = fillTransparentAsBlack
			? skin.Base.FillTransparentAs(new RgbaColor(0, 0, 0, 255))
			: skin.Base;
		DrawPass(result, baseTexture, cubes, 1.0, cosYaw, sinYaw, cosPitch, sinPitch, scale, centerX, centerY, skin);
		if (composeOverlay && skin.HasOverlay)
		{
			DrawPass(result, skin.OverlayLayer, cubes, 1.06, cosYaw, sinYaw, cosPitch, sinPitch, scale, centerX, centerY, skin);
		}
		return result;
	}

	private readonly record struct V2(double X, double Y);

	private readonly record struct V3(double X, double Y, double Z);

	private sealed record Cube(double X, double Y, double Z, double W, double H, double D, McBodyPart Part);

	private sealed record FaceDraw(V2[] Points, Rect Source, double Depth, double Brightness);

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

	private static void DrawPass(RgbaImage target, RgbaImage texture, Cube[] cubes, double factor,
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
				var points = corners.Select(c => Project(c, cosYaw, sinYaw, cosPitch, sinPitch, scale, centerX, centerY)).ToArray();
				double depth = corners.Average(c => DepthOf(c, cosYaw, sinYaw, cosPitch, sinPitch));
				faces.Add(new FaceDraw(points, rect, depth, Brightness(face)));
			}
		}
		// 正交相机位于 +Z，depth 越小越远，先画远面。
		foreach (FaceDraw face in faces.OrderBy(f => f.Depth))
		{
			DrawTexturedQuad(target, texture, face.Points, face.Source, face.Brightness);
		}
	}

	/// <summary>四角对应源矩形的左上、右上、左下、右下，拆成两个三角形做仿射贴图。</summary>
	private static void DrawTexturedQuad(RgbaImage target, RgbaImage texture, V2[] points, Rect source, double brightness)
	{
		V2 p0 = points[0], p1 = points[1], p2 = points[2], p3 = points[3];
		double u0 = source.X, v0 = source.Y;
		double u1 = source.X + source.W, v1 = source.Y + source.H;
		FillTexturedTriangle(target, texture, p0, new V2(u0, v0), p1, new V2(u1, v0), p2, new V2(u0, v1), brightness);
		FillTexturedTriangle(target, texture, p1, new V2(u1, v0), p3, new V2(u1, v1), p2, new V2(u0, v1), brightness);
	}

	private static void FillTexturedTriangle(RgbaImage target, RgbaImage texture,
		V2 a, V2 uvA, V2 b, V2 uvB, V2 c, V2 uvC, double brightness)
	{
		double area = (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);
		if (Math.Abs(area) < 1e-9)
		{
			return;
		}
		int minX = Math.Max(0, (int)Math.Floor(Math.Min(a.X, Math.Min(b.X, c.X))));
		int maxX = Math.Min(target.Width - 1, (int)Math.Ceiling(Math.Max(a.X, Math.Max(b.X, c.X))));
		int minY = Math.Max(0, (int)Math.Floor(Math.Min(a.Y, Math.Min(b.Y, c.Y))));
		int maxY = Math.Min(target.Height - 1, (int)Math.Ceiling(Math.Max(a.Y, Math.Max(b.Y, c.Y))));
		if (minX > maxX || minY > maxY)
		{
			return;
		}
		for (int y = minY; y <= maxY; y++)
		{
			for (int x = minX; x <= maxX; x++)
			{
				double px = x + 0.5, py = y + 0.5;
				double weightA = EdgeFunction(b, c, px, py) / area;
				double weightB = EdgeFunction(c, a, px, py) / area;
				double weightC = EdgeFunction(a, b, px, py) / area;
				if (weightA < -1e-9 || weightB < -1e-9 || weightC < -1e-9)
				{
					continue;
				}
				double u = weightA * uvA.X + weightB * uvB.X + weightC * uvC.X;
				double v = weightA * uvA.Y + weightB * uvB.Y + weightC * uvC.Y;
				int sourceX = Math.Clamp((int)u, 0, texture.Width - 1);
				int sourceY = Math.Clamp((int)v, 0, texture.Height - 1);
				RgbaColor texel = Multiply(texture.GetPixel(sourceX, sourceY), brightness);
				RgbaColor background = target.GetPixel(x, y);
				target.SetPixel(x, y, RgbaColor.Blend(background, texel));
			}
		}
	}

	private static double EdgeFunction(V2 start, V2 end, double px, double py)
	{
		return (end.X - start.X) * (py - start.Y) - (end.Y - start.Y) * (px - start.X);
	}

	private static RgbaColor Multiply(RgbaColor color, double factor)
	{
		if (factor >= 0.999)
		{
			return color;
		}
		return new RgbaColor(
			(byte)Math.Clamp(color.R * factor, 0, 255),
			(byte)Math.Clamp(color.G * factor, 0, 255),
			(byte)Math.Clamp(color.B * factor, 0, 255),
			color.A);
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

	private static V2 Project(V3 point, double cosYaw, double sinYaw, double cosPitch, double sinPitch,
		double scale, double centerX, double centerY)
	{
		double x1 = point.X * cosYaw + point.Z * sinYaw;
		double z1 = -point.X * sinYaw + point.Z * cosYaw;
		double y1 = point.Y * cosPitch - z1 * sinPitch;
		return new V2(centerX + x1 * scale, centerY - y1 * scale);
	}

	private static double DepthOf(V3 point, double cosYaw, double sinYaw, double cosPitch, double sinPitch)
	{
		double z1 = -point.X * sinYaw + point.Z * cosYaw;
		return point.Y * sinPitch + z1 * cosPitch;
	}

	private static double Brightness(McFace face) => face switch
	{
		McFace.Top => 1.0,
		McFace.Front => 0.92,
		McFace.Right => 0.86,
		McFace.Left => 0.82,
		McFace.Back => 0.76,
		McFace.Bottom => 0.70,
		_ => 1.0
	};

	private static void FillRect(RgbaImage image, Rect rect, RgbaColor color)
	{
		for (int y = Math.Max(0, rect.Y); y < Math.Min(image.Height, rect.Y + rect.H); y++)
		{
			for (int x = Math.Max(0, rect.X); x < Math.Min(image.Width, rect.X + rect.W); x++)
			{
				image.SetPixel(x, y, color);
			}
		}
	}

	private static void FillEllipseBlend(RgbaImage image, double centerX, double centerY, double radiusX, double radiusY, RgbaColor color)
	{
		int minX = Math.Max(0, (int)Math.Floor(centerX - radiusX));
		int maxX = Math.Min(image.Width - 1, (int)Math.Ceiling(centerX + radiusX));
		int minY = Math.Max(0, (int)Math.Floor(centerY - radiusY));
		int maxY = Math.Min(image.Height - 1, (int)Math.Ceiling(centerY + radiusY));
		if (radiusX <= 0 || radiusY <= 0)
		{
			return;
		}
		for (int y = minY; y <= maxY; y++)
		{
			for (int x = minX; x <= maxX; x++)
			{
				double dx = (x + 0.5 - centerX) / radiusX;
				double dy = (y + 0.5 - centerY) / radiusY;
				if (dx * dx + dy * dy <= 1.0)
				{
					image.SetPixel(x, y, RgbaColor.Blend(image.GetPixel(x, y), color));
				}
			}
		}
	}

	private static void HLineBlend(RgbaImage image, int x0, int x1, int y, RgbaColor color)
	{
		if (y < 0 || y >= image.Height)
		{
			return;
		}
		for (int x = Math.Max(0, x0); x <= Math.Min(image.Width - 1, x1); x++)
		{
			image.SetPixel(x, y, RgbaColor.Blend(image.GetPixel(x, y), color));
		}
	}

	private static void VLineBlend(RgbaImage image, int x, int y0, int y1, RgbaColor color)
	{
		if (x < 0 || x >= image.Width)
		{
			return;
		}
		for (int y = Math.Max(0, y0); y <= Math.Min(image.Height - 1, y1); y++)
		{
			image.SetPixel(x, y, RgbaColor.Blend(image.GetPixel(x, y), color));
		}
	}

	private static void DrawRectBlend(RgbaImage image, Rect rect, RgbaColor color)
	{
		HLineBlend(image, rect.X, rect.X + rect.W - 1, rect.Y, color);
		HLineBlend(image, rect.X, rect.X + rect.W - 1, rect.Y + rect.H - 1, color);
		VLineBlend(image, rect.X, rect.Y, rect.Y + rect.H - 1, color);
		VLineBlend(image, rect.X + rect.W - 1, rect.Y, rect.Y + rect.H - 1, color);
	}

	private static void DrawCheckerboard(RgbaImage image, int size, int tile)
	{
		var light = new RgbaColor(205, 208, 214, 255);
		var dark = new RgbaColor(170, 174, 182, 255);
		for (int y = 0; y < size; y += tile)
		{
			for (int x = 0; x < size; x += tile)
			{
				FillRect(image, new Rect(x, y, Math.Min(tile, size - x), Math.Min(tile, size - y)), ((x / tile + y / tile) & 1) == 0 ? light : dark);
			}
		}
	}

	private static void BlitNearest(RgbaImage target, RgbaImage source, Rect destination)
	{
		for (int dy = 0; dy < destination.H; dy++)
		{
			int sy = Math.Min(source.Height - 1, dy * source.Height / destination.H);
			for (int dx = 0; dx < destination.W; dx++)
			{
				int sx = Math.Min(source.Width - 1, dx * source.Width / destination.W);
				RgbaColor texel = source.GetPixel(sx, sy);
				RgbaColor background = target.GetPixel(destination.X + dx, destination.Y + dy);
				target.SetPixel(destination.X + dx, destination.Y + dy, RgbaColor.Blend(background, texel));
			}
		}
	}

	private static void DrawGrid(RgbaImage image, int size, int scale)
	{
		int step = 8 * Math.Max(1, scale);
		var color = new RgbaColor(40, 44, 52, 70);
		for (int position = 0; position <= size; position += step)
		{
			VLineBlend(image, position, 0, size, color);
			HLineBlend(image, 0, size, position, color);
		}
	}

	private static void DrawRegionOutline(RgbaImage image, Rect rect, int scale, RgbaColor color)
	{
		DrawRectBlend(image, new Rect(rect.X * scale, rect.Y * scale, rect.W * scale, rect.H * scale), color);
	}
}
