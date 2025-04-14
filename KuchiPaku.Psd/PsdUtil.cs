using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using PsdParser;
using PsdParser.AdditionalLayerInformations;
using static PsdParser.AdditionalLayerInformations.SectionDividerSetting;

namespace KuchiPaku.Psd;

public static class PsdUtil
{
	static readonly SemaphoreSlim _lock = new(1, 1);

	// レイヤーイメージのキャッシュ
	private static readonly ConcurrentDictionary<string, WeakReference<Bitmap>> _layerImageCache =
		new(StringComparer.Ordinal);

	// パフォーマンス計測用
	private static readonly Stopwatch _perfTimer = new();

	//ピクセルデータをキャッシュする
	private static readonly ConcurrentDictionary<
		string,
		(byte[] PixelData, int Width, int Height, DateTime LastAccess)
	> _pixelDataCache = new(StringComparer.Ordinal);

	// キャッシュクリーンアップ用
	private static DateTime _lastCacheCleanup = DateTime.UtcNow;
	private static readonly TimeSpan _cacheCleanupInterval = TimeSpan.FromMinutes(5);
	private static readonly TimeSpan _cacheEntryMaxAge = TimeSpan.FromMinutes(30);

	// 既存のlock用のSemaphoreSlimとは別に、レイヤー結果用のものを追加
	private static readonly SemaphoreSlim _resultLock = new(1, 1);

