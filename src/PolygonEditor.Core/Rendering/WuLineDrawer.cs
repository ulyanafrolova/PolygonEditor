using System.Drawing;
using PolygonEditor.Core.Geometry;

namespace PolygonEditor.Core.Rendering;

/// <summary>
/// Own implementation of Xiaolin Wu's antialiased algorithms for segments and circles (arcs).
/// Pixel (x, y) is the unit square centered at (x, y). Along the driving axis one column of pixels
/// is processed per step; in that column the ideal line covers an interval [lo, hi] of the other axis
/// and every pixel gets the intensity = length of its overlap with that interval.
/// For thickness 1 the interval is [y − ½, y + ½], which gives exactly Wu's pair of pixels with
/// intensities 1 − frac and frac. A thicker line simply has a longer interval: the inner pixels are
/// fully covered, the two outer ones partially (manual thickness, no library involved).
/// </summary>
public sealed class WuLineDrawer : ILineDrawer
{
    private const int DashLength = 6;
    private readonly IRenderTarget _target;

    public WuLineDrawer(IRenderTarget target) => _target = target;

    public void DrawLine(Vec2 a, Vec2 b, Color color, bool dashed = false, int thickness = 1) =>
        DrawPolyline(new[] { a, b }, color, dashed, thickness);

    /// <summary>
    /// Coverage of all segments is summed first (capped at 1) and blended once. At a joint both
    /// segments cover half of the shared pixel (xgap = ½); blending them one after the other would
    /// give only 1 − ½·½ = ¾ and leave a lighter spot at every joint of a curve.
    /// A thick polyline also gets an antialiased disc at every joint (round join, see DiscStamp);
    /// it is combined by maximum, so it fills the notch without darkening the line's smooth edges.
    /// </summary>
    public void DrawPolyline(IReadOnlyList<Vec2> points, Color color, bool dashed = false, int thickness = 1)
    {
        var coverage = new Dictionary<(int X, int Y), double>();
        if (thickness > 1 && !dashed)
            foreach (Vec2 joint in DiscStamp.Joints(points, _target, thickness))
                foreach (var (x, y, c) in DiscStamp.Rasterize(joint, thickness / 2.0))
                    coverage[(x, y)] = Math.Max(coverage.GetValueOrDefault((x, y)), c);
        var lineCoverage = new Dictionary<(int X, int Y), double>();
        for (int i = 0; i + 1 < points.Count; i++)
        {
            Vec2 a = points[i], b = points[i + 1];
            if (!LineClipper.ClipToTarget(ref a, ref b, _target, thickness))
                continue;

            // Dashes count steps along the driving axis (one column of pixels per step).
            bool steep = Math.Abs(b.Y - a.Y) > Math.Abs(b.X - a.X);
            int first = (int)Math.Round(steep ? Math.Min(a.Y, b.Y) : Math.Min(a.X, b.X));
            foreach (var (x, y, c) in RasterizeLine(a, b, thickness))
            {
                int step = (steep ? y : x) - first;
                if (!dashed || step / DashLength % 2 == 0)
                    lineCoverage[(x, y)] = lineCoverage.GetValueOrDefault((x, y)) + c;
            }
        }

        foreach (var (pixel, c) in lineCoverage)
            coverage[pixel] = Math.Max(coverage.GetValueOrDefault(pixel), Math.Min(1, c));
        foreach (var ((x, y), c) in coverage)
            _target.BlendPixel(x, y, color, c);
    }

    public void DrawArc(ArcShape arc, Color color, int thickness = 1)
    {
        foreach (var (x, y, coverage) in RasterizeArc(arc, thickness, 0, 0, _target.Width - 1, _target.Height - 1))
            _target.BlendPixel(x, y, color, coverage);
    }

    /// <summary>
    /// Wu's line. For a flat line (|dy| ≤ |dx|, otherwise x and y swap roles) the columns are
    /// x = round(x0) … round(x1); in column x the line's center is y = y0 + g·(x − x0), g = dy/dx.
    /// The vertical extent of the line is 1 for thickness 1 (classic Wu) and w·√(1 + g²) for a thicker
    /// one (the width w is measured perpendicular to the line). The end columns are weighted by the
    /// part of the pixel the segment really covers along x (Wu's "xgap"), so polylines join smoothly.
    /// </summary>
    public static IEnumerable<(int X, int Y, double Coverage)> RasterizeLine(Vec2 a, Vec2 b, int thickness = 1)
    {
        bool steep = Math.Abs(b.Y - a.Y) > Math.Abs(b.X - a.X);
        if (steep)
        {
            a = new Vec2(a.Y, a.X);
            b = new Vec2(b.Y, b.X);
        }
        if (a.X > b.X)
            (a, b) = (b, a);

        double dx = b.X - a.X, dy = b.Y - a.Y;
        double gradient = dx < 1e-12 ? 0 : dy / dx;
        double half = (thickness <= 1 ? 1 : thickness * Math.Sqrt(1 + gradient * gradient)) / 2;

        for (int x = (int)Math.Round(a.X); x <= (int)Math.Round(b.X); x++)
        {
            double xgap = Overlap(x, a.X, b.X);
            if (xgap <= 0)
                continue;
            double center = a.Y + gradient * (x - a.X);
            foreach (var (y, coverage) in Column(center - half, center + half))
                yield return steep ? (y, x, coverage * xgap) : (x, y, coverage * xgap);
        }
    }

