using PsdParser;
using PsdParser.AdditionalLayerInformations;

namespace KuchiPaku.Psd;

/// <summary>
/// PSD layer wrapper class for ymmp
/// </summary>
/// <param name="Cid">`nXX`形式の文字列</param>
/// <param name="Layer">元のLayer情報</param>
public record YmmPsdLayer(
	string Cid,
	LayerRecordAndImage Layer
)
{
	public string Cid { get; init; } = Cid;
	public LayerRecordAndImage Layer { get; init; } = Layer;
	public LayerImage Image { get; } = Layer.Image;
	public LayerRecord Record { get; } = Layer.Record;

	public YmmPsdLayer? Parent { get; set; }
	public IList<YmmPsdLayer> Children { get; set; } = [];
	public bool IsVisible { get; set; }
		= (Layer.Record.LayerFlags & LayerFlags.Visible) != LayerFlags.Visible;

	public string Name => Layer.Record
		.AdditionalLayerInformations
		.OfType<UnicodeLayerName>()
		.FirstOrDefault()?
		.Name ?? $"[{Cid}]";
	public bool IsFolder => Layer.IsFolder();
	public bool IsFolderOpened => Layer.IsFolderOpened();
	public bool IsDivider => Layer.IsDivider();

	public bool IsNormalLayer => !Layer.IsFolderLike();

	public string Identifier => $"{Cid}_{Name}_{Image.Width}x{Image.Height}";
}