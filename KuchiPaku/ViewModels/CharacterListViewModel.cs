using System;
using Epoxy;

using KuchiPaku.Models;

namespace KuchiPaku.ViewModels;

[ViewModel]
public class CharacterListViewModel
{
	public string? Name { get; set; }
	public string? DirectoryPath { get; set; }

	public string? DefaultMouthImgPath { get; set; }

	public required string TachieType { get; init; }

	public bool IsExport { get; set; } = true;

	public string TacheTypeText
		=> TachieType switch
		{
			YmmpTachieType.PsdTachie => "PSD",
			YmmpTachieType.AnimationTachie => "Anim.",
			_ => "非対応",
		};
}
