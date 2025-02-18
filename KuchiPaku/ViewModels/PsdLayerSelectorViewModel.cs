using Epoxy;

namespace KuchiPaku.ViewModels;

[ViewModel]
public class PsdLayerSelectorViewModel(
	string cid,
	string name
)
{
	public string Cid { get; init; } = cid;
	public string Name { get; init; } = name;
	public bool IsEnabledLayer { get; set; }
}
