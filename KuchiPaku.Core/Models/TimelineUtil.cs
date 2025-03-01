using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using KuchiPaku.Models;

using Newtonsoft.Json.Linq;

public static class TimelineUtil
{

}

public class SearchTimeline
{
	public int Scene { get; }
	private readonly List<SearchItem> _items = [];
	private readonly Dictionary<int, IReadOnlyList<SearchItem>> _cache = [];
	bool _isSorted = true;

	private static readonly IComparer<SearchItem> StartComparer
		= Comparer<SearchItem>.Create(
			(a, b) => a.Start.CompareTo(b.Start)
		);

	public SearchTimeline(int scene)
	{
		Scene = scene;
	}

	public void Add(SearchItem item)
	{
		_items.Add(item);
		_cache.Clear();
		_isSorted = false;
	}

	public void BuildIndex()
	{
		if (!_isSorted)
		{
			_items.Sort(StartComparer);
			_isSorted = true;
		}
		_cache.Clear();
	}

	public IReadOnlyList<SearchItem> GetItemsAtFrame(
		int frame)
	{
		if (!_isSorted)
		{
			BuildIndex();
		}
		if (_cache.TryGetValue(frame, out var cachedResult))
			return cachedResult;

		List<SearchItem> result = new();

		foreach (var item in _items)
		{
			if (item.Start > frame)
				break; // ソート済みなので、これ以降は探索不要

			if (item.End > frame)
				result.Add(item);
		}

		IReadOnlyList<SearchItem> cachedList = result;
		_cache[frame] = cachedList;
		return cachedList;
	}
}

public record SearchItem
{
	public string CharacterName { get; }
	public int Start { get; }
	public int End => Start + Length;
	public int Length { get; }
	public int Layer { get; }
	public ReadOnlyMemory<string> Data { get; }

	public SearchItem(
		string chara,
		int start,
		int length,
		int layer,
		string[] data
	)
	{
		CharacterName = chara;
		Start = start;
		Length = length;
		Layer = layer;
		Data = data;
	}
}