using System;
using System.Text.RegularExpressions;

namespace KuchiPaku.Models;

/// <summary>
/// YMM4 itemの$type定義
/// </summary>
public static class YmmpItemType
{
	public static readonly string VoiceItem
        = "YukkuriMovieMaker.Project.Items.VoiceItem, YukkuriMovieMaker";
	public static readonly string TextItem
        = "YukkuriMovieMaker.Project.Items.TextItem, YukkuriMovieMaker";
}

public static class YmmpTachieType
{
	public const string PsdTachie
		= "YukkuriMovieMaker.Plugin.Tachie.Psd.PsdTachiePlugin, YukkuriMovieMaker.Plugin.Tachie.Psd, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null";
}

public static class YmmpVoiceParameterType
{
	public const string CeVIOAIKarin = "YukkuriMovieMaker.Voice.CeVIOAIVoiceParameterKarin, YukkuriMovieMaker";

	public static readonly Regex RegCeVIO = new(
		@"YukkuriMovieMaker\.Voice\." +
			@"CeVIO(AI)?VoiceParameter.+,\sYukkuriMovieMaker"
	);

	public const string VOICEVOX = "YukkuriMovieMaker.Voice.VOICEVOXVoiceParameter, YukkuriMovieMaker";
}
