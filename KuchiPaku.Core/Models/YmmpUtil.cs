using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using KuchiPaku.Core.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace KuchiPaku.Models;

public static partial class YmmpUtil
{
	/// <summary>
	/// .ymmpファイルを読み込む
	/// </summary>
	/// <param name="ymmpPath">YMM4プロジェクトファイルpath</param>
	/// <returns></returns>
	public static async ValueTask<JObject> ReadAsync(string ymmpPath)
	{
		var str = await Task.Run(() => File.ReadAllText(ymmpPath, System.Text.Encoding.UTF8));

		return JObject.Parse(str);
	}

	public static async ValueTask SaveAsync(JObject ymmp, string savePath)
	{
		var outStr = JsonConvert.SerializeObject(ymmp, Formatting.Indented);

		await Task.Run(() => File.WriteAllText(savePath, outStr));
	}

	public static IEnumerable<(int Scene, int Fps)> GetFPS(JObject ymmp)
	{
		var tl = GetYmmpTimeline(ymmp);
		if (tl is null) { return []; }

		var fps = HasSceneTimelines(ymmp) switch
		{
			//シーンごとにFPSを取得するようにする
			true => tl.Select((v, i) => (Scene: i, Fps: (int)v["VideoInfo"]!["FPS"]!)),
			//旧形式
			_ => [(0, (int)tl["VideoInfo"]!["FPS"]!)],
		};

		Debug.WriteLine($"Current FPS: {fps}");
		return fps;
	}

	public static Dictionary<int, int>
	GetMaxLayer(JObject ymmp)
	{
		var tl = GetYmmpTimeline(ymmp);
		if (tl is null) { return new() { { 0, 9999 } }; }

		var maxLayers = HasSceneTimelines(ymmp) switch
		{
			//シーンごとにMaxLayerを取得
			true => tl.Select((v, i) => (Scene: i, MaxLayer: (int)v["MaxLayer"]!)),
			_ => [(Scene: 0, MaxLayer: (int)tl["MaxLayer"]!)],
		};

		return maxLayers.ToDictionary(x => x.Scene, x => x.MaxLayer);
	}

	public static async ValueTask<List<YmmCharacter>> ParseCharactersAsync(JObject ymmp)
	{
		var hasCharacters = ymmp.TryGetValue("Characters", out var characters);
		if (!hasCharacters) return new([]);

		return await Task.Run(() =>
		{
			return characters
				.Select(c => c.ToString())
				.Select(c => YmmCharacter.FromJson(c))
				.ToList();
		});
	}

	public static async ValueTask<IEnumerable<(int Scene, YmmVoiceItem Item)>>
	ParseVoiceItemsAsync(
		JObject ymmp
	)
	{
		var tl = GetYmmpTimeline(ymmp);
		if (tl is null) return [];
		var items = GetVoiceItemsByScene(ymmp, tl);

		return await Task.Run(() => items
			.SelectMany(v => v.Value.Select((v2, i) => new KeyValuePair<int, JToken>(v.Key, v2)))
			.Where(v => v.Value is not null && v.Value["$type"]!.ToString() == YmmpItemType.VoiceItem)
			.Select(v => (v.Key, YmmItemUtil.FromJson<YmmVoiceItem>(v.Value.ToString())))
			.Where(v => v.Item2 is not null)
			.OfType<(int, YmmVoiceItem)>()
		//.ToList()
		);
	}

