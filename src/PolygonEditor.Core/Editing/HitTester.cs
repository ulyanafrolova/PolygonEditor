using PolygonEditor.Core.Geometry;
using PolygonEditor.Core.Model;
using PolygonEditor.Core.Rendering;

namespace PolygonEditor.Core.Editing;

/// <summary>
/// Finds the element under the cursor. Priority: control point > vertex > edge > polygon interior,
/// so small handles stay grabbable even when they lie on an edge or inside another polygon.
/// Polygons drawn later (on top) win among equal kinds.
/// </summary>
public sealed class HitTester
{
    public double PointTolerance { get; init; } = 8;
    public double EdgeTolerance { get; init; } = 6;

    public HitTarget? HitTest(Scene scene, Vec2 p)
    {
        var polygons = scene.Polygons.Reverse().ToList();

        foreach (Polygon polygon in polygons)
            foreach (Edge edge in polygon.Edges.Where(e => e.Kind == EdgeKind.Bezier))
            {
                if (Vec2.Distance(p, edge.Control1) <= PointTolerance) return new ControlPointHit(edge, 1);
                if (Vec2.Distance(p, edge.Control2) <= PointTolerance) return new ControlPointHit(edge, 2);
            }

        foreach (Polygon polygon in polygons)
            foreach (Vertex vertex in polygon.Vertices)
                if (Vec2.Distance(p, vertex.Position) <= PointTolerance)
                    return new VertexHit(vertex);

        Edge? nearest = null;
        double nearestDistance = EdgeTolerance;
        foreach (Polygon polygon in polygons)
            foreach (Edge edge in polygon.Edges)
            {
                double distance = GeometryUtils.DistanceToPolyline(p, CurveRasterizer.EdgePoints(edge));
                if (distance <= nearestDistance)
                {
                    nearest = edge;
                    nearestDistance = distance;
                }
            }
        if (nearest is not null)
            return new EdgeHit(nearest);

        foreach (Polygon polygon in polygons)
            if (Contains(polygon, p))
                return new PolygonHit(polygon);

        return null;
    }

    /// <summary>Even-odd ray casting against the polygon outline (curves included as polylines).</summary>
    public static bool Contains(Polygon polygon, Vec2 p)
    {
        bool inside = false;
        foreach (Edge edge in polygon.Edges)
        {
            var points = CurveRasterizer.EdgePoints(edge);
            for (int i = 0; i + 1 < points.Count; i++)
            {
                Vec2 a = points[i], b = points[i + 1];
                if ((a.Y > p.Y) != (b.Y > p.Y) &&
                    p.X < a.X + (p.Y - a.Y) * (b.X - a.X) / (b.Y - a.Y))
                    inside = !inside;
            }
        }
        return inside;
    }
}
