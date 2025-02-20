using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Epoxy;
using KuchiPaku.Psd;

namespace KuchiPaku.ViewModels;

[ViewModel]
public class SelectedLayerViewModel
{
	public SelectedLayerViewModel(
		YmmPsdLayer layer,
		MainWindowViewModel vm
	)
	{
		Layer = layer;
		Children = [.. layer.Children.Select(x => new SelectedLayerViewModel(x, vm))];
		IsVisible = layer.IsVisible;
		MainVM = vm;
		IsFolderOpened = IsOpen();
		IsOverrideDefaultVisibility = IsOverride();

		IsPsdToolRadioOption = layer.Name.StartsWith('*');
		IsPsdToolForceDisplay = layer.Name.StartsWith('!');

		_isInitialized = true;

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

					_isLoaded = true;
				}
			);
		}
		else
		{
			ThumbImageWell.Add(
				"Loaded",
				() =>
				{
					_isLoaded = true;
					return default;
				}
			);
		}
	}



	public YmmPsdLayer Layer { get; init; }

	public string Name => Layer.Name;

	public string Cid => Layer.Cid;

	public bool IsVisible { get; set; }
	public bool IsFolder => Layer.IsFolder;

	public bool IsFolderOpened { get; set; }
	public List<SelectedLayerViewModel> Children { get; init; }
	public ImageSource? Image { get; set; }

	public string ParentName => Layer.Parent?.Name ?? "RootGroup";

	public bool IsPsdToolMode => MainVM.IsPsdToolMode;

	public bool IsPsdToolRadioOption { get; init; }
	public bool IsPsdToolForceDisplay { get; init; }

	public bool IsOverrideDefaultVisibility { get; set; }

	public Well<System.Windows.Controls.Image> ThumbImageWell { get; } =
		Well.Factory.Create<System.Windows.Controls.Image>();

	bool IsSizeZero
		=> Layer.Image.Width == 0 || Layer.Image.Height == 0;

	bool _isLoaded;
	bool _isInitialized;
	MainWindowViewModel MainVM { get; init; }

	bool IsOverride()
	{
		var list = MainVM.SelectedLayers?.OverrideLayerList;
		return list is not null && list.TryGetValue(Cid, out var result) && result;
	}

	bool IsOpen()
	{
		var list = MainVM.SelectedLayers?.FolderOpenedList;
		return list is not null && list.TryGetValue(Cid, out var result) && result;
	}

	[PropertyChanged(nameof(IsVisible))]
	[SuppressMessage("", "IDE0051")]
	private ValueTask IsVisibleChangedAsync(bool value)
	{
		if (!_isLoaded) return default;

		Layer.IsVisible = value;
		Debug.WriteLine($"Layer {Name} [{Cid}].IsVisible : {Layer.IsVisible}");

		//再描画
		MainVM?.SelectedLayers?.ShowThumb();

		return default;
	}

	[PropertyChanged(nameof(IsOverrideDefaultVisibility))]
	[SuppressMessage("", "IDE0051")]
	private ValueTask IsOverrideDefaultVisibilityChangedAsync(bool value)
	{
		if (!_isInitialized) return default;

		if (MainVM.SelectedLayers is not null)
		{
			MainVM
				.SelectedLayers
				.OverrideLayerList
				.TryAdd(Cid, value);
		}

		if (Children.Count == 0) return default;
		foreach (var child in Children)
		{
			child.IsOverrideDefaultVisibility = value;
		}

		return default;
	}

	[PropertyChanged(nameof(IsFolderOpened))]
	[SuppressMessage("","IDE0051")]
	private ValueTask IsFolderOpenedChangedAsync(bool value)
	{
		if (!IsFolder) return default;
		if (!_isInitialized) return default;

		if (MainVM.SelectedLayers is not null)
		{
			MainVM
				.SelectedLayers
				.FolderOpenedList
				.TryAdd(Cid, value);
			;
		}

		return default;
	}
}