	public static IEnumerable<(int Scene, SearchItem Item)>
	ParseFaceWithItems(JObject ymmp)
	{
		var tl = GetYmmpTimeline(ymmp);
		if (tl is null)
			return [];
		var items = GetVoiceItemsByScene(ymmp, tl);

		return items
			.SelectMany(v => v.Value.Select((v2, i) => (v.Key, Value: v2)))
			.Where(v => v.Value is not null
				&& (
					v.Value["$type"]!.ToString() == YmmpItemType.VoiceItem
					||
					v.Value["$type"]!.ToString() == YmmpItemType.TachieItem
					||
					v.Value["$type"]!.ToString() == YmmpItemType.TachieFaceItem
				)
				&& (
					v.Value["TachieFaceParameter"]?["$type"]?.ToString() == YmmpTachieFaceParameterType.PsdTachieFace
					||
					v.Value["TachieItemParameter"]?["$type"]?.ToString()
					== YmmpTachieItemParameterType.PsdTachieItem
				)
			)
			.Select(v =>
			{
				var param =
					v.Value["TachieFaceParameter"]
					?? v.Value["TachieItemParameter"];
				string[] layers = param?["EnableLayers"]?
					.ToObject<string[]>() ?? [];
				return (
					v.Key,
					new SearchItem(
						v.Value["CharacterName"]?.ToString() ?? "",
						(int)(v.Value["Frame"] ?? 0),
						(int)(v.Value["Length"] ?? 0),
						(int)(v.Value["Layer"] ?? 0),
						layers
					)
				);
			})
			;
	}

	/// <summary>
	/// シーンごとにボイスアイテムを処理する
	/// </summary>
	/// <param name="ymmp"></param>
	/// <param name="tl"></param>
	/// <returns></returns>
	private static Dictionary<int, JArray>
	GetVoiceItemsByScene(JObject ymmp, JToken tl)
	{
		Dictionary<int, JArray> items = HasSceneTimelines(ymmp) switch
		{
			//シーンごとにボイスアイテムを処理する
			true => tl
				.Select((v, i) => new KeyValuePair<int, JArray>(i, (JArray)v["Items"]! ?? []))
				//.SelectMany(v => v.Value.Select((v2, i) => new KeyValuePair<int, JToken>(i, v2)))
				.ToDictionary(v => v.Key, v => v.Value),
			_ => new() { [0] = (JArray)tl["Items"]! },
		};
		return items;
	}

	public static async ValueTask<IEnumerable<(int Scene, YmmVoiceItem Item)>>
	FilterCustomVoiceAsync(
		IEnumerable<(int Scene, YmmVoiceItem Item)> voices
	)
	{
		Regex regPat = new(@"^[A-Z].+\.[0-9a-zA-Z]{3,}$", RegexOptions.Compiled);
		Regex regFilePat = new(@"^[A-Z]\:\\\\.*$", RegexOptions.Compiled);

		return await Task.Run(() => voices
			.Where(v =>
				v.Item is not null
				&& !string.IsNullOrEmpty(v.Item.Hatsuon)
				&& (regPat.IsMatch(v.Item.Hatsuon) || regFilePat.IsMatch(v.Item.Hatsuon))
				&& File.Exists(v.Item.Hatsuon)
			)
			.Select(v =>
			{
				v.Item.IsCustomVoice = true;
				return v;
			})
		);
	}

	public static async ValueTask<IEnumerable<(int Scene, YmmVoiceItem Item)>>
	FilterAPIVoiceAsync(
		IEnumerable<(int Scene, YmmVoiceItem Item)> voices
	)
	{
		return await Task.Run(() => voices
			.AsParallel()
			.Where(v => v.Item is not null)
			.Where(v =>
				!v.Item.IsCustomVoice
				&& v.Item.VoiceParameter is not null
			)
		);
	}

	static readonly Dictionary<string, JObject> TachieTemplateCache = [];
	public static async ValueTask<JObject> ReadTachieTemplateAsync(
		string type
	)
	{
		var file = type switch
		{
			YmmpTachieType.AnimationTachie => @"Template\template.TachieFaceItem.json",
			YmmpTachieType.PsdTachie => @"Template\template.PsdTachieFaceItem.json",
			_ => @"Template\template.TachieFaceItem.json",
		};
		if (TachieTemplateCache.ContainsKey(file))
		{
			return TachieTemplateCache[file];
		}

		var path = Path.Combine(
			AppDomain.CurrentDomain.BaseDirectory,
			file
		);
		if (!File.Exists(path))
		{
			throw new FileNotFoundException($"Tachie Template not found. '{path}'");
		}

		var str = await Task
			.Run(() => File.ReadAllText(path, System.Text.Encoding.UTF8));
		TachieTemplateCache[file] = JObject.Parse(str);
		return TachieTemplateCache[file];
	}

