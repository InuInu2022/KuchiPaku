using System;
using System.Linq;
using System.Collections.Generic;

namespace KuchiPaku.Models;

public class Lab
{
	public IEnumerable<LabLine>? Lines { get; }

	public Lab(string labData, int fps = 30)
	{
		Lines = labData
			.Split(new string[]{Environment.NewLine}, StringSplitOptions.RemoveEmptyEntries)
			.Where(s => !string.IsNullOrEmpty(s))    //空行無視
			.Select((v, i) =>
			{
				var a = v.Split(new string[]{" "},StringSplitOptions.RemoveEmptyEntries);
				return new LabLine(
					Convert.ToDouble(a[0]),
					Convert.ToDouble(a[1]),
					a[2],
					fps
				);
			});
	}

	/// <summary>
	/// 文章・小節単位に分割する
	/// </summary>
	/// <param name="threshold">分割基準秒数(sec.)</param>
	/// <returns></returns>
	public List<List<LabLine>> SplitToSentence(double threshold)
	{

		var t = threshold * 10000000;

		var l = this.Lines!
			.Where(v => v.Phoneme != "pau")     //無音の空白無視
			.Select(v =>
			{                                   //判定プロパティ生やす
				return (IsSep: false, v: v);
			});

		var result = new List<List<LabLine>>();
		var tmpList = new List<LabLine>();
		var len = l.ToList().Count;
		for (int i = 0; i < len; i++)
		{
			var c = l.ElementAt(i);

			if (i > 0)
			{
				var prev = l.ElementAt(i - 1);
				c.IsSep = (c.v.From - prev.v.To) >= t;
			}

			if (c.IsSep && tmpList.Count != 0)
			{
				//result = result!.Append(tmpList!);
				//result = (ObservableCollection<ObservableCollection<LabLine>>)result!.Append(tmpList);
				var copyed = new List<LabLine>(tmpList);

				result.Add(copyed);
				tmpList.Clear();

			}
			tmpList.Add(c.v);

			//Console.WriteLine("isSep:" + c.IsSep + "," + c.v.Phoneme);
		}

		return result;

	}
}

/// <summary>
/// 行単位
/// <c>[開始秒] [終了秒] [音素]</c>
/// </summary>
public class LabLine
{
	public double From { get; }
	public double To { get; }
	public string Phoneme { get; }

	public static int MovieFPS { get; set; } = 30;

	public LabLine(double from, double to, string phenome, int fps = 30)
	{
		From = from;
		To = to;
		Phoneme = phenome;
		MovieFPS = fps;

		//Console.WriteLine("p:" + Phoneme);
	}

	public int FrameFrom => ToFrame(From);

	public int FrameTo => ToFrame(To);

	public int FrameLen => FrameTo - FrameFrom;

	private int ToFrame(double Time)
	{
		return (int)(Time / 10000000 * MovieFPS);
	}
}