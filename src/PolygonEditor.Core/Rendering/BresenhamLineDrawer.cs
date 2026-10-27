using System.Drawing;
using PolygonEditor.Core.Geometry;

namespace PolygonEditor.Core.Rendering;

/// <summary>Own implementation of Bresenham's (midpoint) line algorithm: integer arithmetic, additions only.</summary>
public sealed class BresenhamLineDrawer : ILineDrawer
{
    private const int DashLength = 6;
    private readonly IRenderTarget _target;

    public BresenhamLineDrawer(IRenderTarget target) => _target = target;

    /// <summary>
    /// Thickness by pixel replication: every pixel of the 1-px line becomes a run of pixels across
    /// the driving axis (vertical for flat lines, horizontal for steep ones), centered on the line.
    /// A run of n pixels across the driving axis is only n/√(1 + m²) wide perpendicular to the line
    /// (m = minor/major slope), so the run is w·√(1 + m²) long: a 45° line is as thick as a horizontal one.
    /// </summary>
    public void DrawLine(Vec2 a, Vec2 b, Color color, bool dashed = false, int thickness = 1)
    {
        if (!LineClipper.ClipToTarget(ref a, ref b, _target, thickness))
            return;

        int x1 = (int)Math.Round(a.X), y1 = (int)Math.Round(a.Y), x2 = (int)Math.Round(b.X), y2 = (int)Math.Round(b.Y);
        int major = Math.Max(Math.Abs(x2 - x1), Math.Abs(y2 - y1)), minor = Math.Min(Math.Abs(x2 - x1), Math.Abs(y2 - y1));
        bool flat = Math.Abs(x2 - x1) >= Math.Abs(y2 - y1);
        double slope = major == 0 ? 0 : (double)minor / major;
        int run = thickness <= 1 ? 1 : (int)Math.Round(thickness * Math.Sqrt(1 + slope * slope));
        int from = -(run - 1) / 2, to = from + run - 1;

        int step = 0;
        foreach (var (x, y) in Rasterize(x1, y1, x2, y2))
        {
            if (!dashed || step / DashLength % 2 == 0)
                for (int k = from; k <= to; k++)
                    _target.SetPixel(flat ? x : x + k, flat ? y + k : y, color);
            step++;
        }
    }

    /// <summary>
    /// Segment by segment; a thick polyline also gets a filled disc at every joint (round join,
    /// see DiscStamp), otherwise a curve turning past 45° shows a notch where the run direction switches.
    /// </summary>
    public void DrawPolyline(IReadOnlyList<Vec2> points, Color color, bool dashed = false, int thickness = 1)
    {
        for (int i = 0; i + 1 < points.Count; i++)
            DrawLine(points[i], points[i + 1], color, dashed, thickness);
        if (thickness <= 1 || dashed)
            return;
        foreach (Vec2 joint in DiscStamp.Joints(points, _target, thickness))
            foreach (var (x, y, coverage) in DiscStamp.Rasterize(joint, thickness / 2.0))
                if (coverage >= 0.5) // no antialiasing here: a pixel belongs to the disc if its center does
                    _target.SetPixel(x, y, color);
    }

    /// <summary>
    /// Midpoint algorithm generalised to all octants. The driving axis is the one with the larger
    /// delta; d decides between the "E" step (driving axis only) and "NE" step (both axes):
    /// d0 = 2*dMinor - dMajor, incrE = 2*dMinor, incrNE = 2*(dMinor - dMajor).
    /// </summary>
    public static IEnumerable<(int X, int Y)> Rasterize(int x1, int y1, int x2, int y2)
    {
        int dx = Math.Abs(x2 - x1), dy = Math.Abs(y2 - y1);
        int sx = x2 >= x1 ? 1 : -1, sy = y2 >= y1 ? 1 : -1;
        int x = x1, y = y1;
        yield return (x, y);

        if (dx >= dy)
        {
            int d = 2 * dy - dx, incrE = 2 * dy, incrNE = 2 * (dy - dx);
            while (x != x2)
            {
                if (d < 0)
                    d += incrE;
                else
                {
                    d += incrNE;
                    y += sy;
                }
                x += sx;
                yield return (x, y);
            }
        }
        else
        {
            int d = 2 * dx - dy, incrE = 2 * dx, incrNE = 2 * (dx - dy);
            while (y != y2)
            {
                if (d < 0)
                    d += incrE;
                else
                {
                    d += incrNE;
                    x += sx;
                }
                y += sy;
                yield return (x, y);
            }
        }
    }
}
