using PolygonEditor.Core.Geometry;
using PolygonEditor.Core.Model;

namespace PolygonEditor.Core.Rendering;

/// <summary>Turns curves into polylines; the polylines are then drawn with the selected line algorithm.</summary>
public static class CurveRasterizer
{
    /// <summary>Edge as a polyline (two points for a straight edge). Shared by drawing and hit testing.</summary>
    public static IReadOnlyList<Vec2> EdgePoints(Edge edge)
    {
        Vec2 a = edge.Start.Position, b = edge.End.Position;
        return edge.Kind switch
        {
            EdgeKind.Bezier => BezierPoints(a, edge.Control1, edge.Control2, b),
            EdgeKind.Arc when ArcGeometry.Compute(edge) is { } arc => ArcPoints(arc),
            _ => new[] { a, b },
        };
    }

    /// <summary>
    /// Cubic Bezier via the power basis P(t) = A3 t^3 + A2 t^2 + A1 t + A0 and forward differences:
    ///   A0 = V0, A1 = 3(V1 - V0), A2 = 3(V2 - 2V1 + V0), A3 = V3 - 3V2 + 3V1 - V0;
    ///   ΔP(0) = A3 d^3 + A2 d^2 + A1 d,  Δ²P(0) = 6 A3 d^3 + 2 A2 d^2,  Δ³P = 6 A3 d^3 (constant).
    /// Each next point costs three vector additions.
    /// </summary>
    public static Vec2[] BezierPoints(Vec2 v0, Vec2 v1, Vec2 v2, Vec2 v3, int segments)
    {
        Vec2 a0 = v0;
        Vec2 a1 = 3 * (v1 - v0);
        Vec2 a2 = 3 * (v2 - 2 * v1 + v0);
        Vec2 a3 = v3 - 3 * v2 + 3 * v1 - v0;

        double d = 1.0 / segments, d2 = d * d, d3 = d2 * d;
        Vec2 p = a0;
        Vec2 delta1 = a3 * d3 + a2 * d2 + a1 * d;
        Vec2 delta2 = 6 * d3 * a3 + 2 * d2 * a2;
        Vec2 delta3 = 6 * d3 * a3;

        var points = new Vec2[segments + 1];
        points[0] = p;
        for (int i = 1; i <= segments; i++)
        {
            p += delta1;
            delta1 += delta2;
            delta2 += delta3;
            points[i] = p;
        }
        points[segments] = v3; // remove accumulated rounding error at the end
        return points;
    }

    /// <summary>Number of steps proportional to the control polygon length (≈ 4 px per step).</summary>
    public static int BezierSegments(Vec2 v0, Vec2 v1, Vec2 v2, Vec2 v3)
    {
        double length = Vec2.Distance(v0, v1) + Vec2.Distance(v1, v2) + Vec2.Distance(v2, v3);
        return Math.Clamp((int)(length / 4), 4, 400);
    }

    public static Vec2[] BezierPoints(Vec2 v0, Vec2 v1, Vec2 v2, Vec2 v3) =>
        BezierPoints(v0, v1, v2, v3, BezierSegments(v0, v1, v2, v3));

    /// <summary>Arc points generated incrementally: each point is the previous radius vector rotated by a fixed angle.</summary>
    public static Vec2[] ArcPoints(ArcShape arc)
    {
        int segments = Math.Clamp((int)(arc.Radius * Math.Abs(arc.Sweep) / 4), 8, 720);
        double step = arc.Sweep / segments;
        double cos = Math.Cos(step), sin = Math.Sin(step);

        var points = new Vec2[segments + 1];
        Vec2 radius = Vec2.FromAngle(arc.StartAngle) * arc.Radius;
        points[0] = arc.Center + radius;
        for (int i = 1; i <= segments; i++)
        {
            radius = radius.Rotate(cos, sin);
            points[i] = arc.Center + radius;
        }
        points[segments] = arc.PointAt(1);
        return points;
    }
}
