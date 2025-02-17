using KuchiPaku.Psd;

namespace PsdTest;

public class UnitTest1
{
	[Fact]
	public async Task LoadPsdAsync()
	{
		try
		{
			using var psd = await PsdUtil.LoadPsdAsync(
				"D:\\videos\\MyVideo\\YMM4_Chara\\坂本アヒルさん_玉姫\\玉姫立ち絵素材\\玉姫立ち絵素材.psd"
			);
			Assert.NotNull(psd);
		}
		catch (System.Exception e)
		{
			Assert.Fail(e.Message);
		}
	}

	[Theory]
	[InlineData(
		"D:\\videos\\MyVideo\\YMM4_Chara\\坂本アヒルさん_玉姫\\玉姫立ち絵素材\\玉姫立ち絵素材.psd"
	)]
	public async Task ParsePsdLayersAsync(
		string path
	)
	{
		using var psd = await PsdUtil.LoadPsdAsync(
			path
		);
		try
		{
			var result = PsdUtil.ParsePsdLayers(psd);

			Assert.False(result is []);
		}
		catch (System.Exception e)
		{
			Assert.Fail(e.Message);
		}
	}
}
