using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading.Tasks;
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
		var resized = new TransformedBitmap(
			ConvertToImageSource(bmp),
			new ScaleTransform(0.05, 0.05)
		);
		ImageSrc = resized;
	}

	static BitmapImage ConvertToImageSource(Bitmap bitmap)
	{
		using var memoryStream = new MemoryStream();
		bitmap.Save(memoryStream, ImageFormat.Png);
		memoryStream.Seek(0, SeekOrigin.Begin);

		var bitmapImage = new BitmapImage();
		bitmapImage.BeginInit();
		bitmapImage.StreamSource = memoryStream;
		bitmapImage.EndInit();

		return bitmapImage;
	}
}
