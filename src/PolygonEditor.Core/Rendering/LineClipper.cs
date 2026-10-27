using PolygonEditor.Core.Geometry;

namespace PolygonEditor.Core.Rendering;

/// <summary>
/// Liang–Barsky clipping of a segment to an axis-aligned rectangle. Applied before rasterizing,
/// so off-screen parts (e.g. of a huge arc) cost nothing and never reach int conversions or GDI+.
/// </summary>
public static class LineClipper
{
    /// <summary>
    /// Segment P(t) = a + t·(b − a), t ∈ [0, 1]. Each rectangle side gives an inequality p·t ≤ q;
    /// entering sides (p &lt; 0) raise t0, leaving sides (p &gt; 0) lower t1. Empty if t0 &gt; t1.
    /// </summary>
    /// <returns>false if the segment lies entirely outside the rectangle.</returns>
    public static bool Clip(ref Vec2 a, ref Vec2 b, double minX, double minY, double maxX, double maxY)
    {
        Vec2 d = b - a;
        double t0 = 0, t1 = 1;

        bool Side(double p, double q)
        {
            if (Math.Abs(p) < 1e-12)
                return q >= 0; // parallel to this side: inside or outside entirely
            double t = q / p;
            if (p < 0)
            {
                if (t > t1) return false;
                if (t > t0) t0 = t;
            }
            else
            {
                if (t < t0) return false;
                if (t < t1) t1 = t;
            }
            return true;
        }

        if (!Side(-d.X, a.X - minX) || !Side(d.X, maxX - a.X) ||
            !Side(-d.Y, a.Y - minY) || !Side(d.Y, maxY - a.Y))
            return false;

        Vec2 start = a;
        a = start + d * t0;
        b = start + d * t1;
        return true;
    }

    /// <summary>
    /// Clips to the render target with a small margin (line caps, rounding) plus half the line width,
    /// so a thick line whose axis runs just outside the target still shows its visible part.
    /// </summary>
    public static bool ClipToTarget(ref Vec2 a, ref Vec2 b, IRenderTarget target, int thickness = 1)
    {
        double margin = 2 + thickness / 2.0;
        return Clip(ref a, ref b, -margin, -margin, target.Width + margin, target.Height + margin);
    }
}
