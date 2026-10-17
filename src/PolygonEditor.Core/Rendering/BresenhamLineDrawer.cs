using System.Drawing;
using PolygonEditor.Core.Geometry;

namespace PolygonEditor.Core.Rendering;

/// <summary>Own implementation of Bresenham's (midpoint) line algorithm: integer arithmetic, additions only.</summary>
public sealed class BresenhamLineDrawer : ILineDrawer
{
    private const int DashLength = 6;
    private readonly IRenderTarget _target;

    public BresenhamLineDrawer(IRenderTarget target) => _target = target;

    public void DrawLine(Vec2 a, Vec2 b, Color color, bool dashed = false)
    {
        int step = 0;
        foreach (var (x, y) in Rasterize((int)Math.Round(a.X), (int)Math.Round(a.Y), (int)Math.Round(b.X), (int)Math.Round(b.Y)))
        {
            if (!dashed || step / DashLength % 2 == 0)
                _target.SetPixel(x, y, color);
            step++;
        }
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
