using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
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
	public static async ValueTask<JObject> ReadAsync(string ymmpPath){
		var str = await Task
			.Run(() => File.ReadAllText(ymmpPath, System.Text.Encoding.UTF8));

		return JObject.Parse(str);
    }

	public static async ValueTask SaveAsync(JObject ymmp, string savePath){
		var outStr = JsonConvert.SerializeObject(ymmp, Formatting.Indented);

		await Task.Run(() =>
			File.WriteAllText(savePath, outStr)
		);
	}

	public static int GetFPS(JObject ymmp){
		var fps = (int)ymmp["Timeline"]!["VideoInfo"]!["FPS"]!;
		Debug.WriteLine($"Current FPS: {fps}");
		return fps;
	}

	public static int GetMaxLayer(JObject ymmp){
		return (int)ymmp["Timeline"]!["MaxLayer"]!;
	}

	public static async ValueTask<List<YmmCharacter>> ParseCharactersAsync(JObject ymmp){
		JArray characters = (JArray)ymmp["Characters"]!;

		return await Task.Run(() => {
			return characters
				.Select(c => c.ToString())
				.Select(c => YmmCharacter.FromJson(c))
				.ToList();
		});
	}

	public static async ValueTask<List<YmmVoiceItem>> ParseVoiceItemsAsync(JObject ymmp){
		JArray items = (JArray)ymmp["Timeline"]!["Items"]!;

		return await Task.Run(() =>
		{
			return items
				//.AsParallel()
				.Where(v => v is not null && v["$type"]!.ToString() == YmmpItemType.VoiceItem)
				.Select(v => YmmItemUtil.FromJson<YmmVoiceItem>(v.ToString()))
				.Where(v => v is not null)
				.OfType<YmmVoiceItem>()
				.ToList();
		});
	}

	public static async ValueTask<IEnumerable<YmmVoiceItem>> FilterCustomVoiceAsync(IEnumerable<YmmVoiceItem?> voices)
	{
		Regex regPat = new(
			@"^[A-Z].+\.[0-9a-zA-Z]{3,}$",
			RegexOptions.Compiled);

		return await Task.Run(() => {
			return voices
				//.AsParallel()
				.Where(v => v is not null)
				.OfType<YmmVoiceItem>()
				.Where(v =>
					v is not null
						&& !string.IsNullOrEmpty(v.Hatsuon)
						&& regPat.IsMatch(v.Hatsuon)
						&& File.Exists(v.Hatsuon)
				)
				.Select(v =>
				{
					v!.IsCustomVoice = true;
					return v;
				})
				;
		});
	}

	public static async ValueTask<IEnumerable<YmmVoiceItem>> FilterAPIVoiceAsync(IEnumerable<YmmVoiceItem?> voices){
		return await Task.Run(() =>
			voices
				.AsParallel()
				.Where(v => v is not null)
				.OfType<YmmVoiceItem>()
				.Where(v => !v.IsCustomVoice && v.VoiceParameter is not null)

		);
	}

	public static async ValueTask<JObject> ReadTachieTemplateAsync(){
		var path = Path.Combine(
			AppDomain.CurrentDomain.BaseDirectory,
			@"Template\template.TachieFaceItem.json"
		);
		if(!File.Exists(path)){
			throw new FileNotFoundException($"Tachie Template not found. '{path}'");
		}

		var str = await Task.Run(()=>
			File.ReadAllText(
				path,
				System.Text.Encoding.UTF8
		));
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
		JArray items,
		JObject tmpItem,
		LipSyncOption lipSyncOption,
		int insertLayer = 0,
		int offsetFrame = 0,
		bool isLocked = false
	){
		if(lab is null || lab.Lines is null)
		{
			return;
		}

		var consoOpt = lipSyncOption.ConsonantOption;
		var images = lipSyncOption.MousePhenomeImagePair;

		string[] VOWELS_A = {
			"a",
			"A",
			"aa",
			"ae",
			"ah",
			"ax",
			"aw",
			"axr",
			"ay"
		};
		string[] VOWELS_I = {
			"i","I",
			"ih","iy","y"
		};
		string[] VOWELS_U = {
			"u","U",
			"uh","uw"
		};
		string[] VOWELS_E = {
			"e","E",
			"eh","ey"
		};
		string[] VOWELS_O = {
			"o","O",
			"ao","ow","oy"
		};
		string[] CLOSE_CONSONANT = {
			"by","my","ny","py",
			"mm","nn","b","m","n","ng","p","v"
		};
		string[] OPEN_CONSONANT = {
			"dy","gy","hy","j","ky","ry","ts","ty",
			"ch","d","dh","f","g","hh","jh","k",
			"l","r","s","sh","t","th","w","z","zh",
			"tt","dd",
		};
		string[] LIKE_N = {
			"N"
		};

		var len = lab.Lines.Count();
		var lastVowel = images["N"];
		for (var i = 0; i < len; i++)
		{
			var line = lab.Lines.ElementAt(i);

			if (line.Phoneme == "pau") continue;
			if (line.FrameLen <= 0) continue;

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
							ConsonantOption.SMALL_MOUSE => images["u"],//TODO:代理処理
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


			var mouseImagePath = Path.Combine(
				lipSyncOption.MouseDir!,
				imageFileName
			);

			//deep copy
			JObject? newItem = await CopyDeepAsync(tmpItem);
			if (newItem is null) continue;

			//lab line to a new item
			newItem["Layer"] = insertLayer;
			newItem["CharacterName"] = lipSyncOption.CharacterName;
			newItem["TachieFaceParameter"]!["Mouth"] = mouseImagePath;
			newItem["Frame"] = line.FrameFrom + offsetFrame;
			newItem["Length"] = line.FrameLen;
			newItem["IsLocked"] = isLocked;


			items.Add(newItem);
		}
	}

	public static async Task<JObject> CopyDeepAsync(JObject tmpItem)
	{
		var s = await Task.Run(()
			=> JsonConvert.SerializeObject(tmpItem));
		var newItem = await Task.Run(()
			=> JsonConvert.DeserializeObject<JObject>(s));
		return newItem!;
	}

	public static int CulcContentOffset(double totalSeconds, int fps){
		return (totalSeconds == 0.0) ? 0 : (int) Math.Round(totalSeconds / 1000 * fps);
	}
}