using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

using FluentCeVIOWrapper.Common;

using CeVIOTalk = FluentCeVIOWrapper.Common.Talk;

using KuchiPaku.Models;

namespace KuchiPaku.Core.Models;

public static class TTSUtil
{
	private static readonly ConcurrentDictionary<TTSProduct, ITTSManager> TTSmanagers = new();

	public static async ValueTask AwakeTTSAsync(
		TTSProduct tts,
		bool isWait = false
	){
		if(isWait){
			await CheckTTSInitAsync(tts);
		}else{
			var _ = await CheckTTSInitAsync(tts);
		}
	}

	public static async ValueTask StopTTSAsync(TTSProduct tts){
		if(!TTSmanagers.ContainsKey(tts))
		{
			return;
		}

		var result = TTSmanagers.TryGetValue(tts, out ITTSManager manager);
		if(!result){
			return;
		}

		//var manager = (TTSManager)TTSmanagers.Find(v => v.TTS == tts);
		await ((TTSManager)manager).StopAsync();
	}

	public static async ValueTask<bool> IsTTSAwakedAsync(TTSProduct tts){
		var manager = await CheckTTSInitAsync(tts);
		return (manager is not null) && manager.IsAwaked;
	}

	public static async ValueTask<ReadOnlyCollection<LabLine>> GetPhonemesAsync(
		Voice voice,
		VoiceParameter voiceParam,
		TTSProduct tts,
		string text,
		int fps,
		double playBackRate
    ){
		var manager = await CheckTTSInitAsync(tts);

		return await manager.GetPhonemeAsync(voice, voiceParam, text, fps, playBackRate);
	}

	private static async ValueTask<ITTSManager> CheckTTSInitAsync(
		TTSProduct tts
	)
	{
		//TODO: check awaked tts engine

		var targetTTS = await Task.Run(() =>
		{
			var path = ConfigUtil
				.Settings
				.TalkSoftInterfaces
				.First(v => TTSTabel.YmmVoiceToProduct[v.Type!] == tts)
				.DllPath;
			path = string.IsNullOrEmpty(path) ? "" : path;
			return TTSmanagers.GetOrAdd(
				tts,
				new TTSManager(
					tts,
					TTSTabel.TTStoType.ContainsKey(tts)
						? TTSTabel.TTStoType[tts]
						: APIType.TypeOther,
					path
					));
		});

        if(!targetTTS.IsAwaked
               && !targetTTS.IsAwaking){
			lock (targetTTS)
			{
				var _ = targetTTS.AwakeAsync();
			}
		}

		return targetTTS;
	}
}

class TTSManager : ITTSManager, IDisposable
{
	public TTSProduct TTS { get; }
    public APIType Type { get; }
	public bool IsAwaked { get => isAwaked; }
	public bool IsAwaking { get => isAwaking; }

	private Process? process;

	private FluentCeVIO? fcw;
	private bool isAwaked = false;
	private bool isAwaking = false;
	private bool disposedValue;
	private readonly string? dllPath;

	public TTSManager(
		TTSProduct tts,
		APIType type,
		string? dllPath = ""
    )
	{
		TTS = tts;
		Type = type;
		this.dllPath = dllPath;
	}