	static readonly string[] VOWELS_A = ["a", "A", "aa", "ae", "ah", "ax", "aw", "axr", "ay"];
	static readonly string[] VOWELS_I = ["i", "I", "ih", "iy", "y"];
	static readonly string[] VOWELS_U = ["u", "U", "uh", "uw"];
	static readonly string[] VOWELS_E = ["e", "E", "eh", "ey"];
	static readonly string[] VOWELS_O = ["o", "O", "ao", "ow", "oy"];
	static readonly string[] CLOSE_CONSONANT = ["by", "my", "ny", "py", "mm", "nn", "b", "m", "n", "ng", "p", "v"];
	static readonly string[] OPEN_CONSONANT =
	[
		"dy",
		"gy",
		"hy",
		"j",
		"ky",
		"ry",
		"ts",
		"ty",
		"ch",
		"d",
		"dh",
		"f",
		"g",
		"hh",
		"jh",
		"k",
		"l",
		"r",
		"s",
		"sh",
		"t",
		"th",
		"w",
		"z",
		"zh",
		"tt",
		"dd",
	];
	static readonly string[] LIKE_N = ["N"];

	/// <summary>
	/// リップシンク（いわゆるあいうえお口パク）用に
	/// 表情アイテムを設置する
	/// </summary>
	/// <seealso cref="https://oov.github.io/aviutl_psdtoolkit/psd.html#.E5.8F.A3.E3.83.91.E3.82.AF_.E3.81.82.E3.81.84.E3.81.86.E3.81.88.E3.81.8A.40PSD"/>
	/// <param name="lab"></param>
	/// <param name="items"></param>
	/// <param name="tmpItem">TachieFaceItem</param>
	/// <param name="lipSyncOption"></param>
	/// <param name="insertLayer"></param>
	/// <param name="isLocked"></param>
	public static async ValueTask MakeRipSyncItemAsync(
		Lab lab,
		Dictionary<int, JArray> items,
		JObject tmpItem,
		LipSyncOption lipSyncOption,
		int insertLayer = 0,
		int offsetFrame = 0,
		bool isLocked = false,
		int sceneIndex = 0,
		int visualLeadFrames = 0,
		string tachieType = "",
		bool isPartsOverrideMode = false,
		Dictionary<int, SearchTimeline>? searchTimelines = null,
		YmmVoiceItem? voiceItem = null
	)
	{
		if (lab is null || lab.Lines is null)
		{
			return;
		}

		var consoOpt = lipSyncOption.ConsonantOption;
		var images = lipSyncOption.MousePhonemeImagePair;

		var len = lab.Lines.Count();
		var lastVowel = tachieType switch
		{
			YmmpTachieType.AnimationTachie => images["N"],
			YmmpTachieType.PsdTachie => "N",
			_ => "",
		};
		for (var i = 0; i < len; i++)
		{
			var line = lab.Lines.ElementAt(i);
			Debug.WriteLine(
				$"Frame[{line.Phoneme}] {line.FrameLen} [{line.FrameFrom}-{line.FrameTo}]({line.From} - {line.To})"
			);

			if (line.Phoneme == "pau")
				continue;
			if (line.FrameLen <= 0)
			{
				continue;
			}

			//deep copy
			JObject? newItem = await CopyDeepAsync(tmpItem);
			if (newItem is null)
				continue;

			OverrideLipSyncExpressions(searchTimelines!, isPartsOverrideMode, sceneIndex, voiceItem!, lipSyncOption, tachieType, offsetFrame,line.FrameFrom + offsetFrame);

			//lab line to a new item
			newItem["Layer"] = insertLayer;
			newItem["CharacterName"] = lipSyncOption.CharacterName;
			newItem["Frame"] = line.FrameFrom + offsetFrame - visualLeadFrames;
			newItem["Length"] = line.FrameLen;
			newItem["IsLocked"] = isLocked;
			switch (tachieType)
			{
				//動く立ち絵
				case YmmpTachieType.AnimationTachie:
					newItem["TachieFaceParameter"]!["Mouth"] = GetMouseImagePath(
						lipSyncOption,
						consoOpt,
						images,
						ref lastVowel,
						line
					);
					break;
				//PSD立ち絵
				case YmmpTachieType.PsdTachie:
					var layers = GetEnableLayers(
						lipSyncOption,
						consoOpt,
						ref lastVowel,
						line
					).ToArray();
					newItem["TachieFaceParameter"]!["EnableLayers"] = new JArray(layers);
					//PSD file path
					newItem["TachieFaceParameter"]!["FilePath"] = lipSyncOption.TargetDir;
					break;
				default:
					break;
			}

			JArray ja = items.ElementAtOrDefault(sceneIndex).Value;
			ja.Add(newItem);
		}
	}

