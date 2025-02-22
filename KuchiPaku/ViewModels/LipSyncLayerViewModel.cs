using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
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

	Rectangle PsdRect { get; set; }
	int PsdWidth { get; init; }
	int PsdHeight { get; init; }

	public LipSyncLayerViewModel(
		string id,
		string name,
		MainWindowViewModel mainVM,
		IReadOnlyList<YmmPsdLayer> layerTree,
		Rectangle psdRect
	)
	{
		Id = id;
		Name = name;
		MainWindowVM = mainVM;
		LayerTree = layerTree;
		PsdRect = psdRect;
		PsdWidth = psdRect.Width;
		PsdHeight = psdRect.Height;

		ThumbImageWell.Add("Loaded", async () =>
		{
			if (ImageSrc is not null) return;
			var w = PsdRect.Width;
			var h = PsdRect.Height;
			ShowThumb();
			//ShowLayer();
		});
	}

	public void ShowThumb()
	{
		using var bmp = PsdUtil.CreateImageFromTree(LayerTree, PsdWidth, PsdHeight);
		var rate = 200.0 / Math.Max(bmp.Width, bmp.Height);
		var rw = (int)(bmp.Width * rate);
		var rh = (int)(bmp.Height * rate);
		var thumb = bmp.GetThumbnailImage(rw, rh, null, IntPtr.Zero);
		var bitmapImage = ConvertToImageSource(thumb);

		/*
		var resized = new TransformedBitmap(
			bitmapImage,
			//new ScaleTransform(100 / PsdRect.Width, 25 / PsdRect.Height)
			new ScaleTransform(0.1, 0.1)
		);
		*/

		var sourceRect = new Int32Rect(
			0,
			(int)bitmapImage.PixelHeight / 10,
			(int)bitmapImage.PixelWidth,
			35 //(int)(resized.Height / 4)*3
		);
		var cropped = new CroppedBitmap(bitmapImage, sourceRect);

		FullImageSrc = new WriteableBitmap(bitmapImage);
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
