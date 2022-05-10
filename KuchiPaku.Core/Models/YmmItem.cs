using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace KuchiPaku.Models;

public interface IYmmItem
{
    public string Type { get; }
    public int Group { get; set; }
	public int Frame { get; set; }
	public int Layer { get; set; }
}

public abstract class YmmItem : IYmmItem{
    [JsonProperty("$type")]
	public abstract string Type { get; }
	[JsonProperty("Group")]
	public int Group { get ; set ; }
    [JsonProperty("Frame")]
	public int Frame { get ; set ; }
    [JsonProperty("Layer")]
	public int Layer { get ; set ; }
}

public class YmmVoiceItem : YmmItem
{
    [JsonProperty("$type")]
	public override string Type { get; } = YmmpItemType.VoiceItem;

    [JsonProperty("CharacterName")]
    public string? CharacterName { get; set; }

    [JsonProperty("Serif")]
    public string? Serif { get; set; }

    [JsonProperty("Hatsuon")]
    public string? Hatsuon { get; set; }

    [JsonProperty("VoiceLength")]
    public DateTimeOffset VoiceLength { get; set; }

    [JsonProperty("ContentOffset")]
    public TimeSpan ContentOffset { get; set; }

    [JsonProperty("VoiceParameter")]
    public VoiceParameter? VoiceParameter { get; set; }

    #region internal added

    public bool IsCustomVoice { get; set; }
    public bool HasLabFile { get; set; }
	#endregion
}

public class YmmTextItem : YmmItem
{
	[JsonProperty("$type")]
	public override string Type { get; } = YmmpItemType.TextItem;
}

public partial class YmmItemUtil
{
	public static T? FromJson<T>(string json) where T : IYmmItem
		=> JsonConvert
			.DeserializeObject<T>(
				json,
				Converter.Settings
			);
}


public partial class VoiceParameter
{
    [JsonProperty("$type")]
    public string? Type { get; set; }

    [JsonProperty("P1")]
    public long P1 { get; set; }

    [JsonProperty("P2")]
    public long P2 { get; set; }

    [JsonProperty("P3")]
    public long P3 { get; set; }

    [JsonProperty("P4")]
    public long P4 { get; set; }

    [JsonProperty("P5")]
    public long P5 { get; set; }

    [JsonProperty("Speed")]
    public long Speed { get; set; }

    [JsonProperty("Tone")]
    public long Tone { get; set; }

    [JsonProperty("Alpha")]
    public long Alpha { get; set; }

    [JsonProperty("ToneScale")]
    public long ToneScale { get; set; }

    [JsonProperty("Pitch")]
    public long? Pitch { get; set; }
    [JsonProperty("PitchRange")]
    public long? PitchRange { get; set; }
    [JsonProperty("MiddlePause")]
    public int? MiddlePause { get; set; }
    [JsonProperty("LongPause")]
    public int? LongPause { get; set; }
    [JsonProperty("SentensePause")]
    public int? SentensePause { get; set; }

    [JsonProperty("Preset")]
    public string? Preset { get; set; }
}

