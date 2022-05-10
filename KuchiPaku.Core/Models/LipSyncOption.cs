using System.Collections.Generic;

namespace KuchiPaku.Models;

/// <summary>リップシンク用オプション</summary>
public class LipSyncOption
{

	public string? CharacterName { get; set; }
	public string? MouseDir { get; set; }

	/// <summary>
	/// 音素と画像名のtupleペアCollection
	/// "音素文字列":"画像名"
	/// </summary>
	public Dictionary<string, string> MousePhenomeImagePair { get; set; }
		= new Dictionary<string, string>();

	/// <summary>
	/// 子音処理オプション
	/// * 0 - すべて「ん」として処理する
	/// * 1 - 口を閉じる子音以外は前の母音を引き継ぐ
	/// * 2 - 口を閉じる子音以外は前後の母音の形をより小さいもので補間
	/// </summary>
	public ConsonantOption ConsonantOption { get; set; } = ConsonantOption.CONTINUE_BEFORE_VOWEL;

	public static LipSyncOption GetDefault(string name, string dirPath, string? imgPath){
		return new LipSyncOption
		{
			CharacterName = name,
			MouseDir = dirPath,
			MousePhenomeImagePair = new Dictionary<string, string>{
				{"a",imgPath ?? ""},
				{"i",imgPath ?? ""},
				{"u",imgPath ?? ""},
				{"e",imgPath ?? ""},
				{"o",imgPath ?? ""},
				{"N",imgPath ?? ""},
			}
		};
	}
}

