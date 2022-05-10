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
	/// Rectangleオブジェクトを返すメソッド
	/// </summary>
	/// <param name="bmp"></param>
	/// <see cref="https://qiita.com/takutoy/items/b123dde5a699f65917b4"/>
	/// <returns></returns>
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
		for (int i = 3; i < rgbValues.Length; i += 4)
		{
			// Aの値が0なら透明ピクセル
			if (rgbValues[i] != 0)
			{
				int x = i / 4 % bmp.Width;
				int y = i / 4 / bmp.Width;

				if (x0 > x) x0 = x;
				if (y0 > y) y0 = y;
				if (x1 < x) x1 = x;
				if (y1 < y) y1 = y;
			}
		}

		return new Rectangle(x0, y0, x1 - x0, y1 - y0);
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