using PolygonEditor.Core.Geometry;

namespace PolygonEditor.Core.Rendering;

/// <summary>
/// Filled disc ("stamp") put at the joints of a thick polyline. Each segment of a thick line is cut
/// across its driving axis, so where a curve turns past 45° (the driving axis switches) the two cut
/// ends leave a wedge-shaped notch on the outer side; a disc of diameter = thickness fills it
/// (round join). Pixel (x, y) is the unit square centered at (x, y), as in Wu's algorithm.
/// </summary>
public static class DiscStamp
{
    /// <summary>
    /// Pixels of the disc with antialiased coverage: 1 inside, falling linearly to 0 over one pixel
    /// across the rim (coverage ≈ r + ½ − distance of the pixel center).
    /// </summary>
    public static IEnumerable<(int X, int Y, double Coverage)> Rasterize(Vec2 center, double radius)
    {
        int x0 = (int)Math.Floor(center.X - radius - 0.5), x1 = (int)Math.Ceiling(center.X + radius + 0.5);
        int y0 = (int)Math.Floor(center.Y - radius - 0.5), y1 = (int)Math.Ceiling(center.Y + radius + 0.5);
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                double coverage = Math.Clamp(radius + 0.5 - Vec2.Distance(new Vec2(x, y), center), 0, 1);
                if (coverage > 0)
                    yield return (x, y, coverage);
            }
    }

    /// <summary>
    /// Interior points of the polyline (where two of its segments meet) whose disc can touch the
    /// target; off-screen joints (e.g. of a huge arc) are skipped.
    /// </summary>
    public static IEnumerable<Vec2> Joints(IReadOnlyList<Vec2> points, IRenderTarget target, int thickness)
    {
        for (int i = 1; i + 1 < points.Count; i++)
        {
            Vec2 p = points[i];
            if (p.X >= -thickness && p.Y >= -thickness && p.X <= target.Width + thickness && p.Y <= target.Height + thickness)
                yield return p;
        }
    }
}
