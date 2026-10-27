using System.Drawing.Drawing2D;
using System.Drawing.Text;
using PolygonEditor.Core.Geometry;
using PolygonEditor.Core.Rendering;

namespace PolygonEditor.App.Canvas;

/// <summary>IRenderTarget over a DirectBitmap: SetPixel writes to the buffer, the rest uses GDI+.</summary>
public sealed class BitmapRenderTarget : IRenderTarget, IDisposable
{
    private static readonly Font LabelFont = new("Segoe UI", 8f);

    private readonly DirectBitmap _bitmap;
    private readonly Graphics _graphics;

    public BitmapRenderTarget(DirectBitmap bitmap)
    {
        _bitmap = bitmap;
        _graphics = Graphics.FromImage(bitmap.Bitmap);
        _graphics.SmoothingMode = SmoothingMode.AntiAlias;
        _graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
    }

    public int Width => _bitmap.Width;
    public int Height => _bitmap.Height;

    public void Clear(Color color) => Array.Fill(_bitmap.Bits, color.ToArgb());

    public void SetPixel(int x, int y, Color color) => _bitmap.SetPixel(x, y, color.ToArgb());

    public void BlendPixel(int x, int y, Color color, double coverage) => _bitmap.BlendPixel(x, y, color.ToArgb(), coverage);

    /// <summary>Library thickness: pen width; round caps so the segments of a thick polyline join without notches.</summary>
    public void DrawLine(Vec2 a, Vec2 b, Color color, bool dashed, int thickness)
    {
        using var pen = new Pen(color, thickness) { DashStyle = dashed ? DashStyle.Dash : DashStyle.Solid };
        if (thickness > 1)
            pen.StartCap = pen.EndCap = LineCap.Round;
        _graphics.DrawLine(pen, (float)a.X, (float)a.Y, (float)b.X, (float)b.Y);
    }

    public void FillCircle(Vec2 center, double radius, Color color)
    {
        using var brush = new SolidBrush(color);
        _graphics.FillEllipse(brush, (float)(center.X - radius), (float)(center.Y - radius), (float)(2 * radius), (float)(2 * radius));
    }

    public void DrawCircle(Vec2 center, double radius, Color color)
    {
        using var pen = new Pen(color, 1.5f);
        _graphics.DrawEllipse(pen, (float)(center.X - radius), (float)(center.Y - radius), (float)(2 * radius), (float)(2 * radius));
    }

    public void DrawLabel(string text, Vec2 center, Color foreground, Color background)
    {
        SizeF size = _graphics.MeasureString(text, LabelFont);
        var box = new RectangleF((float)center.X - size.Width / 2 - 2, (float)center.Y - size.Height / 2,
            size.Width + 4, size.Height);

        using var backgroundBrush = new SolidBrush(background);
        using var textBrush = new SolidBrush(foreground);
        using var borderPen = new Pen(Color.FromArgb(160, 160, 160));
        _graphics.FillRectangle(backgroundBrush, box);
        _graphics.DrawRectangle(borderPen, box.X, box.Y, box.Width, box.Height);
        _graphics.DrawString(text, LabelFont, textBrush, box.X + 2, box.Y);
    }

    public void Dispose()
    {
        _graphics.Flush();
        _graphics.Dispose();
    }
}
