using PolygonEditor.Core.Geometry;
using PolygonEditor.Core.Model;

namespace PolygonEditor.Core.Rendering;

/// <summary>Extent of everything that is drawn, used to size the scrollable drawing area.</summary>
public static class SceneBounds
{
    /// <summary>
    /// Largest x and y over vertices, control points and the sampled curves (arcs may bulge far
    /// beyond their vertices). Labels and markers are drawn around these points, so callers add a margin.
    /// </summary>
    public static Vec2 MaxCorner(Scene scene)
    {
        double maxX = 0, maxY = 0;
        void Include(Vec2 p)
        {
            maxX = Math.Max(maxX, p.X);
            maxY = Math.Max(maxY, p.Y);
        }

        foreach (Polygon polygon in scene.Polygons)
            foreach (Edge edge in polygon.Edges)
            {
                foreach (Vec2 p in CurveRasterizer.EdgePoints(edge))
                    Include(p);
                if (edge.Kind == EdgeKind.Bezier)
                {
                    Include(edge.Control1);
                    Include(edge.Control2);
                }
            }
        return new Vec2(maxX, maxY);
    }
}
