using System.Diagnostics;
using System.Globalization;

using PsdParser;
using PsdParser.AdditionalLayerInformations;

using static PsdParser.AdditionalLayerInformations.SectionDividerSetting;
using System.Runtime.InteropServices;
using System.Diagnostics.CodeAnalysis;

namespace KuchiPaku.Psd;

public static class PsdUtil
{
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "SMA0040:Missing Using Statement", Justification = "<保留中>")]
	public static async ValueTask<PsdFile> LoadPsdAsync(
		string path
	)
	{
		if (!Path.Exists(path))
		{
			throw new FileNotFoundException($"file: {path} is not found!");
		}
		var isPsd =
			path.EndsWith(".psd", StringComparison.OrdinalIgnoreCase) ||
			path.EndsWith(".psb", StringComparison.OrdinalIgnoreCase);
		if (!isPsd)
		{
			throw new FileLoadException($"file is not psd: {path}");
		}

		var psd = await Task
			.Run(() => new PsdFile(path))
			.ConfigureAwait(false);

		Debug.WriteLine($"""
			------------------------------------
			PSD file: {Path.GetFileName(path)}
			w: {psd.Header.Width}, h: {psd.Header.Height}
			depth: {psd.Header.Depth}
			channel: {psd.Header.Channels}
			ver. {psd.Header.Version}
			------------------------------------
			""");
		return psd;
	}

	public static IReadOnlyList<YmmPsdLayer> ParsePsdLayers(PsdFile psd)
	{
		var layers = psd
			.LayerAndMaskInformationSection
			.LayerInfo
			.Items;

		var tree = ConvertToTree(layers);

		PrintTree(tree);

		//DebugPrintLayers(layers);

		return tree;
	}

	[Conditional("DEBUG")]
	static void DebugPrintLayers(LayerRecordAndImage[] layers)
	{
		for (int i = 0; i < layers.Length; i++)
		{
			var layer = layers[i];

			var img = layer.Image;
			bool isFolder = layer.IsFolderLike();
			var head = (isFolder ? ParseFolderInfo(layer) : "");
			Debug.WriteLine($"""
				{head} [n{i}] 「{layer.Record.LayerName}」 {img.Width} x {img.Height}
				""");
		}

		static string ParseFolderInfo(LayerRecordAndImage layer)
		{
			var info = layer
				.Record
				.AdditionalLayerInformations
				.FirstOrDefault(v => v is SectionDividerSetting);
			return info is not SectionDividerSetting sec
				? ""
				: sec.Type switch
				{
					LsctType.ClosedFolder => "💼",
					LsctType.OpenedFolder =>
					"📂",
					LsctType.BoundingSectionDivider =>
					"-",
					_ => "other",
				};
		}
	}

	[Conditional("DEBUG")]
	static void PrintTree(IEnumerable<YmmPsdLayer> layers, int depth = 0, string prefix = "")
	{
		var items = layers.ToList();
		int count = items.Count;

		for (int i = 0; i < count; i++)
		{
			var layer = items[i];
			bool isLast = (i == count - 1);

			// ツリー用の接頭辞
			string branch = isLast ? "└─ " : "├─ ";
			string indent = prefix + branch;

			// アイコンの選択
			string icon = layer.IsFolder switch
			{
				true => layer.IsFolderOpened ? "📂" : "📁",
				false => "",
			};
			Debug.WriteLine($"{indent}{icon}[{layer.Cid}]{layer.Name}");

			// 子ノードがある場合は再帰
			if (layer.Children.Any())
			{
				string newPrefix = prefix + (isLast ? "  " : "│  ");
				PrintTree(layer.Children, depth + 1, newPrefix);
			}
		}
	}



	[SuppressMessage("Performance", "CA1859:可能な場合は具象型を使用してパフォーマンスを向上させる", Justification = "<保留中>")]
	static IReadOnlyList<YmmPsdLayer>
	ConvertToTree(
		IEnumerable<LayerRecordAndImage> layers
	)
	{
		List<YmmPsdLayer> rootNodes = [];
		Stack<YmmPsdLayer> folderStack = new();

		int index = layers.Count() - 1;
		foreach (var layer in layers.Reverse())
		{
			var node = new YmmPsdLayer(
				$"n{index--}",
				layer
			);

			if (node.IsFolder)
			{
				// フォルダ開始 → スタックに積む
				if (folderStack.TryPeek(out var parent))
				{
					parent.Children.Add(node);
					node.Parent = parent;
				}
				else
				{
					rootNodes.Add(node);
				}
				folderStack.Push(node);
			}
			else if (node.IsDivider)
			{
				// フォルダ終了 → スタックから外す（空でない場合のみ）
				if (folderStack.Count > 0)
				{
					var closedFolder = folderStack.Pop();

					// 親フォルダを設定
					if (folderStack.TryPeek(out var newParent))
					{
						closedFolder.Parent = newParent;
					}
				}
			}
			else
			{
				// 通常レイヤー
				if (folderStack.TryPeek(out var parent))
				{
					parent.Children.Add(node);
					node.Parent = parent;
				}
				else
				{
					rootNodes.Add(node);
				}
			}
		}

		return rootNodes;
	}



	public static bool IsFolderLike(this LayerRecordAndImage layer)
	{
		return layer.Record
			.AdditionalLayerInformations
			.OfType<SectionDividerSetting>()
			.Any();
	}

	public static bool IsFolder(this LayerRecordAndImage layer)
	{
		if (!layer.IsFolderLike())
		{
			return false;
		}
		var type = GetSectionLayerType(layer);
		return type is LsctType.OpenedFolder or LsctType.ClosedFolder;
	}

	public static bool IsDivider(this LayerRecordAndImage layer)
	{
		if (!layer.IsFolderLike()) { return false; }
		var type = GetSectionLayerType(layer);
		return type is LsctType.BoundingSectionDivider;
	}
	public static bool IsFolderOpened(this LayerRecordAndImage layer)
	{
		if (!layer.IsFolderLike())
		{
			return false;
		}
		var type = GetSectionLayerType(layer);
		return type is LsctType.OpenedFolder;
	}

	static LsctType GetSectionLayerType(LayerRecordAndImage layer)
	{
		return layer.Record
			.AdditionalLayerInformations
			.OfType<SectionDividerSetting>()
			.First()
			.Type;
	}
}
