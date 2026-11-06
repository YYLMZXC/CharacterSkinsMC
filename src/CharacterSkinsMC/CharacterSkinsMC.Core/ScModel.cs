#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace CharacterSkinsMC.Core;

public readonly record struct ScVector3(double X, double Y, double Z);

public readonly record struct ScUv(double U, double V);

/// <summary>一个已应用场景变换的贴图三角形，UV 保持 COLLADA 原始 T 方向。</summary>
public sealed class ScTriangle
{
	public ScVector3 A { get; init; }
	public ScVector3 B { get; init; }
	public ScVector3 C { get; init; }
	public ScUv UvA { get; init; }
	public ScUv UvB { get; init; }
	public ScUv UvC { get; init; }
}

/// <summary>
/// Survivalcraft 人物模型（HumanMale.dae / HumanFemale.dae）的几何快照。
/// DAE 为 Y_UP，Body 节点把几何的 Z 轴换成世界 Y 轴并缩放，四肢节点各有 Z 向平移；
/// 因此必须解析 library_visual_scenes 的累积矩阵，否则躯干会与四肢脱节。
/// </summary>
public sealed class ScModel
{
	private static readonly XNamespace Ns = "http://www.collada.org/2005/11/COLLADASchema";

	public IReadOnlyList<ScTriangle> Triangles { get; }
	public ScVector3 Min { get; }
	public ScVector3 Max { get; }

	private ScModel(List<ScTriangle> triangles)
	{
		Triangles = triangles;
		double minX = double.PositiveInfinity, minY = double.PositiveInfinity, minZ = double.PositiveInfinity;
		double maxX = double.NegativeInfinity, maxY = double.NegativeInfinity, maxZ = double.NegativeInfinity;
		foreach (ScTriangle triangle in triangles)
		{
			foreach (ScVector3 point in new[] { triangle.A, triangle.B, triangle.C })
			{
				minX = Math.Min(minX, point.X); maxX = Math.Max(maxX, point.X);
				minY = Math.Min(minY, point.Y); maxY = Math.Max(maxY, point.Y);
				minZ = Math.Min(minZ, point.Z); maxZ = Math.Max(maxZ, point.Z);
			}
		}
		Min = new ScVector3(minX, minY, minZ);
		Max = new ScVector3(maxX, maxY, maxZ);
	}

	public static ScModel Load(byte[] daeBytes)
	{
		ArgumentNullException.ThrowIfNull(daeBytes);
		using var stream = new MemoryStream(daeBytes);
		XDocument document = XDocument.Load(stream);
		XElement root = document.Root ?? throw new InvalidDataException("DAE 文件缺少根元素。");
		Dictionary<string, List<ScTriangle>> meshes = ParseGeometries(root);
		var triangles = new List<ScTriangle>();
		XElement? scene = root.Element(Ns + "library_visual_scenes")?.Element(Ns + "visual_scene");
		if (scene == null)
		{
			throw new InvalidDataException("DAE 文件缺少 visual_scene，无法组装人物模型。");
		}
		foreach (XElement node in scene.Elements(Ns + "node"))
		{
			VisitNode(node, Identity(), meshes, triangles);
		}
		if (triangles.Count == 0)
		{
			throw new InvalidDataException("DAE 文件没有可用于渲染的三角形。");
		}
		return new ScModel(triangles);
	}

	private static Dictionary<string, List<ScTriangle>> ParseGeometries(XElement root)
	{
		var meshes = new Dictionary<string, List<ScTriangle>>(StringComparer.Ordinal);
		XElement? library = root.Element(Ns + "library_geometries");
		if (library == null)
		{
			return meshes;
		}
		foreach (XElement geometry in library.Elements(Ns + "geometry"))
		{
			string id = geometry.Attribute("id")?.Value ?? throw new InvalidDataException("geometry 缺少 id。");
			XElement? mesh = geometry.Element(Ns + "mesh");
			if (mesh == null)
			{
				continue;
			}
			double[] positions = ReadSource(mesh, "-positions");
			double[] uvs = ReadSource(mesh, "-map");
			XElement? trianglesElement = mesh.Element(Ns + "triangles");
			if (trianglesElement == null)
			{
				continue;
			}
			List<XElement> inputs = trianglesElement.Elements(Ns + "input").ToList();
			int stride = inputs.Count;
			int vertexOffset = FindOffset(inputs, "VERTEX");
			int uvOffset = FindOffset(inputs, "TEXCOORD");
			var triangles = new List<ScTriangle>();
			foreach (XElement p in trianglesElement.Elements(Ns + "p"))
			{
				int[] indices = ParseInts(p.Value);
				for (int i = 0; i + 3 * stride <= indices.Length; i += 3 * stride)
				{
					int[] vertexIndices = new int[3];
					int[] uvIndices = new int[3];
					for (int corner = 0; corner < 3; corner++)
					{
						vertexIndices[corner] = indices[i + corner * stride + vertexOffset];
						uvIndices[corner] = indices[i + corner * stride + uvOffset];
					}
					triangles.Add(new ScTriangle
					{
						A = ReadVector3(positions, vertexIndices[0]),
						B = ReadVector3(positions, vertexIndices[1]),
						C = ReadVector3(positions, vertexIndices[2]),
						UvA = ReadUv(uvs, uvIndices[0]),
						UvB = ReadUv(uvs, uvIndices[1]),
						UvC = ReadUv(uvs, uvIndices[2])
					});
				}
			}
			meshes[id] = triangles;
		}
		return meshes;
	}