	public async ValueTask AwakeAsync()
	{
        switch (Type)
        {
			case APIType.TypeCeVIO:
			{
				//awake server
				isAwaking = true;
					var dll = !string.IsNullOrEmpty(dllPath)
						? $" -dllPath {dllPath}"
						: "";

					if(process is null || process.HasExited){
					var ps = new ProcessStartInfo()
					{
						FileName = Path.Combine(
							AppDomain.CurrentDomain.BaseDirectory,
							@"Server\FluentCeVIOWrapper.Server.exe"
						),
						Arguments = $"-cevio {TTS} -pipeName KuchiPaku_{TTS} {dll}",
						ErrorDialog = true,
						//RedirectStandardOutput = true,
						CreateNoWindow = !AppUtil.IsDebug,	//show console if debug
						UseShellExecute = false
					};
					ps.WorkingDirectory = Path.GetDirectoryName(ps.FileName);
					Debug.WriteLine($"process path: {ps.FileName}");
					try{
						process = Process.Start(ps);
					}catch (Exception e){
						Debug.WriteLine($"e:{e}");
						isAwaking = false;
					}

					//var bs = Encoding.GetEncoding("Shift_JIS").GetBytes();
					//var b8 = Encoding.GetEncoding("UTF-8").GetBytes(process.StandardOutput.ReadToEnd());
					//Debug.WriteLine("STDOUT "+Encoding.GetEncoding("UTF-8").GetString(b8));
					await Task.Delay(2000);
				}

                //Client lib awake
                var p = TTS switch
                {
                    TTSProduct.CeVIO_AI => Product.CeVIO_AI,
                    TTSProduct.CeVIO_CS => Product.CeVIO_CS,
                    _ => Product.CeVIO_AI
                };
                fcw = await FluentCeVIO.FactoryAsync(
                          pipeName: $"KuchiPaku_{TTS}",
                          product: p);
                var result = await fcw.StartAsync();
                if (result is null)
                {
                    throw new Exception("cevio start result is null");
                }
                else if (result != CeVIOTalk.HostStartResult.Succeeded){
					isAwaking = false;
					throw new Exception($"{TTS} awake Error:{result}");
                }

				isAwaking = false;
				isAwaked = true;
				Debug.WriteLine("FCW awaked.");

				break;
				//}
		    }

            case APIType.TypeOther:
            default:
				{
					isAwaking = false;
					//
					throw new NotSupportedException("not supported yet.");
					//break;
				}
	    }
    }

	public async ValueTask StopAsync()
	{
		switch (Type)
		{
			case APIType.TypeCeVIO:
			{
                await Task.Run(() => process?.Kill());
				isAwaked = false;
				isAwaking = false;
				await Task.Delay(1000);
				break;
            }

            default:
				break;
		}
	}

	public async ValueTask<ReadOnlyCollection<LabLine>> GetPhonemeAsync(
		Voice voice,
		VoiceParameter voiceParam,
		string text,
		int fps = 30,
		double playBackRate = 100.0
    ){
		switch (Type)
		{
			case APIType.TypeCeVIO:
			{
                if(fcw is null)
                {
                    throw new Exception($"{nameof(FluentCeVIO)} is null");
                }

                if(await fcw.GetIsHostStartedAsync()){
                    await fcw.StartAsync();
                }

				Debug.WriteLine(
					$"{voice.Arg}「{text}」\n"+
						$"	ALP {voiceParam.Alpha}\n"+
						$"	P_1 {voiceParam.P1}\n"+
						$"	P_2 {voiceParam.P2}\n"+
						$"	P_3 {voiceParam.P3}\n"+
						$"	P_4 {voiceParam.P4}\n"+
						$"	P_5 {voiceParam.P5}\n"+
						$"	PIT {voiceParam.Pitch}\n"+
						$"	PRN {voiceParam.PitchRange}\n"+
						$"	SPD {voiceParam.Speed}\n"+
						$"	TON {voiceParam.Tone}\n"+
						$"	TSC {voiceParam.ToneScale}\n"
				);

				//set voice param
				//await fcw.SetCastAsync(voice.Arg);
				var alpha = ParamConverter.Alpha(voiceParam.Alpha, Type);
				//await fcw.SetAlphaAsync((uint)voiceParam.Alpha);
				var speed = ParamConverter.Speed(voiceParam.Speed, Type);
				//await fcw.SetSpeedAsync((uint)speed);
				var tone = ParamConverter.Tone(voiceParam.Tone, Type);
                //await fcw.SetToneAsync((uint)tone);
				var toneScale = ParamConverter.ToneScale(voiceParam.ToneScale, Type);
                //await fcw.SetToneScaleAsync((uint)toneScale);
				//await fcw.SetVolumeAsync((uint)voiceParam);

				//set component
				//TODO:使いにくいのでAPI改良したい…
				var comps = await fcw.GetComponentsAsync();
				var newComps = new List<CeVIOTalk.TalkerComponent>();
				for (var i = 0; i < comps.Count; i++)
				{
					var v = comps[i];
					var pInfo = voiceParam
						.GetType()
						.GetProperty(
							$"P{i+1}",
							BindingFlags.Instance | BindingFlags.Public
						);
					v.Value = Convert.ToUInt32(pInfo.GetValue(voiceParam) ?? 0);
					newComps.Add(v);
				}

				await fcw.CreateParam()
					.Cast(voice.Arg)
					.Alpha((uint)alpha)
					.Speed((uint)speed)
					.Tone((uint)tone)
					.ToneScale((uint)toneScale)
					.Components(newComps.AsReadOnly())
					.SendAsync();

				//call cevio GetPhonemeAsync
				var phs = await fcw.GetPhonemesAsync(text);
				double rate = playBackRate / 100;

				return phs
					.Select(v =>
						new LabLine(
							v.StartTime * 10000000,
							v.EndTime * 10000000,
							v.Phoneme,
							fps
						))
					.ToList()
					.AsReadOnly();
            }

            default:
				break;
		}

		return new ReadOnlyCollection<LabLine>(null);
	}

