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

    public DirectBitmap(int width, int height)
    {
        Width = width;
        Height = height;
        Bits = new int[width * height];
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

    public void Dispose()
    {
        Bitmap.Dispose();
        if (_handle.IsAllocated)
            _handle.Free();
    }
}