	private static void VisitNode(XElement node, double[] parentMatrix,
		Dictionary<string, List<ScTriangle>> meshes, List<ScTriangle> output)
	{
		double[] local = ParseMatrix(node.Element(Ns + "matrix")?.Value);
		double[] world = Multiply(parentMatrix, local);
		foreach (XElement instance in node.Elements(Ns + "instance_geometry"))
		{
			string id = (instance.Attribute("url")?.Value ?? string.Empty).TrimStart('#');
			if (id.Length == 0 || !meshes.TryGetValue(id, out List<ScTriangle>? triangles))
			{
				continue;
			}
			foreach (ScTriangle triangle in triangles)
			{
				output.Add(new ScTriangle
				{
					A = Transform(world, triangle.A),
					B = Transform(world, triangle.B),
					C = Transform(world, triangle.C),
					UvA = triangle.UvA,
					UvB = triangle.UvB,
					UvC = triangle.UvC
				});
			}
		}
		foreach (XElement child in node.Elements(Ns + "node"))
		{
			VisitNode(child, world, meshes, output);
		}
	}

	private static double[] ReadSource(XElement mesh, string suffix)
	{
		XElement? source = mesh.Elements(Ns + "source")
			.FirstOrDefault(element => (element.Attribute("id")?.Value ?? string.Empty).EndsWith(suffix, StringComparison.Ordinal));
		if (source == null)
		{
			throw new InvalidDataException($"DAE 网格缺少 {suffix} 数据源。");
		}
		return ParseFloats(source.Element(Ns + "float_array")?.Value ?? string.Empty);
	}

	private static int FindOffset(List<XElement> inputs, string semantic)
	{
		XElement? input = inputs.FirstOrDefault(element => element.Attribute("semantic")?.Value == semantic);
		if (input == null)
		{
			throw new InvalidDataException($"DAE 三角形缺少 {semantic} 输入。");
		}
		return int.Parse(input.Attribute("offset")?.Value ?? "0", CultureInfo.InvariantCulture);
	}

	private static ScVector3 ReadVector3(double[] values, int index) =>
		new(values[index * 3], values[index * 3 + 1], values[index * 3 + 2]);

	private static ScUv ReadUv(double[] values, int index) =>
		new(values[index * 2], values[index * 2 + 1]);

	private static double[] ParseFloats(string text) => text
		.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
		.Select(part => double.Parse(part, NumberStyles.Float, CultureInfo.InvariantCulture))
		.ToArray();

	private static int[] ParseInts(string text) => text
		.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
		.Select(part => int.Parse(part, CultureInfo.InvariantCulture))
		.ToArray();

	private static double[] Identity() => new double[]
	{
		1, 0, 0, 0,
		0, 1, 0, 0,
		0, 0, 1, 0,
		0, 0, 0, 1
	};

	/// <summary>COLLADA 导出的矩阵按行主序书写，平移分量位于每行末列。</summary>
	private static double[] ParseMatrix(string? text)
	{
		if (string.IsNullOrWhiteSpace(text))
		{
			return Identity();
		}
		double[] values = ParseFloats(text);
		if (values.Length != 16)
		{
			throw new InvalidDataException($"DAE 节点矩阵元素数量为 {values.Length}，应为 16。");
		}
		return values;
	}

	private static double[] Multiply(double[] a, double[] b)
	{
		var result = new double[16];
		for (int row = 0; row < 4; row++)
		{
			for (int column = 0; column < 4; column++)
			{
				double sum = 0;
				for (int k = 0; k < 4; k++)
				{
					sum += a[row * 4 + k] * b[k * 4 + column];
				}
				result[row * 4 + column] = sum;
			}
		}
		return result;
	}

	private static ScVector3 Transform(double[] matrix, ScVector3 point) => new(
		matrix[0] * point.X + matrix[1] * point.Y + matrix[2] * point.Z + matrix[3],
		matrix[4] * point.X + matrix[5] * point.Y + matrix[6] * point.Z + matrix[7],
		matrix[8] * point.X + matrix[9] * point.Y + matrix[10] * point.Z + matrix[11]);
}
