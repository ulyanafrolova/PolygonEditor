using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace PolygonEditor.App.Canvas;

/// <summary>
/// Bitmap backed by a pinned int[] buffer: SetPixel is a plain array write (Bitmap.SetPixel is far too slow
/// for per-pixel algorithms), while GDI+ can still draw into the same memory through Graphics.
/// </summary>
public sealed class DirectBitmap : IDisposable
{
    private GCHandle _handle;

    /// <summary>Upper bound on the pixel count (~ 400 MB at 4 bytes per pixel); the canvas never needs more.</summary>
    public const long MaxPixels = 100_000_000;

    public DirectBitmap(int width, int height)
    {
        long pixels = (long)width * height; // long: width * height can overflow int
        if (width <= 0 || height <= 0 || pixels > MaxPixels)
            throw new ArgumentOutOfRangeException(nameof(width), $"Invalid bitmap size {width} x {height}.");

        Width = width;
        Height = height;
        Bits = new int[pixels];
        _handle = GCHandle.Alloc(Bits, GCHandleType.Pinned);
        Bitmap = new Bitmap(width, height, width * 4, PixelFormat.Format32bppPArgb, _handle.AddrOfPinnedObject());
    }

    public int Width { get; }
    public int Height { get; }
    public int[] Bits { get; }
    public Bitmap Bitmap { get; }

    public void SetPixel(int x, int y, int argb)
    {
        if ((uint)x < (uint)Width && (uint)y < (uint)Height)
            Bits[x + y * Width] = argb;
    }

    /// <summary>Per channel: old + (new − old)·coverage; the result is opaque (the canvas background is).</summary>
    public void BlendPixel(int x, int y, int argb, double coverage)
    {
        if ((uint)x >= (uint)Width || (uint)y >= (uint)Height || coverage <= 0)
            return;
        if (coverage >= 1)
        {
            Bits[x + y * Width] = argb;
            return;
        }

        int old = Bits[x + y * Width];
        int Mix(int shift)
        {
            int from = (old >> shift) & 0xFF, to = (argb >> shift) & 0xFF;
            return (int)Math.Round(from + (to - from) * coverage) << shift;
        }
        Bits[x + y * Width] = unchecked((int)0xFF000000) | Mix(16) | Mix(8) | Mix(0);
    }

    public void Dispose()
    {
        Bitmap.Dispose();
        if (_handle.IsAllocated)
            _handle.Free();
    }
}