	private static string GetMouseImagePath(
		LipSyncOption lipSyncOption,
		ConsonantOption consoOpt,
		Dictionary<string, string> images,
		ref string lastVowel,
		LabLine line)
	{
		var imageFileName = "";

		switch (line.Phoneme)
		{
			case var p when VOWELS_A.Contains(p):
				{
					imageFileName = images["a"];
					lastVowel = imageFileName;
					break;
				}

			case var p when VOWELS_I.Contains(p):
				{
					imageFileName = images["i"];
					lastVowel = imageFileName;
					break;
				}

			case var p when VOWELS_U.Contains(p):
				{
					imageFileName = images["u"];
					lastVowel = imageFileName;
					break;
				}

			case var p when VOWELS_E.Contains(p):
				{
					imageFileName = images["e"];
					lastVowel = imageFileName;
					break;
				}

			case var p when VOWELS_O.Contains(p):
				{
					imageFileName = images["o"];
					lastVowel = imageFileName;
					break;
				}

			case var p when CLOSE_CONSONANT.Contains(p):
				{
					imageFileName = images["N"];
					break;
				}

			case var p when OPEN_CONSONANT.Contains(p):
				{
					imageFileName = consoOpt switch
					{
						ConsonantOption.CONTINUE_BEFORE_VOWEL => lastVowel,
						ConsonantOption.SMALL_MOUSE => images["u"], //TODO:代理処理
						_ => images["N"],
					};
					break;
				}

			default:
				{
					imageFileName = images["N"];
					break;
				}
		}

		var mouseImagePath = Path.Combine(lipSyncOption.TargetDir!, imageFileName);
		return mouseImagePath;
	}

	static IEnumerable<string> GetEnableLayers(
		LipSyncOption lipSyncOption,
		ConsonantOption consoOpt,
		ref string lastVowel,
		LabLine line
	)
	{
		IEnumerable<string> layers = [];
		switch (line.Phoneme)
		{
			case var p when VOWELS_A.Contains(p):
				{
					layers = lipSyncOption.MousePhonemeLayerPair["a"];
					lastVowel = "a";
					break;
				}

			case var p when VOWELS_I.Contains(p):
				{
					layers = lipSyncOption.MousePhonemeLayerPair["i"];
					lastVowel = "i";
					break;
				}

			case var p when VOWELS_U.Contains(p):
				{
					layers = lipSyncOption.MousePhonemeLayerPair["u"];
					lastVowel = "u";
					break;
				}

			case var p when VOWELS_E.Contains(p):
				{
					layers = lipSyncOption.MousePhonemeLayerPair["e"];
					lastVowel = "e";
					break;
				}

			case var p when VOWELS_O.Contains(p):
				{
					layers = lipSyncOption.MousePhonemeLayerPair["o"];
					lastVowel = "o";
					break;
				}

			case var p when CLOSE_CONSONANT.Contains(p):
				{
					layers = lipSyncOption.MousePhonemeLayerPair["N"];
					break;
				}

			case var p when OPEN_CONSONANT.Contains(p):
				{
					layers = consoOpt switch
					{
						ConsonantOption.CONTINUE_BEFORE_VOWEL => lipSyncOption.MousePhonemeLayerPair[lastVowel],
						ConsonantOption.SMALL_MOUSE => lipSyncOption.MousePhonemeLayerPair["u"], //TODO:代理処理
						_ => lipSyncOption.MousePhonemeLayerPair["N"],
					};
					break;
				}

			default:
				{
					layers = lipSyncOption.MousePhonemeLayerPair["N"];
					break;
				}
		}

		return layers;
	}

