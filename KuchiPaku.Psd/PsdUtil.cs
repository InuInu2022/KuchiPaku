using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using PsdParser;
using PsdParser.AdditionalLayerInformations;
using System.Drawing;
using System.Drawing.Imaging;
using static PsdParser.AdditionalLayerInformations.SectionDividerSetting;
using System.Runtime.InteropServices;

namespace KuchiPaku.Psd;

public static class PsdUtil
{
	static readonly SemaphoreSlim _lock = new(1, 1);

	[SuppressMessage("Usage", "SMA0040:Missing Using Statement", Justification = "<保留中>")]
	public static async ValueTask<PsdFile> LoadPsdAsync(string path)
	{
		if (!Path.Exists(path))
		{
			throw new FileNotFoundException($"file: {path} is not found!");
		}
		var isPsd =
			path.EndsWith(".psd", StringComparison.OrdinalIgnoreCase)
			|| path.EndsWith(".psb", StringComparison.OrdinalIgnoreCase);
		if (!isPsd)
		{
			throw new FileLoadException($"file is not psd: {path}");
		}

		var psd = await Task.Run(() => new PsdFile(path)).ConfigureAwait(false);

		Debug.WriteLine(
			$"""
			------------------------------------
			PSD file: {Path.GetFileName(path)}
			w: {psd.Header.Width}, h: {psd.Header.Height}
			depth: {psd.Header.Depth}
			channel: {psd.Header.Channels}
			ver. {psd.Header.Version}
			------------------------------------
			"""
		);
		return psd;
	}

	public static IReadOnlyList<YmmPsdLayer> ParsePsdLayers(PsdFile psd)
	{
		var layers = psd.LayerAndMaskInformationSection.LayerInfo.Items;

		var tree = ConvertToTree(layers);

		PrintTree(tree);

		//DebugPrintLayers(layers);

		return tree;
	}

	public static Bitmap CreateImageFromTree(
		IReadOnlyList<YmmPsdLayer> tree, int width, int height,
		IEnumerable<string>? enabledLayers
	)
	{
		var finalBitmap = new Bitmap(width, height);

		// Graphics オブジェクトを作成
		using (Graphics g = Graphics.FromImage(finalBitmap))
		{
			// 背景を透明に設定
			g.Clear(System.Drawing.Color.Transparent);

			// 入れ子になったレイヤーを走査して合成
			CombineLayerRecursive(tree, g, enabledLayers);
		}

		return finalBitmap;
	}

	public static async Task<Bitmap> CreateImageFromLayerAsync(YmmPsdLayer layer)
	{
		///*
		await _lock.WaitAsync().ConfigureAwait(false);
		byte[] pixelData = [];
		try
		{
			pixelData = await Task
				.Run(() => layer.Image.Read()).ConfigureAwait(false);
		}
		finally
		{
			_lock.Release();
		}
		//*/
		//var pixelData = layer.Image.Read();
		return CreateBitmapFromPixelData(
			pixelData, layer.Image.Width, layer.Image.Height);
	}

	static void CombineLayerRecursive(
		IEnumerable<YmmPsdLayer> layers,
		Graphics g,
		IEnumerable<string>? enabledLayers
	)
	{
		foreach (var layer in layers.Reverse())
		{
			if (layer.IsFolder)
			{
				// フォルダの場合、再帰的にその中のレイヤーを走査
				if (!enabledLayers?.Contains(layer.Cid, StringComparer.Ordinal) ??  false /*!layer.IsVisible*/)
				{
					continue;
				}
				CombineLayerRecursive(layer.Children.Reverse(), g, enabledLayers);
			}
			else if (layer.IsNormalLayer)
			{
				if (layer.Image.Width == 0 || layer.Image.Height == 0)
				{
					continue;
				}

				if (!enabledLayers?.Contains(layer.Cid, StringComparer.Ordinal) ?? false /*!layer.IsVisible*/)
				{
					continue;
				}

				// 通常レイヤーの場合、ビットマップを取得して合成
				byte[] pixelData = layer.Image.Read(); // Pixelデータ取得
				using var layerBitmap = CreateBitmapFromPixelData(
					pixelData,
					layer.Image.Width,
					layer.Image.Height
				);

				//layer.Record.

				// レイヤーのビットマップを合成（上書き）
				g.DrawImage(layerBitmap, layer.Record.Left, layer.Record.Top);
			}
		}
	}

	static Bitmap CreateBitmapFromPixelData(byte[] pixelData, int width, int height)
	{
		var bitmap = new Bitmap(
			width,
			height,
			PixelFormat.Format32bppArgb
		);

		// ピクセルデータをビットマップに設定
		var rect = new System.Drawing.Rectangle(0, 0, width, height);
		var data = bitmap.LockBits(
			rect,
			System.Drawing.Imaging.ImageLockMode.WriteOnly,
			bitmap.PixelFormat
		);
		Marshal.Copy(pixelData, 0, data.Scan0, pixelData.Length);
		bitmap.UnlockBits(data);

		return bitmap;
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
			Debug.WriteLine(
				$"""
				{head} [n{i}] 「{layer.Record.LayerName}」 {img.Width} x {img.Height}
				"""
			);
		}

		static string ParseFolderInfo(LayerRecordAndImage layer)
		{
			var info = layer.Record.AdditionalLayerInformations.FirstOrDefault(v =>
				v is SectionDividerSetting
			);
			return info is not SectionDividerSetting sec
				? ""
				: sec.Type switch
				{
					LsctType.ClosedFolder => "💼",
					LsctType.OpenedFolder => "📂",
					LsctType.BoundingSectionDivider => "-",
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

	[SuppressMessage(
		"Performance",
		"CA1859:可能な場合は具象型を使用してパフォーマンスを向上させる",
		Justification = "<保留中>"
	)]
	static IReadOnlyList<YmmPsdLayer> ConvertToTree(IEnumerable<LayerRecordAndImage> layers)
	{
		List<YmmPsdLayer> rootNodes = [];
		Stack<YmmPsdLayer> folderStack = new();

		int index = layers.Count() - 1;
		foreach (var layer in layers.Reverse())
		{
			var node = new YmmPsdLayer($"n{index--}", layer);

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
		return layer.Record.AdditionalLayerInformations
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
		if (!layer.IsFolderLike())
		{
			return false;
		}
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
		return layer
			.Record.AdditionalLayerInformations
			.OfType<SectionDividerSetting>()
			.First()
			.Type;
	}
}
