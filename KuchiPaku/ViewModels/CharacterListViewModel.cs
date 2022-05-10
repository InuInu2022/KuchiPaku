using System;
using Epoxy;

namespace KuchiPaku.ViewModels;

[ViewModel]
public class CharacterListViewModel
{
	public string? Name { get; set; }
	public string? DirectoryPath { get; set; }

	public string? DefaultMouthImgPath { get; set; }
}
