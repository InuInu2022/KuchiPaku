using Epoxy;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace KuchiPaku.ViewModels;

[ViewModel]
public class LipSyncImageViewModel
{
	public string Id { get; set; }
	public string? Name { get; set; }
	public ObservableCollection<LipSyncImageLineViewModel> ImageList { get; set; }

	public string CharacterName { get; set; }
	public string CharacterDir { get; set; }
	public LipSyncImageLineViewModel? SelectedLipSyncItem { get; set; }
	public MainWindowViewModel MainWindowVM { get; set; }

	public int SelectedItemIndex { get; set; }

	public LipSyncImageViewModel(
		string id,
		string? name,
		ObservableCollection<LipSyncImageLineViewModel> imageList,
		string characterDir,
		string characterName,
		MainWindowViewModel mainWindowVM,
		int selectedIndex,
		LipSyncImageLineViewModel? selectedLipSyncItem = null
	)
	{
		ImageList = new ObservableCollection<LipSyncImageLineViewModel>();
		Id = id;
		Name = name;
		ImageList = imageList;
		CharacterDir = characterDir;
		MainWindowVM = mainWindowVM;
		CharacterName = characterName;
		SelectedItemIndex = selectedIndex;
	}

	[PropertyChanged(nameof(SelectedItemIndex))]
	private async ValueTask SelectedItemIndexChangedAsync(int index){
		var selected = ImageList[index] ?? ImageList[0];
		Debug.WriteLine($"SelectedLipSyncItem: {Name} {selected.ImageName}");

		if(MainWindowVM?.LipSyncSettings is null)return;
		if(!MainWindowVM.LipSyncSettings.ContainsKey(CharacterName))return;

		MainWindowVM
			.LipSyncSettings[CharacterName]
			.MousePhenomeImagePair[Id]
			= Path.GetFileName(selected.Path)!;

		Debug.WriteLine($"RipSyncSettings[{CharacterName}]:[{Id}]:{Path.GetFileName(selected.Path)!}");
	}

	[PropertyChanged(nameof(SelectedLipSyncItem))]
	private async ValueTask SelectedLipSyncItemChangedAsync(LipSyncImageLineViewModel item){
		Debug.WriteLine($"SelectedLipSyncItem: {Name} {item.ImageName}");

		if(MainWindowVM?.LipSyncSettings is null)return;

		if(!MainWindowVM.LipSyncSettings.ContainsKey(CharacterName))return;

		MainWindowVM
			.LipSyncSettings[CharacterName]
			.MousePhenomeImagePair[Id]
			= await Task.Run(()=>Path.GetFileName(item.Path)!);

		Debug.WriteLine($"RipSyncSettings[{CharacterName}]:[{Id}]:{Path.GetFileName(item.Path)!}");
	}

}

[ViewModel]
public class LipSyncImageLineViewModel
{
	public LipSyncImageLineViewModel(string? imageName, string? path)
	{
		ImageName = imageName;
		Path = path;
	}

	public string? ImageName { get; set; }
	public string? Path { get; set; }

	public ImageSource? ImageSrc { get; set; }

	[PropertyChanged(nameof(Path))]
	private async ValueTask PathChangedAsync(string path){
		if(string.IsNullOrEmpty(path)){return;}

		var bitmap = await Task.Run(() =>
		{
			// Drawing.Image => Bitmap 変換
			return new System.Drawing.Bitmap(path);
		});
		var rect = await Task.Run(
			() => ThumbUtil.GetNoTransRect(bitmap)
		);

		BitmapImage bi = new();
		// BitmapImage.UriSource must be in a BeginInit/EndInit block.
		bi.BeginInit();
		bi.CacheOption = BitmapCacheOption.OnDemand;
		bi.CreateOptions = BitmapCreateOptions.DelayCreation;

		bi.UriSource = new Uri(path, UriKind.RelativeOrAbsolute);
		bi.SourceRect = new Int32Rect(rect.X,rect.Y,rect.Width,rect.Height);

		bi.EndInit();

		ImageSrc = bi;
	}
}

public class LipSyncLine
{
	public LipSyncLine(
		string id,
		string name,
		IEnumerable<LipSyncImageLineViewModel> images,
		string characterDir
	)
	{
		Id = id;
		Name = name;
		CharacterDir = characterDir;
		Images = images;
	}

	public string Id { get; set; }
    public string Name { get; set; }
    public IEnumerable<LipSyncImageLineViewModel> Images { get; set; }
	public string CharacterDir { get; set; }

	public LipSyncImageLineViewModel? SelectedImage { get; set; }

}