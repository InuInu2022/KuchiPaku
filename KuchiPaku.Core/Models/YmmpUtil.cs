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
				//TODO: support psd tachie
				&& v.Item.TachieFaceParameter?.Type
					!= "YukkuriMovieMaker.Plugin.Tachie.Psd.PsdTachieFaceParameter, YukkuriMovieMaker.Plugin.Tachie.Psd"
			)
		);
	}

	public static async ValueTask<JObject> ReadTachieTemplateAsync()
	{
		var path = Path.Combine(
			AppDomain.CurrentDomain.BaseDirectory,
			@"Template\template.TachieFaceItem.json"
		);
		if (!File.Exists(path))
		{
			throw new FileNotFoundException($"Tachie Template not found. '{path}'");
		}

		var str = await Task
			.Run(() => File.ReadAllText(path, System.Text.Encoding.UTF8));
		return JObject.Parse(str);
	}

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
		int visualLeadFrames = 0
	)
	{
		if (lab is null || lab.Lines is null)
		{
			return;
		}

		var consoOpt = lipSyncOption.ConsonantOption;
		var images = lipSyncOption.MousePhonemeImagePair;

		string[] VOWELS_A = { "a", "A", "aa", "ae", "ah", "ax", "aw", "axr", "ay" };
		string[] VOWELS_I = { "i", "I", "ih", "iy", "y" };
		string[] VOWELS_U = { "u", "U", "uh", "uw" };
		string[] VOWELS_E = { "e", "E", "eh", "ey" };
		string[] VOWELS_O = { "o", "O", "ao", "ow", "oy" };
		string[] CLOSE_CONSONANT =
		{
			"by",
			"my",
			"ny",
			"py",
			"mm",
			"nn",
			"b",
			"m",
			"n",
			"ng",
			"p",
			"v",
		};
		string[] OPEN_CONSONANT =
		{
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
		};
		string[] LIKE_N = { "N" };

		var len = lab.Lines.Count();
		var lastVowel = images["N"];
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

			var mouseImagePath = Path.Combine(lipSyncOption.MouseDir!, imageFileName);

			//deep copy
			JObject? newItem = await CopyDeepAsync(tmpItem);
			if (newItem is null)
				continue;

			//lab line to a new item
			newItem["Layer"] = insertLayer;
			newItem["CharacterName"] = lipSyncOption.CharacterName;
			newItem["TachieFaceParameter"]!["Mouth"] = mouseImagePath;
			newItem["Frame"] = line.FrameFrom + offsetFrame - visualLeadFrames;
			newItem["Length"] = line.FrameLen;
			newItem["IsLocked"] = isLocked;

			JArray ja = items.ElementAtOrDefault(sceneIndex).Value;
			ja.Add(newItem);
		}
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

			//(JArray)ymmp!["Timeline"]!["Items"]!;
			var tachie = new JObject();
			try
			{
				tachie = await ReadTachieTemplateAsync();
			}
			catch (System.Exception e)
			{
				throw new Exception(e.Message);
			}

			var set = lipSyncSettings!;

			var contentOffset = CulcContentOffset(
				v.Item.ContentOffset.TotalMilliseconds,
				sceneFps
			);

			await MakeRipSyncItemAsync(
				lab,
				items,
				tachie,
				set[v.Item.CharacterName!]!,
				maxLayer[v.Scene] + 1,
				v.Item.Frame - contentOffset,
				sceneIndex: v.Scene,
				visualLeadFrames: CulcVisualLeadOffset(visualLeadMs, sceneFps)
			);

			maxLayer[v.Scene]++;
		});

		sw.Stop();
		Debug.WriteLine($"TIME[MakeCustomVoiceFaceItem]:{sw.ElapsedMilliseconds}");
	}

	public static async ValueTask MakeAPIVoiceFaceItemAsync(
		IDictionary<int, int> maxLayer,
		IEnumerable<(int Scene, YmmVoiceItem Item)> voiceItems,
		JObject ymmp,
		Dictionary<string, LipSyncOption> lipSyncSettings,
		IEnumerable<(int Scene, int Fps)> currentYmmpFPS,
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

			var tachie = new JObject();
			try
			{
				tachie = await ReadTachieTemplateAsync();
			}
			catch (System.Exception e)
			{
				throw new Exception(e.Message);
			}

			var set = lipSyncSettings!;
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
					set[v!.item.Item.CharacterName!]!,
					maxLayer[v.item.Scene] + 1,
					v.item.Item.Frame - contentOffset,
					sceneIndex: v.item.Scene,
					visualLeadFrames: CulcVisualLeadOffset(
						visualLeadMs,
						currentYmmpFPS
							.ElementAtOrDefault(v.item.Scene)
							.Fps
					)
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