    /// <summary>
    /// Wu's circle restricted to the arc's angles, for any (also non-integer) center and clipped to the
    /// rectangle [minX, maxX] × [minY, maxY]. Like for lines, the driving axis changes at 45°:
    /// pixels with |dy| ≥ |dx| (relative to the center) are produced column by column, where the circle
    /// crosses column x at |dy| = √(r² − dx²); the remaining ones row by row, symmetrically. The interval
    /// in a column is [|dy| − ½, |dy| + ½] for thickness 1 (classic Wu) and the ring
    /// [√(r_in² − dx²), √(r_out² − dx²)], r_in/out = r ∓ w/2, for a thicker arc. Each pixel center
    /// lies on one side of the 45° split only, so no pixel is blended twice.
    /// </summary>
    public static IEnumerable<(int X, int Y, double Coverage)> RasterizeArc(
        ArcShape arc, int thickness, int minX, int minY, int maxX, int maxY)
    {
        Vec2 c = arc.Center;
        double r = arc.Radius;
        double outer = r + (thickness <= 1 ? 0.5 : thickness / 2.0);
        double inner = Math.Max(0, r - thickness / 2.0);

        // (lo, hi) of |offset| across the driving axis at distance d along it; null when the ring misses it.
        (double Lo, double Hi)? Span(double d)
        {
            if (thickness <= 1)
            {
                double m2 = r * r - d * d;
                if (m2 < 0)
                    return null;
                double m = Math.Sqrt(m2);
                return (m - 0.5, m + 0.5);
            }
            double o2 = outer * outer - d * d;
            if (o2 <= 0)
                return null;
            double i2 = inner * inner - d * d;
            return (i2 > 0 ? Math.Sqrt(i2) : 0, Math.Sqrt(o2));
        }

        // Columns: pixels in the upper and lower parts of the circle (|dy| ≥ |dx|).
        int x0 = Math.Max(minX, (int)Math.Floor(c.X - outer)), x1 = Math.Min(maxX, (int)Math.Ceiling(c.X + outer));
        for (int x = x0; x <= x1; x++)
        {
            double dx = x - c.X;
            if (Span(dx) is not { } span)
                continue;
            var (lo, hi) = span;
            foreach (var (dyLo, dyHi) in Signed(lo, hi))
                foreach (var (y, coverage) in Column(c.Y + dyLo, c.Y + dyHi, minY, maxY))
                {
                    double dy = y - c.Y;
                    if (Math.Abs(dy) >= Math.Abs(dx) && OnArc(arc, dx, dy))
                        yield return (x, y, coverage);
                }
        }

        // Rows: pixels in the left and right parts (|dx| > |dy|).
        int y0 = Math.Max(minY, (int)Math.Floor(c.Y - outer)), y1 = Math.Min(maxY, (int)Math.Ceiling(c.Y + outer));
        for (int y = y0; y <= y1; y++)
        {
            double dy = y - c.Y;
            if (Span(dy) is not { } span)
                continue;
            var (lo, hi) = span;
            foreach (var (dxLo, dxHi) in Signed(lo, hi))
                foreach (var (x, coverage) in Column(c.X + dxLo, c.X + dxHi, minX, maxX))
                {
                    double dx = x - c.X;
                    if (Math.Abs(dx) > Math.Abs(dy) && OnArc(arc, dx, dy))
                        yield return (x, y, coverage);
                }
        }
    }

    /// <summary>The span on both sides of the center; one span through the center if the ring has no hole there.</summary>
    private static IEnumerable<(double Lo, double Hi)> Signed(double lo, double hi)
    {
        if (lo <= 0)
        {
            yield return (-hi, hi);
            yield break;
        }
        yield return (lo, hi);
        yield return (-hi, -lo);
    }

    /// <summary>Pixels of one column covered by [lo, hi], with their coverage.</summary>
    private static IEnumerable<(int Index, double Coverage)> Column(double lo, double hi, int min = int.MinValue, int max = int.MaxValue)
    {
        int from = Math.Max(min, (int)Math.Floor(lo + 0.5)), to = Math.Min(max, (int)Math.Floor(hi + 0.5));
        for (int i = from; i <= to; i++)
        {
            double coverage = Overlap(i, lo, hi);
            if (coverage > 0)
                yield return (i, coverage);
        }
    }

    /// <summary>Length of the overlap of pixel [i − ½, i + ½] with [lo, hi].</summary>
    private static double Overlap(int i, double lo, double hi) => Math.Max(0, Math.Min(i + 0.5, hi) - Math.Max(i - 0.5, lo));

    /// <summary>True if the direction (dx, dy) from the center lies within the arc's sweep.</summary>
    private static bool OnArc(ArcShape arc, double dx, double dy)
    {
        double angle = Math.Atan2(dy, dx);
        double fromStart = arc.Sweep >= 0 ? angle - arc.StartAngle : arc.StartAngle - angle;
        fromStart %= 2 * Math.PI;
        if (fromStart < 0)
            fromStart += 2 * Math.PI;
        return fromStart <= Math.Abs(arc.Sweep);
    }
}
