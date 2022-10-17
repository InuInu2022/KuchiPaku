////////////////////////////////////////////////////////////////////////////
//
// Epoxy template source code.
// Write your own copyright and note.
// (You can use https://github.com/rubicon-oss/LicenseHeaderManager)
//
////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using System.Windows;

using Microsoft.WindowsAPICodePack.Dialogs;

using Epoxy;

using KuchiPaku.Models;
using System.Diagnostics;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Enterwell.Clients.Wpf.Notifications;
using NLog;
using System.Windows.Threading;

namespace KuchiPaku.ViewModels;

public enum Page{
    Home
}

[ViewModel]
[DebuggerDisplay("{DebuggerDisplay,nq}")]
public sealed class MainWindowViewModel
{
	private static readonly Logger logger = LogManager.GetCurrentClassLogger();
	public string WindowTitle { get; set; }
	public INotificationMessageManager Manager { get; set; }
	public Command? OpenYmmp { get; set; }
	public string? TargetYmmpFileName { get; set; }

	public ObservableCollection<CharacterListViewModel>? Characters { get; set; }

	public CharacterListViewModel? SelectedCharaItem { get; set; }

	public ObservableCollection<LipSyncImageViewModel>? LipSyncImages { get; set; }

	public ConsonantOption CurrentConsonantOption { get; set; } = ConsonantOption.CONTINUE_BEFORE_VOWEL;
	public IEnumerable<ConsonantOption> ConsonantOptionList { get; set; }
			= Enum.GetValues(typeof(ConsonantOption)).Cast<ConsonantOption>();

	public Command? SaveYmmp { get; set; }
	public Command? OpenLicenses { get; set; }

	public Command? OpenWebsite { get; set; }
	public Command? OpenYMM4Website { get; set; }

	public bool IsSaveBackup { get; set; } = true;
	public bool IsOpenWithSave { get; set; } = true;

	private JObject? CurrentYmmp { get; set; }

	private string? CurrentYmmpPath { get; set; }

	private int CurrentYmmpFPS { get; set; } = 30;

	public Dictionary<string, LipSyncOption> LipSyncSettings { get; set; } = new();

	[DebuggerBrowsable(DebuggerBrowsableState.Never)]
	private string? DebuggerDisplay => ToString();