	// 追加のキャッシュ
	private static readonly ConcurrentDictionary<string, byte[]> _rawDataCache =
		new(StringComparer.Ordinal);

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
		return tree;
	}

	/// <summary>
	/// PSDレイヤーツリーから画像を高速生成します
	/// </summary>
	/// <param name="tree">レイヤーツリー</param>
	/// <param name="width">画像幅</param>
	/// <param name="height">画像高さ</param>
	/// <param name="enabledLayers">表示するレイヤーのID一覧</param>
	/// <param name="progress">進捗通知用オブジェクト (0-100)</param>
	/// <returns>合成された画像</returns>
	[SuppressMessage("Usage", "SMA0040:Missing Using Statement", Justification = "<保留中>")]
	public static async Task<Bitmap> CreateImageFromTreeAsync(
		IReadOnlyList<YmmPsdLayer> tree,
		int width,
		int height,
		IEnumerable<string>? enabledLayers,
		IProgress<int>? progress = null
	)
	{
		_perfTimer.Restart();
		progress?.Report(0);

		// まず出力用ビットマップを生成
		var finalBitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);

		// 表示するレイヤーのIDセットを作成
		var enabledLayerSet =
			enabledLayers?.ToHashSet(StringComparer.Ordinal)
			?? new HashSet<string>(StringComparer.Ordinal);

		// 表示可能なレイヤー（親フォルダの状態も考慮）を計算
		var visibleLayerSet = new HashSet<string>(StringComparer.Ordinal);

		// 親フォルダーの状態を考慮したレイヤーの可視性を計算
		CalculateLayerVisibility(tree, enabledLayerSet, visibleLayerSet, true);

		// フラット化したレイヤー一覧を取得（描画対象のみ）
		var relevantLayers = FlattenLayersInDrawOrder(tree)
			.Where(layer =>
				// フォルダではなく
				!layer.IsFolder
				// サイズが有効で
				&& layer.Image.Width > 0
				&& layer.Image.Height > 0
				// 可視性計算の結果、表示されるレイヤーのみを対象とする
				&& visibleLayerSet.Contains(layer.Cid)
			)
			.ToList();

		int totalLayers = relevantLayers.Count;
		int processedLayers = 0;

		Debug.WriteLine($"Processing {totalLayers} layers for image generation");

		// レイヤーが存在しない場合は空の画像を返す
		if (totalLayers == 0)
		{
			progress?.Report(100);
			return finalBitmap;
		}

		// 並列読み込みと描画の分離
		// 1. まず全てのタスクを並列実行
		var layerResults = new List<(YmmPsdLayer Layer, Bitmap Bitmap)>(totalLayers);
		await Parallel.ForEachAsync(
			relevantLayers,
			new ParallelOptions
			{
				MaxDegreeOfParallelism = Environment.ProcessorCount,
			},
			async (layer, ct) =>
			{
				string cacheKey = layer.Cid;
				var layerBitmap = await GetOrCreateBitmapAsync(cacheKey, layer, ReadImageAsync);

				// ロックの取得を試みる（非同期版）
				await _resultLock.WaitAsync(ct).ConfigureAwait(false);
				try
				{
					layerResults.Add((layer, layerBitmap));

					// 進捗報告を減らしてオーバーヘッドを削減
					int progressStep = Math.Max(1, totalLayers / 20); // 全体の5%ごとに報告
					if (processedLayers % progressStep == 0 || processedLayers == totalLayers)
					{
						int progressValue = (int)((float)processedLayers / totalLayers * 100);
						progress?.Report(progressValue);
					}
				}
				finally
				{
					// 必ず解放する
					_resultLock.Release();
				}
			}
		);

		// 2. 描画順にソートして描画（PSDでは下から上に描画）
		var orderedResults = layerResults
			.OrderBy(item => relevantLayers.IndexOf(item.Layer))
			.ToList();

		// 3. Graphics オブジェクトを使った描画
		using var g = Graphics.FromImage(finalBitmap);

		g.Clear(Color.Transparent);
		g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighSpeed;
		g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighSpeed;
		g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.Low;
		g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighSpeed;
		g.PageUnit = GraphicsUnit.Pixel;  // ピクセル単位で処理

		foreach (var (layer, bitmap) in orderedResults)
		{
			using (bitmap)
			{
				g.DrawImageUnscaled(bitmap, layer.Record.Left, layer.Record.Top);
			}
		}

		_perfTimer.Stop();
		Debug.WriteLine($"Image generation completed in {_perfTimer.ElapsedMilliseconds}ms");
		progress?.Report(100);

		return finalBitmap;
	}

	/// <summary>
	/// 親フォルダーの状態を考慮したレイヤーの可視性を計算する
	/// </summary>
	/// <param name="layers">処理するレイヤーツリー</param>
	/// <param name="enabledLayerSet">enabledLayersのセット</param>
	/// <param name="visibleLayerSet">表示可能なレイヤーのIDを格納するセット</param>
	/// <param name="parentEnabled">親フォルダが表示可能かどうか</param>
	private static void CalculateLayerVisibility(
		IEnumerable<YmmPsdLayer> layers,
		HashSet<string> enabledLayerSet,
		HashSet<string> visibleLayerSet,
		bool parentEnabled
	)
	{
		foreach (var layer in layers)
		{
			// このレイヤー自体がenabledLayerSetに含まれているか
			bool isSelfEnabled = enabledLayerSet.Contains(layer.Cid);

			// このレイヤーが表示されるかどうか
			// - 親フォルダが表示され、かつ
			// - このレイヤー自身がenabledLayerSetに含まれている
			bool isLayerEnabled = parentEnabled && isSelfEnabled;

			// フォルダの場合、表示条件が満たされていれば子レイヤーにも伝搬
			bool childrenEnabled = isLayerEnabled;

			// このレイヤーが表示される場合はvisibleLayerSetに追加
			if (isLayerEnabled)
			{
				visibleLayerSet.Add(layer.Cid);
			}

			// 子レイヤーがある場合は再帰的に処理
			if (layer.Children.Any())
			{
				CalculateLayerVisibility(
					layer.Children,
					enabledLayerSet,
					visibleLayerSet,
					childrenEnabled
				);
			}
		}
	}

	/// <summary>
	/// 低解像度のサムネイルを素早く生成します
	/// </summary>
	[SuppressMessage("Usage", "SMA0040:Missing Using Statement", Justification = "<保留中>")]
	public static async Task<Bitmap> CreateThumbnailFromTreeAsync(
		IReadOnlyList<YmmPsdLayer> tree,
		int width,
		int height,
		IEnumerable<string>? enabledLayers,
		int maxThumbWidth = 400,
		int maxThumbHeight = 300
	)
	{
		// 大きく異なる場合は直接小さいサイズを生成
		double scaleFactor = Math.Min((double)maxThumbWidth / width, (double)maxThumbHeight / height);

		if (scaleFactor < 0.3)
		{
			// スケールが小さすぎる場合は直接小さいサイズで生成
			int thumbWidth = (int)(width * scaleFactor);
			int thumbHeight = (int)(height * scaleFactor);

			return await CreateImageFromTreeAsync(
				tree, thumbWidth, thumbHeight, enabledLayers, progress: null
			).ConfigureAwait(false);
		}
		else
		{
			// 縮小率の計算
			double scaleRatio = Math.Min(
				(double)maxThumbWidth / width,
				(double)maxThumbHeight / height
			);

			// スケール後のサイズ
			int thumbWidth = (int)(width * scaleRatio);
			int thumbHeight = (int)(height * scaleRatio);

			var thumbnail = new Bitmap(thumbWidth, thumbHeight);

			using var g = Graphics.FromImage(thumbnail);
			g.Clear(Color.Transparent);
			g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.Low;
			g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighSpeed;

			await CombineLayerRecursiveWithScaleAsync(tree, g, enabledLayers, scaleRatio)
				.ConfigureAwait(false);

			return thumbnail;
		}

	}

	[SuppressMessage("Usage", "SMA0040:Missing Using Statement", Justification = "<保留中>")]
	public static async Task<Bitmap> CreateImageFromLayerAsync(YmmPsdLayer layer)
	{
		// キャッシュキーを生成
		string cacheKey = layer.Cid;

		// キャッシュから取得を試みる
		if (
			_layerImageCache.TryGetValue(cacheKey, out var weakRef)
			&& weakRef.TryGetTarget(out var cachedBitmap)
		)
		{
			return new Bitmap(cachedBitmap); // キャッシュから複製を返す
		}

		// キャッシュになければ新規作成
		var pixelData = await ReadImageAsync(layer).ConfigureAwait(false);
		var bitmap = CreateBitmapFromPixelData(pixelData, layer.Image.Width, layer.Image.Height);

		// キャッシュに保存
		_layerImageCache[cacheKey] = new WeakReference<Bitmap>(bitmap);

		return new Bitmap(bitmap);
	}

	static async Task<byte[]> ReadImageAsync(YmmPsdLayer layer)
	{
		string cacheKey = $"raw_{layer.Cid}";

		// メモリ内キャッシュを先にチェック
		if (_rawDataCache.TryGetValue(cacheKey, out var cachedData))
		{
			return cachedData;
		}

		await _lock.WaitAsync().ConfigureAwait(false);
		try
		{
			// ダブルチェック（他のスレッドがすでに読み込んだ可能性）
			if (_rawDataCache.TryGetValue(cacheKey, out cachedData))
			{
				return cachedData;
			}

			var sw = Stopwatch.StartNew();
			byte[] pixelData = await Task.Run(() => layer.Image.Read()).ConfigureAwait(false);
			sw.Stop();

			// キャッシュに保存
			_rawDataCache[cacheKey] = pixelData;

			return pixelData;
		}
		finally
		{
			_lock.Release();
		}
	}

	// 縮小描画対応版のCombineLayerRecursiveAsync
	static async ValueTask CombineLayerRecursiveWithScaleAsync(
		IEnumerable<YmmPsdLayer> layers,
		Graphics g,
		IEnumerable<string>? enabledLayers,
		double scaleRatio = 1.0
	)
	{
		// PSDレイヤーを描画順にフラットなリストとして収集する
		var flattenedLayers = FlattenLayersInDrawOrder(layers).ToList();
		var enabledLayerSet =
			enabledLayers?.ToHashSet(StringComparer.Ordinal)
			?? new HashSet<string>(StringComparer.Ordinal);

		// レイヤーを一度に処理する（最下層から最上層へ）
		foreach (var layer in flattenedLayers)
		{
			if (layer.IsFolder)
				continue; // フォルダはスキップ
			if (layer.Image.Width == 0 || layer.Image.Height == 0)
				continue;

			// レイヤーが有効かチェック
			if (enabledLayerSet.Count > 0 && !enabledLayerSet.Contains(layer.Cid))
			{
				continue;
			}

			// レイヤーの描画処理
			var pixelData = await ReadImageAsync(layer).ConfigureAwait(false);

			using var layerBitmap = CreateBitmapFromPixelData(
				pixelData,
				layer.Image.Width,
				layer.Image.Height
			);

			// 縮小サイズで描画
			g.DrawImage(
				layerBitmap,
				(int)(layer.Record.Left * scaleRatio),
				(int)(layer.Record.Top * scaleRatio),
				(int)(layer.Image.Width * scaleRatio),
				(int)(layer.Image.Height * scaleRatio)
			);
		}
	}

	// レイヤーを描画順（下から上）にフラット化する
	static IEnumerable<YmmPsdLayer> FlattenLayersInDrawOrder(IEnumerable<YmmPsdLayer> layers)
	{
		// PSDでは下から上に描画するため、逆順にする
		foreach (var layer in layers.Reverse())
		{
			if (layer.IsFolder && layer.Children.Any())
			{
				// フォルダ内のレイヤーを先に処理
				foreach (var childLayer in FlattenLayersInDrawOrder(layer.Children))
				{
					yield return childLayer;
				}
			}

			// フォルダも含めてすべてのレイヤーを返す
			// （後でフィルタリングするため）
			yield return layer;
		}
	}

	static Bitmap CreateBitmapFromPixelData(byte[] pixelData, int width, int height)
	{
		// ピクセルデータのサイズが大きい場合は解像度を下げる
		const int maxPixels = 4000 * 3000; // 適切なしきい値

		if (width * height > maxPixels)
		{
			// 大きすぎる画像は解像度を下げる
			double scale = Math.Sqrt((double)maxPixels / (width * height));
			int newWidth = (int)(width * scale);
			int newHeight = (int)(height * scale);

			Debug.WriteLine(
				$"Downscaling large image from {width}x{height} to {newWidth}x{newHeight}"
			);

			var scaledBitmap = new Bitmap(newWidth, newHeight, PixelFormat.Format32bppArgb);

			// 元のビットマップを生成
			var originalBitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
			var rect = new Rectangle(0, 0, width, height);
			var data = originalBitmap.LockBits(
				rect,
				ImageLockMode.WriteOnly,
				originalBitmap.PixelFormat
			);
			Marshal.Copy(
				pixelData,
				0,
				data.Scan0,
				Math.Min(pixelData.Length, Math.Abs(data.Stride) * height)
			);
			originalBitmap.UnlockBits(data);

			// 縮小
			using (Graphics g = Graphics.FromImage(scaledBitmap))
			{
				g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
				g.DrawImage(originalBitmap, 0, 0, newWidth, newHeight);
			}

			originalBitmap.Dispose(); // 元の大きなビットマップを破棄
			return scaledBitmap;
		}

		// 通常サイズの場合は既存のコード
		var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
		var rectNormal = new Rectangle(0, 0, width, height);
		var dataNormal = bitmap.LockBits(rectNormal, ImageLockMode.WriteOnly, bitmap.PixelFormat);
		Marshal.Copy(
			pixelData,
			0,
			dataNormal.Scan0,
			Math.Min(pixelData.Length, Math.Abs(dataNormal.Stride) * height)
		);
		bitmap.UnlockBits(dataNormal);

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
			string visible = layer.IsVisible ? "👀" : "";
			Debug.WriteLine($"{indent}{icon}{visible}[{layer.Cid}]{layer.Name}");

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
			//layer id がある場合は「iXX」,なければ「nXX」
			var infos = layer.Record.AdditionalLayerInformations.OfType<LayerID>();
			var cid = infos.Any() ? $"i{infos.First().Id}" : $"n{index--}";
			var node = new YmmPsdLayer(cid, layer);

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
		return layer.Record.AdditionalLayerInformations.OfType<SectionDividerSetting>().Any();
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
			.Record.AdditionalLayerInformations.OfType<SectionDividerSetting>()
			.First()
			.Type;
	}

	[SuppressMessage(
		"Design",
		"MA0016:Prefer using collection abstraction instead of implementation",
		Justification = "<保留中>"
	)]
	public static Dictionary<string, bool> GetVisibilityFromTree(IEnumerable<YmmPsdLayer>? tree)
	{
		if (tree is null)
			return [];

		var result = new Dictionary<string, bool>(StringComparer.Ordinal);

		void Traverse(YmmPsdLayer layer)
		{
			if (!result.TryGetValue(layer.Cid, out bool isVisible))
			{
				result[layer.Cid] = layer.IsVisible;
			}
			else
			{
				result[layer.Cid] = isVisible || layer.IsVisible; // 一つでも true なら true
			}

			foreach (var child in layer.Children)
			{
				Traverse(child);
			}
		}

		foreach (var layer in tree)
		{
			Traverse(layer);
		}

		return result;
	}

	// キャッシュからのBitmap取得または生成
	private static async Task<Bitmap> GetOrCreateBitmapAsync(
		string cacheKey,
		YmmPsdLayer layer,
		Func<YmmPsdLayer, Task<byte[]>> pixelDataProvider
	)
	{
		// キャッシュクリーンアップチェック
		CleanupCacheIfNeeded();

		// キャッシュを確認
		if (_pixelDataCache.TryGetValue(cacheKey, out var cachedData))
		{
			try
			{
				// キャッシュヒット - 最終アクセス日時を更新
				_pixelDataCache[cacheKey] = (
					cachedData.PixelData,
					cachedData.Width,
					cachedData.Height,
					DateTime.UtcNow
				);

				// 新しいBitmapを作成（他のスレッドとの共有なし）
				return CreateBitmapFromPixelData(
					cachedData.PixelData,
					cachedData.Width,
					cachedData.Height
				);
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"Error creating bitmap from cached data: {ex.Message}");
				// キャッシュから削除し再読み込み
				_pixelDataCache.TryRemove(cacheKey, out _);
			}
		}

		// キャッシュにない場合は新たに読み込み
		var pixelData = await pixelDataProvider(layer).ConfigureAwait(false);

		// すべてのピクセルが透明の場合は再読み込みを試みる
		if (pixelData.All(p => p == 0))
		{
			Debug.WriteLine($"Layer {layer.Name} is completely transparent, retrying...");
			pixelData = await pixelDataProvider(layer).ConfigureAwait(false);
		}

		// キャッシュに保存
		_pixelDataCache[cacheKey] = (
			pixelData,
			layer.Image.Width,
			layer.Image.Height,
			DateTime.UtcNow
		);

		// 新しいBitmapを作成
		return CreateBitmapFromPixelData(pixelData, layer.Image.Width, layer.Image.Height);
	}

	// キャッシュクリーンアップ処理
	private static void CleanupCacheIfNeeded()
	{
		var now = DateTime.UtcNow;
		if (now - _lastCacheCleanup < _cacheCleanupInterval)
			return;

		_lastCacheCleanup = now;

		// 古いエントリを削除
		var keysToRemove = _pixelDataCache
			.Where(kvp => now - kvp.Value.LastAccess > _cacheEntryMaxAge)
			.Select(kvp => kvp.Key)
			.ToList();

		foreach (var key in keysToRemove)
		{
			_pixelDataCache.TryRemove(key, out _);
			Debug.WriteLine($"Removed expired cache entry: {key}");
		}

		Debug.WriteLine(
			$"Cache cleanup: removed {keysToRemove.Count} entries, {_pixelDataCache.Count} remaining"
		);
	}
}
