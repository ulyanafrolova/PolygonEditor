namespace PolygonEditor.Core.Geometry;

public static class GeometryUtils
{
    /// <summary>Distance from point p to segment ab (used for hit testing edges).</summary>
    public static double DistanceToSegment(Vec2 p, Vec2 a, Vec2 b)
    {
        Vec2 ab = b - a;
        double lengthSquared = ab.LengthSquared;
        if (lengthSquared < 1e-12)
            return Vec2.Distance(p, a);

        double t = Math.Clamp(Vec2.Dot(p - a, ab) / lengthSquared, 0, 1);
        return Vec2.Distance(p, a + ab * t);
    }

    /// <summary>Distance from point p to a polyline (curves are hit-tested via their sampled points).</summary>
    public static double DistanceToPolyline(Vec2 p, IReadOnlyList<Vec2> points)
    {
        double best = double.PositiveInfinity;
        for (int i = 0; i + 1 < points.Count; i++)
            best = Math.Min(best, DistanceToSegment(p, points[i], points[i + 1]));
        return best;
    }
}
