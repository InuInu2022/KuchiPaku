using System.Collections.Generic;

namespace KuchiPaku.Models;

/// <summary>リップシンク用オプション</summary>
public class LipSyncOption
{
	public string? CharacterName { get; set; }
	public string? TargetDir { get; set; }
	public string TachieType { get; set; } = YmmpTachieType.AnimationTachie;

	/// <summary>
	/// 音素と画像名のtupleペアCollection
	/// "音素文字列":"画像名"
	/// 動く立ち絵用
	/// </summary>
	public Dictionary<string, string> MousePhonemeImagePair { get; set; } = [];

	/// <summary>
	/// 音素と有効レイヤーリストのCollection
	/// "音素文字列":"有効レイヤー名リスト"
	/// PSD立ち絵用
	/// </summary>
	public Dictionary<string, IEnumerable<string>> MousePhonemeLayerPair { get; set; } = [];

	/// <summary>
	/// 音素と上書きレイヤーリスト
	/// "音素文字列":"有効レイヤー名true/falseリスト"
	/// PSD立ち絵用
	/// 動く立ち絵と同じ挙動にするオプションを有効化したときのみ使う
	/// </summary>
	public Dictionary<string, Dictionary<string, bool>> MousePhonemeOverrideLayerPair { get; set; } = [];

	/// <summary>
	/// 子音処理オプション
	/// * 0 - すべて「ん」として処理する
	/// * 1 - 口を閉じる子音以外は前の母音を引き継ぐ
	/// * 2 - 口を閉じる子音以外は前後の母音の形をより小さいもので補間
	/// </summary>
	public ConsonantOption ConsonantOption { get; set; }
		= ConsonantOption.CONTINUE_BEFORE_VOWEL;

	public static LipSyncOption GetDefault(
		string name,
		string dirPath,
		string tachieType,
		string? imgPath = null,
		IEnumerable<string>? layers = null
	)
	{
		return new LipSyncOption
		{
			CharacterName = name,
			TargetDir = dirPath,
			TachieType = tachieType,
			MousePhonemeImagePair = new Dictionary<string, string>
			{
				{ "a", imgPath ?? "" },
				{ "i", imgPath ?? "" },
				{ "u", imgPath ?? "" },
				{ "e", imgPath ?? "" },
				{ "o", imgPath ?? "" },
				{ "N", imgPath ?? "" },
			},
			MousePhonemeLayerPair = new()
			{
				{ "a", layers ?? []},
				{ "i", layers ?? []},
				{ "u", layers ?? []},
				{ "e", layers ?? []},
				{ "o", layers ?? []},
				{ "N", layers ?? []},
			}
		};
	}
}
