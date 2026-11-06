#nullable enable
using System;
using System.Collections.Generic;

namespace CharacterSkinsMC.Core;

/// <summary>
/// Survivalcraft 专属 3D 预览：直接把真实 HumanMale/HumanFemale.dae 的三角形贴上图集，
/// 与 SkinPreview.Render3D 的 Minecraft 立方体预览是两条独立渲染通道，形状、骨架和 UV 都不同。
/// 纯托管软件光栅化，Android 同样可用。
/// </summary>
public static class ScModelPreview
{
	private readonly record struct ScreenPoint(double X, double Y);

	private sealed class ProjectedTriangle
	{
		public ScreenPoint P0, P1, P2;
		public ScUv Uv0, Uv1, Uv2;
		public double Depth;
		public float Brightness;
	}

	public static RgbaImage Render(RgbaImage atlas, ScPlayerClass playerClass,
		int width, int height, double yawDegrees, double pitchDegrees, bool fillTransparentAsBlack = false) =>
		Render(atlas, ScDefaultSkins.LoadModel(playerClass), width, height, yawDegrees, pitchDegrees, fillTransparentAsBlack);

	public static RgbaImage Render(RgbaImage atlas, ScModel model,
		int width, int height, double yawDegrees, double pitchDegrees, bool fillTransparentAsBlack = false)
	{
		ArgumentNullException.ThrowIfNull(atlas);
		ArgumentNullException.ThrowIfNull(model);
		if (fillTransparentAsBlack)
		{
			// 图集透明区域填成不透明黑色，避免人物表面出现镂空，观感更实。
			atlas = atlas.FillTransparentAs(new RgbaColor(0, 0, 0, 255));
		}
		width = Math.Max(160, width);
		height = Math.Max(200, height);
		var result = new RgbaImage(width, height);
		// 与 MC 立方体预览用不同的底色，便于一眼区分两条预览通道。
		FillRect(result, new Rect(0, 0, width, height), new RgbaColor(34, 40, 50, 255));

		double modelWidth = Math.Max(1e-6, model.Max.X - model.Min.X);
		double modelHeight = Math.Max(1e-6, model.Max.Y - model.Min.Y);
		double scale = Math.Min(width / (modelWidth * 1.8), height / (modelHeight * 1.12));
		double centerX = (model.Min.X + model.Max.X) / 2.0;
		double centerY = (model.Min.Y + model.Max.Y) / 2.0;
		double centerZ = (model.Min.Z + model.Max.Z) / 2.0;
		FillEllipseBlend(result, width / 2.0, height / 2.0 + modelHeight * scale * 0.46,
			modelWidth * scale * 1.1, modelHeight * scale * 0.06, new RgbaColor(0, 0, 0, 70));

		// HumanMale/HumanFemale.dae 的人物正面朝 -Z，而 MC 立方体预览正面朝 +Z；
		// 统一偏转 180°，使同一个视角数值在两条预览通道中都看到人物正面。
		double yaw = (yawDegrees + 180.0) * Math.PI / 180.0;
		double pitch = pitchDegrees * Math.PI / 180.0;
		double cosYaw = Math.Cos(yaw), sinYaw = Math.Sin(yaw);
		double cosPitch = Math.Cos(pitch), sinPitch = Math.Sin(pitch);

		var projected = new List<ProjectedTriangle>(model.Triangles.Count);
		foreach (ScTriangle triangle in model.Triangles)
		{
			ScreenPoint p0 = Project(triangle.A, centerX, centerY, centerZ, cosYaw, sinYaw, cosPitch, sinPitch, scale, width, height);
			ScreenPoint p1 = Project(triangle.B, centerX, centerY, centerZ, cosYaw, sinYaw, cosPitch, sinPitch, scale, width, height);
			ScreenPoint p2 = Project(triangle.C, centerX, centerY, centerZ, cosYaw, sinYaw, cosPitch, sinPitch, scale, width, height);
			double depth = (
				DepthOf(triangle.A, centerX, centerY, centerZ, cosYaw, sinYaw, cosPitch, sinPitch) +
				DepthOf(triangle.B, centerX, centerY, centerZ, cosYaw, sinYaw, cosPitch, sinPitch) +
				DepthOf(triangle.C, centerX, centerY, centerZ, cosYaw, sinYaw, cosPitch, sinPitch)) / 3.0;
			projected.Add(new ProjectedTriangle
			{
				P0 = p0, P1 = p1, P2 = p2,
				Uv0 = triangle.UvA, Uv1 = triangle.UvB, Uv2 = triangle.UvC,
				Depth = depth,
				Brightness = FaceBrightness(triangle)
			});
		}
		// 正交相机位于 +Z，depth 越小越远，先画远面。
		projected.Sort((a, b) => a.Depth.CompareTo(b.Depth));
		foreach (ProjectedTriangle triangle in projected)
		{
			RasterizeTriangle(result, atlas, triangle);
		}
		return result;
	}

	private static ScreenPoint Project(ScVector3 point, double centerX, double centerY, double centerZ,
		double cosYaw, double sinYaw, double cosPitch, double sinPitch, double scale, int width, int height)
	{
		double x = point.X - centerX, y = point.Y - centerY, z = point.Z - centerZ;
		double x1 = x * cosYaw + z * sinYaw;
		double z1 = -x * sinYaw + z * cosYaw;
		double y1 = y * cosPitch - z1 * sinPitch;
		return new ScreenPoint(width / 2.0 + x1 * scale, height / 2.0 - y1 * scale);
	}

