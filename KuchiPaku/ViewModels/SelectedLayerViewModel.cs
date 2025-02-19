using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Epoxy;
using KuchiPaku.Psd;

namespace KuchiPaku.ViewModels;

[ViewModel]
public class SelectedLayerViewModel
{
	public SelectedLayerViewModel(YmmPsdLayer layer)
	{
		Layer = layer;
		Children = [.. layer.Children.Select(x => new SelectedLayerViewModel(x))];
		IsVisible = layer.IsVisible;

		if (!IsFolder && !IsSizeZero)
		{
			ThumbImageWell.Add(
				"Loaded",
				async () =>
				{
					if (Image is not null) return;

					using var bmp = await PsdUtil.CreateImageFromLayerAsync(Layer);
					var rate = 36.0 / Math.Max(bmp.Width, bmp.Height);
					var rw = (int)(bmp.Width * rate);
					var rh = (int)(bmp.Height * rate);
					var thumb = bmp.GetThumbnailImage(rw, rh, null, IntPtr.Zero);

					using var memoryStream = new MemoryStream();
					thumb.Save(memoryStream, System.Drawing.Imaging.ImageFormat.Png);
					memoryStream.Seek(0, SeekOrigin.Begin);

					var bitmapImage = new BitmapImage();
					bitmapImage.BeginInit();
					bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
					bitmapImage.StreamSource = memoryStream;
					bitmapImage.EndInit();
					bitmapImage.Freeze(); // UIスレッド以外でも使用可能にする

					Image = bitmapImage;
				}
			);
		}
	}

	public YmmPsdLayer Layer { get; init; }

	public string Name => Layer.Name;
	public bool IsVisible { get; set; }
	public bool IsFolder => Layer.IsFolder;
	public List<SelectedLayerViewModel> Children { get; init; }
	public ImageSource? Image { get; set; }

	public Well<System.Windows.Controls.Image> ThumbImageWell { get; } =
		Well.Factory.Create<System.Windows.Controls.Image>();

	bool IsSizeZero
		=> Layer.Image.Width == 0 || Layer.Image.Height == 0;
}
