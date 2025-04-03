using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media.Imaging;

namespace KuchiPaku.ViewModels;

public static class ThumbUtil{

	/// <summary>
	/// 透明でないピクセルの上、下、左、右(y0, y1, x0, x1)の座標を検出し、
	/// Rectangleオブジェクトを返すメソッド。
	/// 不透明ピクセルが見つからない場合や、不正な矩形になる場合は元の画像サイズを返します。
	/// </summary>
	/// <param name="bmp">対象の画像</param>
	/// <see cref="https://qiita.com/takutoy/items/b123dde5a699f65917b4"/>
	/// <returns>不透明ピクセルを含む最小の矩形、または元の画像サイズの矩形</returns>
	public static Rectangle GetNoTransRect(Bitmap bmp)
	{
		// 画像のピクセルを byte[] にコピーする
		var rect = new Rectangle(0, 0, bmp.Width, bmp.Height);
		var bmpData = bmp.LockBits(rect, System.Drawing.Imaging.ImageLockMode.ReadOnly, bmp.PixelFormat);
		var bytes = Math.Abs(bmpData.Stride) * bmp.Height;
		var rgbValues = new byte[bytes];
		System.Runtime.InteropServices.Marshal.Copy(bmpData.Scan0, rgbValues, 0, bytes);
		bmp.UnlockBits(bmpData);

		int x0 = bmp.Width;
		int y0 = bmp.Height;
		int x1 = 0;
		int y1 = 0;

		// 透明でないピクセルを探す
		bool foundNonTransparent = false;
		for (int i = 3; i < rgbValues.Length; i += 4)
		{
			// Aの値が0なら透明ピクセル
			if (rgbValues[i] != 0)
			{
				foundNonTransparent = true;
				// ピクセルインデックスを計算
				int pixelIndex = i / 4;
				int x = pixelIndex % bmp.Width;
				int y = pixelIndex / bmp.Width;

				if (x0 > x) x0 = x;
				if (y0 > y) y0 = y;
				if (x1 < x) x1 = x;
				if (y1 < y) y1 = y;
			}
		}

		// 不透明ピクセルが見つからなかった場合、または幅/高さが0以下の場合は元のサイズを返す
		if (!foundNonTransparent || x1 < x0 || y1 < y0)
		{
			return new Rectangle(0, 0, bmp.Width, bmp.Height);
		}

		// 境界を含むために +1 する（x0,y0からx1,y1までの範囲）
		int width = x1 - x0 + 1;
		int height = y1 - y0 + 1;

		return new Rectangle(x0, y0, width, height);
	}

	public static Bitmap ToBitmap(this BitmapImage bmpImg){
		var encoder = new PngBitmapEncoder();
		encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bmpImg));
		System.Drawing.Bitmap? bitmap = null;
		using var ms = new System.IO.MemoryStream();
		encoder.Save(ms);
		ms.Seek(0, System.IO.SeekOrigin.Begin);
		using (var temp = new System.Drawing.Bitmap(ms))
		{
			bitmap = new System.Drawing.Bitmap(temp);
		}
		return bitmap;
	}

	public static Bitmap ToBitmap(this BitmapSource bitmapSource, PixelFormat pixelFormat)
	{
		int width = bitmapSource.PixelWidth;
		int height = bitmapSource.PixelHeight;
		int stride = width * ((bitmapSource.Format.BitsPerPixel + 7) / 8);
		IntPtr intPtr = IntPtr.Zero;
		try
		{
			intPtr = Marshal.AllocCoTaskMem(height * stride);
			bitmapSource.CopyPixels(new Int32Rect(0, 0, width, height), intPtr, height * stride, stride);
			using var bitmap = new Bitmap(width, height, stride, pixelFormat, intPtr);
			return new Bitmap(bitmap);
		}
		finally
		{
			if (intPtr != IntPtr.Zero)
			{
				Marshal.FreeCoTaskMem(intPtr);
			}
		}
	}

}