	public MainWindowViewModel()
	{
		WindowTitle = AppUtil.GetWindowTitle();
		Manager = new NotificationMessageManager();
		Characters = new ObservableCollection<CharacterListViewModel>();
		LipSyncImages = new ObservableCollection<LipSyncImageViewModel>();
		LipSyncSettings = new();

		KuchiPaku.Core.Models.ConfigUtil.LoadConfig();
		var settings = KuchiPaku.Core.Models.ConfigUtil.Settings;
		foreach(var i in settings!.TalkSoftInterfaces!){
			Debug.WriteLine($"{i.Type}:{i.DllPath}");
		}

		OpenYmmp = Command.Factory.Create<RoutedEventArgs>(async _ =>
		{
			using var cofd = new CommonOpenFileDialog()
			{
				Title = "口パクさせるYMM4プロジェクトファイルを選んでください。",
				RestoreDirectory = true,
				IsFolderPicker = false,
			};
			cofd.Filters
				.Add(new CommonFileDialogFilter("YMM4プロジェクトファイル", "*.ymmp"));
			if (cofd.ShowDialog() != CommonFileDialogResult.Ok)
			{
				return;
			}

			Debug.WriteLine($"{cofd.FileName}");
			TargetYmmpFileName = Path.GetFileName(cofd.FileName);

			var json = await YmmpUtil.ReadAsync(cofd.FileName);
			CurrentYmmp = json;
			CurrentYmmpPath = cofd.FileName;
			var fps = YmmpUtil.GetFPS(CurrentYmmp);
			CurrentYmmpFPS = fps;

			var ymmChara = await YmmpUtil.ParseCharactersAsync(CurrentYmmp);
			var viewList = ymmChara
				.Where(v => v.TachieCharacterParameter is not null)
				//TODO:support psd tachie
				.Where(v => v.TachieType is not YmmpTachieType.PsdTachie)
				.Select(v => new CharacterListViewModel
					{
						Name = v.Name,
						DirectoryPath = v.TachieCharacterParameter.Directory,
						DefaultMouthImgPath = v.TachieDefaultItemParameter.Mouth
					}
				);

			LipSyncSettings.Clear();
			viewList
				.ToList()
				.ForEach(v =>
				{
					LipSyncSettings.Add(
						v.Name ?? "",
						LipSyncOption.GetDefault(
							v.Name ?? "",
							Path.Combine(
								v.DirectoryPath ?? "",
								"口"
							)
								?? "",
							v.DefaultMouthImgPath
						)
					);
				});

			Characters.Clear();
			Characters = new ObservableCollection<CharacterListViewModel>(viewList);
		});

		SaveYmmp = Command.Factory.Create((Func<RoutedEventArgs, ValueTask>)(async _ =>
		{
			if (CurrentYmmp is null)
			{
				Manager.Warn(
					"YMM4プロジェクトが読み込まれていません",
					"YMM4プロジェクトファイル(.ymmp)を最初に読み込んでください。"
				);
				return;
			}
			var sw = new System.Diagnostics.Stopwatch();
			sw.Start();

			var loading = Manager.Loading("保存中", "保存しています…");

			var ymmp = await YmmpUtil.CopyDeepAsync(CurrentYmmp);

			var voiceItems = await YmmpUtil.ParseVoiceItemsAsync(ymmp);

			sw.Stop();
			Debug.WriteLine($"TIME[ParseVoiceItemsAsync]:{sw.ElapsedMilliseconds.ToString()}");
			sw.Restart();

			if (voiceItems is null)
			{
				Manager.Dismiss(loading);
				Manager.Info("ボイスアイテムなし","対象のボイスアイテムがありませんでした。");
				return;
			}

			var maxLayer = YmmpUtil.GetMaxLayer(ymmp);
			Debug.WriteLine($"MaxLayer: {maxLayer.ToString()}");

			//カスタムボイス
			//カスタムボイスは同じ場所の.labファイルを探して口パク生成
			var customVoices = await YmmpUtil.FilterCustomVoiceAsync(voiceItems);

			sw.Stop();
			Debug.WriteLine($"TIME[FilterCustomVoiceAsync]:{sw.ElapsedMilliseconds.ToString()}");
			sw.Restart();

			var name = SelectedCharaItem?.Name ?? "";
			//var hasRipSyncSet = RipSyncSettings?.ContainsKey(name) ?? false;

			try
			{
				MakeCustomVoiceFaceItem(
					maxLayer,
					customVoices,
					ymmp,
					LipSyncSettings,
					CurrentYmmpFPS
				);
			}
			catch (System.Exception e)
			{
				logger.Error(e);
				Manager.Dismiss(loading);
				return;
			}

			sw.Stop();
			Debug.WriteLine($"TIME[MakeCustomVoiceFaceItem]:{sw.ElapsedMilliseconds.ToString()}");
			sw.Restart();

			//APIボイス
			maxLayer = YmmpUtil.GetMaxLayer(ymmp);
			var apiVoices = await YmmpUtil.FilterAPIVoiceAsync(voiceItems);

			//APIボイスの口パク生成
			Debug.WriteLine(nameof(YmmpUtil.MakeAPIVoiceFaceItemAsync));
			//apiVoices.Select(v => v.)
			//await Core.Models.TTSUtil.AwakeTTSAsync(Core.Models.TTSProduct.CeVIO_AI);
			try
			{
				await YmmpUtil.MakeAPIVoiceFaceItemAsync(
					maxLayer,
					apiVoices,
					ymmp,
					LipSyncSettings,
					CurrentYmmpFPS);
			}
			catch (System.Exception e)
			{
				Debug.WriteLine($"ERROR:{e.Message}");
				logger.Error(e);
				Manager.Dismiss(loading);
				return;
			}

			sw.Stop();
			Debug.WriteLine($"TIME[FilterAPIVoiceAync]:{sw.ElapsedMilliseconds.ToString()}");

			//出力
			var dir = Directory.Exists(CurrentYmmpPath)
				? Path.GetDirectoryName(CurrentYmmpPath)!
				: AppDomain.CurrentDomain.BaseDirectory;
			using var csfd = new CommonSaveFileDialog()
			{
				Title = "保存するYMM4プロジェクトファイルを選択",
				RestoreDirectory = true,
				DefaultDirectory = dir,
				DefaultFileName =
					Path.GetFileNameWithoutExtension(CurrentYmmpPath)
						+ (IsSaveBackup ? ".tmp" : "")
						+ Path.GetExtension(CurrentYmmpPath),
			};
			csfd.Filters
				.Add(new CommonFileDialogFilter("YMM4プロジェクトファイル", "*.ymmp"));

			try
			{
				if (csfd.ShowDialog() != CommonFileDialogResult.Ok)
				{
					Manager.Dismiss(loading);
					return;
				}
			}
			catch (System.Exception e)
			{
				//Manager.Warn(e.Message, e.StackTrace ?? "no stack");
				logger.Info(dir);
				logger.Error(e);
				return;
			}

			sw.Restart();
			await YmmpUtil.SaveAsync(ymmp, csfd.FileName);

			Manager.Dismiss(loading);
			Manager.Info("保存成功", "保存しました", true);

			sw.Stop();
			Debug.WriteLine($"TIME[SaveAsync]:{sw.ElapsedMilliseconds.ToString()}");

			if(IsOpenWithSave){
				await OpenAsync(csfd.FileName);
			}

		}));

		//open license folder
		OpenLicenses = CommandFactory.Create<RoutedEventArgs>(async _ =>
		{
			var path = Path.Combine(
				AppDomain.CurrentDomain.BaseDirectory,
				@"Licenses\"
			);
			await OpenAsync(path);
		});

		OpenWebsite = CommandFactory.Create<RoutedEventArgs>(async _
			=> await OpenAsync("https://github.com/InuInu2022/KuchiPaku"));

		OpenYMM4Website = CommandFactory.Create<RoutedEventArgs>(async _
			=> await OpenAsync("https://manjubox.net/ymm4/"));
	}