	private static double DepthOf(ScVector3 point, double centerX, double centerY, double centerZ,
		double cosYaw, double sinYaw, double cosPitch, double sinPitch)
	{
		double x = point.X - centerX, y = point.Y - centerY, z = point.Z - centerZ;
		double z1 = -x * sinYaw + z * cosYaw;
		return y * sinPitch + z1 * cosPitch;
	}

	private static float FaceBrightness(ScTriangle triangle)
	{
		double ux = triangle.B.X - triangle.A.X, uy = triangle.B.Y - triangle.A.Y, uz = triangle.B.Z - triangle.A.Z;
		double vx = triangle.C.X - triangle.A.X, vy = triangle.C.Y - triangle.A.Y, vz = triangle.C.Z - triangle.A.Z;
		double nx = uy * vz - uz * vy;
		double ny = uz * vx - ux * vz;
		double nz = ux * vy - uy * vx;
		double length = Math.Sqrt(nx * nx + ny * ny + nz * nz);
		if (length < 1e-9)
		{
			return 0.85f;
		}
		// 固定世界光源，人物旋转时光照保持稳定；双面取绝对值避免背面全黑。
		double dot = Math.Abs((nx * 0.35 + ny * 0.85 + nz * 0.45) / (length * 1.0));
		return (float)(0.62 + 0.38 * Math.Min(1.0, dot));
	}

	private static void RasterizeTriangle(RgbaImage target, RgbaImage atlas, ProjectedTriangle triangle)
	{
		double area = Edge(triangle.P0, triangle.P1, triangle.P2);
		if (Math.Abs(area) < 1e-9)
		{
			return;
		}
		double inverseArea = 1.0 / area;
		int minX = Math.Max(0, (int)Math.Floor(Math.Min(triangle.P0.X, Math.Min(triangle.P1.X, triangle.P2.X))));
		int maxX = Math.Min(target.Width - 1, (int)Math.Ceiling(Math.Max(triangle.P0.X, Math.Max(triangle.P1.X, triangle.P2.X))));
		int minY = Math.Max(0, (int)Math.Floor(Math.Min(triangle.P0.Y, Math.Min(triangle.P1.Y, triangle.P2.Y))));
		int maxY = Math.Min(target.Height - 1, (int)Math.Ceiling(Math.Max(triangle.P0.Y, Math.Max(triangle.P1.Y, triangle.P2.Y))));
		for (int y = minY; y <= maxY; y++)
		{
			for (int x = minX; x <= maxX; x++)
			{
				var pixel = new ScreenPoint(x + 0.5, y + 0.5);
				double w0 = Edge(triangle.P1, triangle.P2, pixel) * inverseArea;
				double w1 = Edge(triangle.P2, triangle.P0, pixel) * inverseArea;
				double w2 = Edge(triangle.P0, triangle.P1, pixel) * inverseArea;
				if (w0 < 0 || w1 < 0 || w2 < 0)
				{
					continue;
				}
				double u = w0 * triangle.Uv0.U + w1 * triangle.Uv1.U + w2 * triangle.Uv2.U;
				// COLLADA 的 T 与图片行方向相反，采样时翻转。
				double v = 1.0 - (w0 * triangle.Uv0.V + w1 * triangle.Uv1.V + w2 * triangle.Uv2.V);
				int texelX = Math.Clamp((int)(u * atlas.Width), 0, atlas.Width - 1);
				int texelY = Math.Clamp((int)(v * atlas.Height), 0, atlas.Height - 1);
				RgbaColor texel = atlas.GetPixel(texelX, texelY);
				if (texel.A == 0)
				{
					continue;
				}
				var lit = new RgbaColor(
					(byte)Math.Clamp((int)(texel.R * triangle.Brightness), 0, 255),
					(byte)Math.Clamp((int)(texel.G * triangle.Brightness), 0, 255),
					(byte)Math.Clamp((int)(texel.B * triangle.Brightness), 0, 255),
					texel.A);
				target.SetPixel(x, y, RgbaColor.Blend(target.GetPixel(x, y), lit));
			}
		}
	}

	private static double Edge(ScreenPoint a, ScreenPoint b, ScreenPoint p) =>
		(b.X - a.X) * (p.Y - a.Y) - (b.Y - a.Y) * (p.X - a.X);

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

	private static void FillEllipseBlend(RgbaImage image, double centerX, double centerY,
		double radiusX, double radiusY, RgbaColor color)
	{
		int minX = Math.Max(0, (int)(centerX - radiusX)), maxX = Math.Min(image.Width - 1, (int)(centerX + radiusX));
		int minY = Math.Max(0, (int)(centerY - radiusY)), maxY = Math.Min(image.Height - 1, (int)(centerY + radiusY));
		for (int y = minY; y <= maxY; y++)
		{
			for (int x = minX; x <= maxX; x++)
			{
				double dx = (x + 0.5 - centerX) / Math.Max(1e-6, radiusX);
				double dy = (y + 0.5 - centerY) / Math.Max(1e-6, radiusY);
				if (dx * dx + dy * dy <= 1.0)
				{
					image.SetPixel(x, y, RgbaColor.Blend(image.GetPixel(x, y), color));
				}
			}
		}
	}
}
