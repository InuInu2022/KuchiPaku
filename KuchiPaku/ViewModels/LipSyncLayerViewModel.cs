using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

using Epoxy;

using KuchiPaku.Psd;

namespace KuchiPaku.ViewModels;

[ViewModel]
public class LipSyncLayerViewModel(
	string id,
	string name,
	MainWindowViewModel mainVM,
	IReadOnlyList<YmmPsdLayer> layerTree,
	Rectangle psdRect
)
{
	public string Id { get; init; } = id;
	public string? Name { get; init; } = name;
	public ImageSource? ImageSrc { get; set; }
	public MainWindowViewModel MainWindowVM { get; init; } = mainVM;
	public IReadOnlyList<YmmPsdLayer> LayerTree { get; init; } = layerTree;

	Rectangle PsdRect { get; set; } = psdRect;

	public void ShowLayer()
	{
		using var bmp = PsdUtil.CreateImageFromTree(LayerTree, PsdRect.Width, PsdRect.Height);
		var bitmapImage = ConvertToImageSource(bmp);
		var resized = new TransformedBitmap(
			bitmapImage,
			//new ScaleTransform(100 / PsdRect.Width, 25 / PsdRect.Height)
			new ScaleTransform(0.1,0.1)
		);
		var w = (double)100 / PsdRect.Width;
		var h = (double)25 / PsdRect.Height;

		var sourceRect = new Int32Rect(
			0,
			(int)resized.PixelHeight / 10,
			(int)resized.PixelWidth,
			35 //(int)(resized.Height / 4)*3
		);
		var cropped = new CroppedBitmap(resized, sourceRect);

		var thumb = new WriteableBitmap(cropped);
		ImageSrc = thumb;
	}

	static BitmapImage ConvertToImageSource(Bitmap bitmap)
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