	private static async ValueTask OpenAsync(string path){
		await Task.Run(() =>
		{
			var info = new ProcessStartInfo()
			{
				FileName = path,
				UseShellExecute = true
			};
			Process.Start(info);
		});
	}

	public void MakeCustomVoiceFaceItem(
		int maxLayer,
		IEnumerable<YmmVoiceItem> customVoices,
		JObject ymmp,
		Dictionary<string, LipSyncOption> lipSyncSettings,
		int currentYmmpFPS
	)
	{
		var sw = new System.Diagnostics.Stopwatch();
		sw.Start();

		customVoices
			.AsParallel()
			.Where(v => v is not null)
			.Select(v =>
			{
				var labPath = Path.ChangeExtension(v!.Hatsuon, "lab");

				Debug.WriteLine($"lab: {labPath}:{File.Exists(labPath)}");

				v.HasLabFile = File.Exists(labPath);

				return v;
			})
			.Where(v => v.HasLabFile)
			.ToList()
			.ForEach(async v =>
			{
				var f = new FileInfo(Path.ChangeExtension(v!.Hatsuon, "lab")!);
				var lab = await LabUtil
					.MakeLabAsync(f, currentYmmpFPS);
				var items = (JArray)ymmp!["Timeline"]!["Items"]!;
				var tachie = new JObject();
				try
				{
					tachie = await YmmpUtil.ReadTachieTemplateAsync();
				}
				catch (System.Exception e)
				{
					throw new Exception(e.Message);
				}

				var set = lipSyncSettings!;

				var contentOffset = YmmpUtil
					.CulcContentOffset(
						v.ContentOffset.TotalMilliseconds,
						currentYmmpFPS
					);

				await YmmpUtil.MakeRipSyncItemAsync(
					lab,
					items,
					tachie,
					set[v!.CharacterName!]!,
					maxLayer + 1,
					v.Frame - contentOffset
				);
			})
			;
			//*/

		sw.Stop();
		Debug.WriteLine($"TIME[MakeCustomVoiceFaceItem]:{sw.ElapsedMilliseconds}");
	}

