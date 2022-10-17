using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;

namespace KuchiPaku.Core.Models;

public static class ConfigUtil
{
	public static Settings Settings { get; set; } = new();

	public static void LoadConfig(){
        var configuration = new ConfigurationBuilder()
               .SetBasePath(System.AppDomain.CurrentDomain.BaseDirectory)
               .AddJsonFile("appsettings.json")
               .Build();
		Settings = configuration.Get<Settings>();
	}
}

/// <summary>
/// 設定データ
/// </summary>
public class Settings{
    [JsonPropertyName("talkSoftInterfaces")]
    public TalkSoftInterface[]? TalkSoftInterfaces { get; set; }
}

public class TalkSoftInterface{
    [JsonPropertyName("type")]
    public string? Type {get;set;}

    [JsonPropertyName("engine")]
    public string? Engine {get;set;}

    [JsonPropertyName("env_prog")]
    public string? EnvironmentProgramVar { get; set; }

    [JsonPropertyName("dll")]
    public string? DllName { get; set; }

    [JsonPropertyName("dll_dir")]
    public string? DllPath { get; set; }

    [JsonPropertyName("service")]
    public string? Service {get;set;}

    [JsonPropertyName("talker")]
    public string? Talker {get;set;}

    [JsonPropertyName("agent")]
    public string? Agent {get;set;}

    [JsonPropertyName("restHost")]
    public string? RestHost { get; set; }
}