	public static async Task<JObject> CopyDeepAsync(JObject tmpItem)
	{
		var s = await Task.Run(() => JsonConvert.SerializeObject(tmpItem));
		var newItem = await Task.Run(() => JsonConvert.DeserializeObject<JObject>(s));
		return newItem!;
	}

	public static int CulcContentOffset(double totalSeconds, int fps)
	{
		return (totalSeconds == 0.0) ? 0 : (int)Math.Round(totalSeconds / 1000 * fps);
	}

	public static void MakeCustomVoiceFaceItem(
		IDictionary<int, int> maxLayer,
		IEnumerable<(int Scene, YmmVoiceItem Item)> customVoices,
		JObject ymmp,
		Dictionary<string, LipSyncOption> lipSyncSettings,
		IEnumerable<(int Scene, int Fps)> currentYmmpFPS,
		Dictionary<int, SearchTimeline> searchTimelines,
		bool isPartsOverrideMode = false,
		int visualLeadMs = 0
	)
	{
		var sw = new System.Diagnostics.Stopwatch();
		sw.Start();

		var tl = GetYmmpTimeline(ymmp);
		if (tl is null) return;

		var filtered = customVoices
			.AsParallel()
			.Where(v => v.Item is not null)
			.Select(v =>
			{
				var labPath = Path.ChangeExtension(v.Item.Hatsuon, "lab");

				Debug.WriteLine($"lab: {labPath}:{File.Exists(labPath)}");

				v.Item.HasLabFile = File.Exists(labPath);

				return v;
			})
			.Where(v => v.Item.HasLabFile)
			.ToList();



		filtered.ForEach(async v =>
		{
			var f = new FileInfo(Path.ChangeExtension(v.Item.Hatsuon, "lab")!);
			int sceneFps = currentYmmpFPS.ElementAtOrDefault(v.Scene).Fps;
			var lab = await LabUtil.MakeLabAsync(f, sceneFps);

			await lab.ChangeLengthByRateAsync(v.Item.PlaybackRate);
			var items = GetVoiceItemsByScene(ymmp, tl);

			var settings = lipSyncSettings!;
			var tachieType = settings[v.Item.CharacterName!].TachieType;

			//(JArray)ymmp!["Timeline"]!["Items"]!;
			var tachie = new JObject();
			try
			{
				tachie = await ReadTachieTemplateAsync(tachieType);
			}
			catch (System.Exception e)
			{
				throw new Exception(e.Message);
			}

			var contentOffset = CulcContentOffset(
				v.Item.ContentOffset.TotalMilliseconds,
				sceneFps
			);

			/*OverrideLipSyncExpressions(searchTimelines, isPartsOverrideMode, v, settings, tachieType, contentOffset);*/

			await MakeRipSyncItemAsync(
				lab,
				items,
				tachie,
				settings[v.Item.CharacterName!]!,
				maxLayer[v.Scene] + 1,
				v.Item.Frame - contentOffset,
				sceneIndex: v.Scene,
				visualLeadFrames: CulcVisualLeadOffset(visualLeadMs, sceneFps),
				tachieType: tachieType,
				isPartsOverrideMode: isPartsOverrideMode,
				searchTimelines:searchTimelines,
				voiceItem:v.Item
			);

			maxLayer[v.Scene]++;
		});

		sw.Stop();
		Debug.WriteLine($"TIME[MakeCustomVoiceFaceItem]:{sw.ElapsedMilliseconds}");
	}