	[PropertyChanged(nameof(SelectedCharaItem))]
	private async ValueTask SelectedCharaItemChangedAsync(CharacterListViewModel item){
		if(item is null){return;}

		var sw = new System.Diagnostics.Stopwatch();
		sw.Start();

		var chara = item;
		Debug.WriteLine($"SelectedChara: {chara.Name}");

		//TODO:設定リストから読み出す
		//クチパク設定Viewに設定

		(LipSyncImages ??= new ObservableCollection<LipSyncImageViewModel>()).Clear();

		//TODO:将来的にルール設定から追加するようにする

		var path = chara.DirectoryPath!;
		if (path is null || !Directory.Exists(path))
		{
			//パスが無いのをユーザー通知
			Manager
				.Warn(
					"ファイルのあるはずのフォルダがみつかりません",
					"キャラの立ち絵の指定されたフォルダがありません。ファイルを丸ごと移動していませんか？"
				);
			return;
		}

		var kuchiDir = await Task.Run(()
			=> Directory.GetDirectories(path, "口").FirstOrDefault());

		if(kuchiDir is null)
		{
			Manager
				.Warn(
					"「口」フォルダがありません",
					"口パクさせるために、「口」フォルダに画像が置いてある必要があります。"
				);
			return;
		}

		string[] EXTS = { ".png",".gif",",webp"};

		/*
		var files = Directory
			.GetFiles(kuchiDir)
			.Select(s => Path.GetExtension(s));
		*/
		sw.Stop();
		Debug.WriteLine($"TIME[get dir kuchi]:{sw.ElapsedMilliseconds.ToString()}");
		sw.Restart();

		var kuchiImages =
			Directory
				.GetFiles(kuchiDir)
				.Where(s => EXTS.Contains(Path.GetExtension(s)))
				.Select(s => new LipSyncImageLineViewModel(
					Path.GetFileNameWithoutExtension(s),
					s
				));

		var kList = new ObservableCollection<LipSyncImageLineViewModel>(kuchiImages);

		sw.Stop();
		Debug.WriteLine($"TIME[kuchi images]:{sw.ElapsedMilliseconds.ToString()}");
		sw.Restart();

		//Debug.WriteLine($"kuchiImages:{kuchiImages}");
		//kList.Where(k => k.Path == )

		var images = LipSyncSettings[chara!.Name!]
			.MousePhenomeImagePair
			.Select(v =>
			{
				var lineName = v.Key switch
				{
					"a" => "あ行",
					"i" => "い行",
					"u" => "う行",
					"e" => "え行",
					"o" => "お行",
					"N" => "ん",
					_ => "ERROR"
				};
				var p = Path.Combine(kuchiDir, Path.GetFileName(v.Value));
				var kuchi
					= kList.First(k => k.Path == p)
						?? kList.FirstOrDefault(k => k.Path == chara.DefaultMouthImgPath);
				var index = (kuchi is null) ? 0 : kList.IndexOf(kuchi);
				return new LipSyncImageViewModel(
					v.Key,
					lineName,
					kList,
					path,
					chara.Name!,
					this,
					index
				);
			})
			.ToList()
			;
		LipSyncImages = new(images);

		sw.Stop();
		Debug.WriteLine($"TIME[rip sync images]:{sw.ElapsedMilliseconds.ToString()}");
		//sw.Restart();

		/*
		LipSyncImageViewModel[] lipSyncSets =
		{
			new("a","あ行",kList,chara.DirectoryPath!,chara.Name!,this),
			new("i","い行",kList,chara.DirectoryPath!,chara.Name!,this),
			new("u","う行",kList,chara.DirectoryPath!,chara.Name!,this),
			new("e","え行",kList,chara.DirectoryPath!,chara.Name!,this),
			new("o","お行",kList,chara.DirectoryPath!,chara.Name!,this),
			new("N","ん",kList,chara.DirectoryPath!,chara.Name!,this)
		};

		Array
			.ForEach(
				lipSyncSets,
				a => LipSyncImages.Add(a)
			);
		*/


	}

	[PropertyChanged(nameof(CurrentConsonantOption))]
	private async ValueTask CurrentConsonantOptionChangedAsync(ConsonantOption opt){
		await Application.Current.Dispatcher.InvokeAsync(() =>
		{
			LipSyncSettings
				.AsParallel()
				.ForAll(s => s.Value.ConsonantOption = opt);
		});
	}
}
