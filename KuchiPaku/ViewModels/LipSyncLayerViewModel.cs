using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

using Epoxy;

using KuchiPaku.Psd;

namespace KuchiPaku.ViewModels;

[ViewModel]
public class LipSyncLayerViewModel
{
	public string Id { get; init; }
	public string? Name { get; init; }
	public ImageSource? ImageSrc { get; set; }
	public ImageSource? FullImageSrc { get; set; }
	public MainWindowViewModel MainWindowVM { get; init; }
	public IReadOnlyList<YmmPsdLayer> LayerTree { get; init; }

	public Dictionary<string, bool> FolderOpenedList { get; set; } = [];
	public Dictionary<string, bool> OverrideLayerList { get; set; }
	= [];
	public Dictionary<string, bool> VisibleLayerList { get; set; } = [];

	public Well<System.Windows.Controls.Image> ThumbImageWell { get; }
		= Well.Factory.Create<System.Windows.Controls.Image>();

	public bool IsLoading { get; set; }

	Rectangle PsdRect { get; set; }
	int PsdWidth { get; init; }
	int PsdHeight { get; init; }

	public LipSyncLayerViewModel(
		string id,
		string name,
		string characterName,
		MainWindowViewModel mainVM,
		IReadOnlyList<YmmPsdLayer> layerTree,
		Rectangle psdRect,
		Dictionary<string, bool> defaultVisible
	)
	{
		Id = id;
		Name = name;
		MainWindowVM = mainVM;
		LayerTree = layerTree;
		PsdRect = psdRect;
		PsdWidth = psdRect.Width;
		PsdHeight = psdRect.Height;
		VisibleLayerList = defaultVisible;

		//override default visible
		SetVisibleLayersFromSavedOptions(characterName);

		ThumbImageWell.Add("Loaded", async () =>
		{
			if (ImageSrc is not null) return;
			IsLoading = true;
			var st = Stopwatch.StartNew();

			await ShowThumbAsync(VisibleLayerList.Where(v => v.Value).Select(v => v.Key));

			IsLoading = false;
			st.Stop();
		});
	}

	void SetVisibleLayersFromSavedOptions(string characterName)
	{
		if (
			MainWindowVM.LipSyncSettings.TryGetValue(characterName, out var savedOption)
			&&
			savedOption.MousePhonemeLayerPair.TryGetValue(Id, out var layers)
			&&
			layers is not null
		)
		{
			if (!layers.Any()) return;

			VisibleLayerList = VisibleLayerList
				.Select(v => (v.Key, Value:false))
				.ToDictionary(v => v.Key, v => v.Value, StringComparer.Ordinal);

			layers
				.Where(layer => VisibleLayerList.ContainsKey(layer))
				.ToList()
				.ForEach(layer => VisibleLayerList[layer] = true);
		}
	}

	public async Task ShowThumbAsync(
		IEnumerable<string> enabledLayers
	)
	{
		var st = Stopwatch.StartNew();

		using var bmp = await PsdUtil
			.CreateImageFromTreeAsync(LayerTree, PsdWidth, PsdHeight, enabledLayers);
		var rate = 250.0 / Math.Max(bmp.Width, bmp.Height);
		var rw = (int)(bmp.Width * rate);
		var rh = (int)(bmp.Height * rate);
		using var thumb = bmp.GetThumbnailImage(rw, rh, null, IntPtr.Zero);
		var bitmapImage = ConvertToImageSource(thumb);

		var noRect = ThumbUtil.GetNoTransRect(bitmapImage.ToBitmap());
		var noFull = new CroppedBitmap(bitmapImage, new(
			noRect.X, noRect.Y, noRect.Width, noRect.Height
		));

		var sourceRect = new Int32Rect(
			noRect.X,
			noFull.PixelHeight * noRect.Width / noRect.Height / 4,
			noFull.PixelWidth,
			noFull.PixelHeight * noRect.Width / noRect.Height * 3 / 5
		);
		var cropped = new CroppedBitmap(bitmapImage, sourceRect);

		FullImageSrc = new WriteableBitmap(noFull);
		ImageSrc = new WriteableBitmap(cropped);
	}

	static BitmapImage ConvertToImageSource(System.Drawing.Image bitmap)
	{
		using var memoryStream = new MemoryStream();
		bitmap.Save(memoryStream, ImageFormat.Png);
		memoryStream.Seek(0, SeekOrigin.Begin);

		var bitmapImage = new BitmapImage();
		bitmapImage.BeginInit();
		bitmapImage.StreamSource = memoryStream;
		bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
		bitmapImage.EndInit();
		bitmapImage.Freeze();

		return bitmapImage;
	}

	[Conditional("DEBUG")]
	static void DebugSave(TransformedBitmap bmp)
	{
		var savePath = Path.Combine(
			Path.GetTempPath(),
			Path.GetRandomFileName() + ".png");
		//bmp.Save(savePath, ImageFormat.Png);

		var encoder = new PngBitmapEncoder();
		encoder.Frames.Add(BitmapFrame.Create(bmp));

		using var fileStream = new FileStream(savePath, FileMode.Create);
		encoder.Save(fileStream);

		Debug.WriteLine(savePath);
	}
}