	/// <summary>
	/// `isPartsOverrideMode`がtrueのときに差分再現
	/// </summary>
	/// <param name="searchTimelines"></param>
	/// <param name="isPartsOverrideMode"></param>
	/// <param name="v"></param>
	/// <param name="settings"></param>
	/// <param name="tachieType"></param>
	/// <param name="contentOffset"></param>
	static void OverrideLipSyncExpressions(
		Dictionary<int, SearchTimeline> searchTimelines,
		bool isPartsOverrideMode,
		int scene,
		YmmVoiceItem voiceItem,
		LipSyncOption settings,
		string tachieType,
		int contentOffset,
		int searchFrame
	)
	{
		if (
			!isPartsOverrideMode
			|| tachieType != YmmpTachieType.PsdTachie
			|| !searchTimelines.TryGetValue(scene, out var timeline)
		)
		{
			return;
		}

		var frame = Math.Max(searchFrame, 0);
		Debug.WriteLine($"GetItemsAtFrame({frame})");
		var sItems = timeline
			.GetItemsAtFrame(frame)
			.Where(s =>
				s.CharacterName == voiceItem.CharacterName
				&& !s.Data.IsEmpty)
			.OrderByDescending(s => s.Layer)
			.ToList();

		if (!sItems.Any())
		{
			return;
		}

		Debug.WriteLine($"-- front --");
		sItems.First().Data.ToArray().ToList().ForEach(v => Debug.WriteLine(v));
		Debug.WriteLine($"-- --");



		var overrides = settings.MousePhonemeOverrideLayerPair;
		var selections = settings.MousePhonemeLayerPair;

		var frontLayers = sItems.First().Data;

		foreach (var item in selections)
		{
			var newLayers = new string[frontLayers.Length];
			frontLayers.CopyTo(newLayers);
			var newList = newLayers.ToList();

			if (!overrides.TryGetValue(item.Key, out var rides))
			{
				//上書き指定レイヤー無いなら上のアイテムと同じ見た目にする
				selections[item.Key] = newList;
				continue;
			}

			var overrideCids = rides
				.Where(v => v.Value).Select(v => v.Key) ?? [];
			var layerSelection = selections[item.Key];

			if (overrideCids.Count() == 0)
			{
				//上書き指定レイヤー無いなら上のアイテムと同じ見た目にする
				selections[item.Key] = newList;
				continue;
			}

			var visibles = overrideCids.ToDictionary(
				cid => cid,
				cid => layerSelection.Contains(cid)
			);

			newList.RemoveAll(
				visibles.Where(v => !v.Value).Select(v => v.Key).Contains);
			foreach (var v2 in visibles)
			{
				if (v2.Value)
				{
					newList.Add(v2.Key);
				}
			}
			newList = [.. newList.Distinct()];
			selections[item.Key] = newList;

			newList.ForEach(v => Debug.WriteLine(v));
		}
	}