	protected virtual void Dispose(bool disposing)
	{
		if (!disposedValue)
		{
			if (disposing)
			{
				// TODO: マネージド状態を破棄します (マネージド オブジェクト)
				StopAsync();
				process?.Kill();
			}

			// TODO: アンマネージド リソース (アンマネージド オブジェクト) を解放し、ファイナライザーをオーバーライドします
			// TODO: 大きなフィールドを null に設定します
			disposedValue = true;
		}
	}

	// // TODO: 'Dispose(bool disposing)' にアンマネージド リソースを解放するコードが含まれる場合にのみ、ファイナライザーをオーバーライドします
	// ~TTSManager()
	// {
	//     // このコードを変更しないでください。クリーンアップ コードを 'Dispose(bool disposing)' メソッドに記述します
	//     Dispose(disposing: false);
	// }

	public void Dispose()
	{
		// このコードを変更しないでください。クリーンアップ コードを 'Dispose(bool disposing)' メソッドに記述します
		Dispose(disposing: true);
		GC.SuppressFinalize(this);
	}
}

interface ITTSManager{
	TTSProduct TTS { get; }
    APIType Type { get; }
    bool IsAwaked { get;}
	bool IsAwaking { get; }

	ValueTask AwakeAsync();

	ValueTask<ReadOnlyCollection<LabLine>> GetPhonemeAsync(
		Voice voice,
		VoiceParameter setting,
		string text,
		int fps,
		double playBackRate
	);
}

//TODO:export to json
record TTSTabel{
	public static Dictionary<TTSProduct, APIType> TTStoType = new()
	{
		{TTSProduct.CeVIO_AI, APIType.TypeCeVIO},
		{TTSProduct.CeVIO_CS, APIType.TypeCeVIO},
		{TTSProduct.AIVOICE, APIType.TypeAIVOICE},
		{TTSProduct.VOICEVOX, APIType.TypeVOICEVOX},
		{TTSProduct.SHAREVOX, APIType.TypeVOICEVOX},
	};

    /// <summary>
	/// ymmpの <c>Voice.Api</c> から <c>TTSProduct</c> 変換
	/// </summary>
	/// <see cref="Voice.Api"/>
	public static Dictionary<string, TTSProduct> YmmVoiceToProduct = new()
	{
		{"CeVIO",       TTSProduct.CeVIO_CS},
		{"CeVIO AI",    TTSProduct.CeVIO_AI},
		{"voicevox",    TTSProduct.VOICEVOX},
		{"A.I.VOICE Editor",TTSProduct.AIVOICE},
	};
}

//TODO:export to json
public enum TTSProduct{
    CeVIO_AI,
    CeVIO_CS,
    AIVOICE,
    VOICEVOX,
    SHAREVOX,
}
public enum APIType{
    TypeCeVIO,
    TypeAIVOICE,
    TypeVOICEVOX,
    TypeOther = 99
}

public static class ParamConverter
{

	public static double Alpha(double alpha, APIType api){
		return api switch
		{
			APIType.TypeCeVIO => ((alpha / 100) + 1) / 2 * 100,
			_ => alpha	//TODO:support
		};
	}
	public static double Speed(double speed, APIType api)
	{
		//var type = TTSTabel.TTStoType[tts];
		return api switch
		{
			APIType.TypeCeVIO => (int)(50 / (speed / 100)),
			_ => speed  //TODO:support
		};
	}

	public static double Tone(double tone, APIType api)
	{
		//var type = TTSTabel.TTStoType[tts];
		return api switch
		{
			APIType.TypeCeVIO => ((tone / 600) + 1) / 2 * 100,
			_ => tone  //TODO:support
		};
	}

	public static double ToneScale(double toneScale, APIType api){
		return api switch
		{
			APIType.TypeCeVIO => 50 * (toneScale / 100),
			_ => toneScale  //TODO:support
		};
	}

}