	public static async ValueTask MakeAPIVoiceFaceItemAsync(
		IDictionary<int, int> maxLayer,
		IEnumerable<(int Scene, YmmVoiceItem Item)> voiceItems,
		JObject ymmp,
		Dictionary<string, LipSyncOption> lipSyncSettings,
		IEnumerable<(int Scene, int Fps)> currentYmmpFPS,
		Dictionary<int, SearchTimeline> searchTimelines,
		bool isPartsOverrideMode = false,
		int visualLeadMs = 0
	)
	{
		var ymmChara = await ParseCharactersAsync(ymmp);

		var convItems = voiceItems
			.AsParallel()
			.Where(v =>
				v.Item is not null
				&& !string.IsNullOrEmpty(v.Item.Serif)
				&& v.Item.VoiceParameter is not null
			)
			.Select(v => (
				item: v,
				voice: ymmChara.First(c => c.Name == v.Item.CharacterName).Voice))
			.Where(v => v.voice.Api != "commandline")
			.Where(v =>
				TTSTable.YmmVoiceToProduct[v.voice.Api]
					is TTSProduct.CeVIO_AI
						or TTSProduct.CeVIO_CS
			) //TODO:support voicevox,sharevox
			.ToList();
		var moddedItems = new List<((int Scene, YmmVoiceItem Item) item, Voice voice)>();
		try
		{
			await Task.WhenAll(
				convItems
					.Select(v =>
						TTSUtil
							.AwakeTTSAsync(TTSTable.YmmVoiceToProduct[v.voice.Api], true)
							.AsTask()
					)
					.ToArray()
			);
			await CheckTTSAwakedAsync(convItems);

			foreach (var (item, voice) in convItems)
			{
				item.Item.LabLines = await TTSUtil.GetPhonemesAsync(
					voice,
					item.Item.VoiceParameter!,
					TTSTable.YmmVoiceToProduct[voice.Api],
					item.Item.Serif ?? "",
					fps: currentYmmpFPS.ElementAtOrDefault(item.Scene).Fps,
					item.Item.PlaybackRate
				);
			}

			moddedItems = convItems;
		}
		catch (Exception e)
		{
			Debug.WriteLine($"ERROR:{e.Message}");
			await TTSUtil.StopTTSAsync(TTSProduct.CeVIO_AI);
			await TTSUtil.StopTTSAsync(TTSProduct.CeVIO_CS);
			throw new Exception("get phoneme error", e);
		}

		Debug.WriteLine($"API voice num:{moddedItems.Count}");

		//convItems
		moddedItems.ForEach(async v =>
		{
			var tl = GetYmmpTimeline(ymmp);
			if (tl is null) { return; }

			var items = GetVoiceItemsByScene(ymmp, tl);

			var settings = lipSyncSettings!;
			var tachieType = settings[v.item.Item.CharacterName!].TachieType;

			var tachie = new JObject();
			try
			{
				tachie = await ReadTachieTemplateAsync(tachieType);
			}
			catch (System.Exception e)
			{
				throw new Exception(e.Message);
			}


			var contentOffset = CulcContentOffset(
				v.item.Item.ContentOffset.TotalMilliseconds,
				currentYmmpFPS.ElementAtOrDefault(v.item.Scene).Fps
			);
			try
			{
				var lab = new Lab(v.item.Item.LabLines!);
				if (v.item.Item.PlaybackRate is not 100.0)
				{
					await lab.ChangeLengthByRateAsync(v.item.Item.PlaybackRate);
				}

				await MakeRipSyncItemAsync(
					lab,
					items,
					tachie,
					settings[v!.item.Item.CharacterName!]!,
					maxLayer[v.item.Scene] + 1,
					v.item.Item.Frame - contentOffset,
					sceneIndex: v.item.Scene,
					visualLeadFrames: CulcVisualLeadOffset(
						visualLeadMs,
						currentYmmpFPS
							.ElementAtOrDefault(v.item.Scene)
							.Fps
					),
					tachieType: tachieType,
					isPartsOverrideMode:isPartsOverrideMode,
					searchTimelines:searchTimelines,
					voiceItem: v.item.Item
				);
			}
			catch (System.Exception e)
			{
				Debug.WriteLine($"e:{e.Message}");
			}

			maxLayer[v.item.Scene]++;
		});

		//end tts server
		await TTSUtil.StopTTSAsync(TTSProduct.CeVIO_AI);
		await TTSUtil.StopTTSAsync(TTSProduct.CeVIO_CS);
	}

	private static async ValueTask CheckTTSAwakedAsync(
		IEnumerable<((int Scene, YmmVoiceItem Item) item, Voice voice)> convItems
	)
	{
		await Task.Delay(2000);
		Debug.WriteLine("TTS awake waiting...");
		var results = await Task.WhenAll(
			convItems
				.Select(v =>
					TTSUtil.IsTTSAwakedAsync(TTSTable.YmmVoiceToProduct[v.voice.Api]).AsTask()
				)
				.ToArray()
		);
		var isAllAwaked = results.All(v => v);
		if (!isAllAwaked)
		{
			await CheckTTSAwakedAsync(convItems);
		}
	}

	/// <summary>
	/// タイムラインにシーンが存在するか
	/// </summary>
	/// <param name="ymmp"></param>
	/// <returns></returns>
	static bool HasSceneTimelines(JObject ymmp)
	{
		return ymmp.TryGetValue("Timelines", out var _);
	}

	static JToken? GetYmmpTimeline(JObject ymmp)
	{
		if (ymmp is null)
			return default;

		var hasTimelines = ymmp.TryGetValue("Timelines", out var timelines);
		if (hasTimelines)
			return timelines;

		var hasOldTimeline = ymmp.TryGetValue("Timeline", out var oldTimeline);
		if (hasOldTimeline)
			return oldTimeline;

		return default;
	}

	static int CulcVisualLeadOffset(double ms, int fps)
	{
		return (int)Math.Round((ms / 1000.0) * fps);
	}
}
