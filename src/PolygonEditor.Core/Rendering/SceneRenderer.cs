using System.Drawing;
using PolygonEditor.Core.Editing;
using PolygonEditor.Core.Geometry;
using PolygonEditor.Core.Model;

namespace PolygonEditor.Core.Rendering;

public static class Palette
{
    public static readonly Color Background = Color.White;
    public static readonly Color Edge = Color.Black;
    public static readonly Color Curve = Color.FromArgb(20, 90, 200);
    public static readonly Color ControlPolygon = Color.FromArgb(150, 150, 150);
    public static readonly Color ControlPoint = Color.FromArgb(230, 120, 20);
    public static readonly Color Vertex = Color.FromArgb(200, 30, 30);
    public static readonly Color LabelText = Color.FromArgb(40, 40, 40);
    public static readonly Color ContinuityBackground = Color.FromArgb(225, 240, 225);
    public static readonly Color ConstraintBackground = Color.FromArgb(255, 245, 200);
    public static readonly Color Highlight = Color.FromArgb(0, 170, 90);
}

/// <summary>Draws the whole scene: edges (lines, Bezier curves, arcs), vertices, continuity and constraint icons.</summary>
public sealed class SceneRenderer
{
    private const double VertexRadius = 5;
    private const double ControlPointRadius = 4;

    /// <param name="highlight">Hovered or dragged element, drawn in the highlight color.</param>
    public void Render(Scene scene, IRenderTarget target, ILineDrawer lines, HitTarget? highlight = null)
    {
        target.Clear(Palette.Background);

        foreach (Polygon polygon in scene.Polygons)
            foreach (Edge edge in polygon.Edges)
                DrawEdge(edge, target, lines, IsHighlighted(edge, highlight));

        foreach (Polygon polygon in scene.Polygons)
            foreach (Vertex vertex in polygon.Vertices)
                DrawVertex(vertex, target);

        DrawConstraintIcons(scene, target);
        DrawHandleHighlight(highlight, target);
    }

    private static bool IsHighlighted(Edge edge, HitTarget? highlight) => highlight switch
    {
        EdgeHit e => e.Edge == edge,
        PolygonHit p => p.Target == edge.Polygon,
        _ => false,
    };

    private static void DrawEdge(Edge edge, IRenderTarget target, ILineDrawer lines, bool highlighted)
    {
        if (edge.Kind == EdgeKind.Bezier)
        {
            Vec2 a = edge.Start.Position, b = edge.End.Position, c1 = edge.Control1, c2 = edge.Control2;
            lines.DrawLine(a, c1, Palette.ControlPolygon, dashed: true);
            lines.DrawLine(c1, c2, Palette.ControlPolygon, dashed: true);
            lines.DrawLine(c2, b, Palette.ControlPolygon, dashed: true);
            target.FillCircle(c1, ControlPointRadius, Palette.ControlPoint);
            target.FillCircle(c2, ControlPointRadius, Palette.ControlPoint);
        }

        Color color = highlighted ? Palette.Highlight : edge.Kind == EdgeKind.Line ? Palette.Edge : Palette.Curve;
        lines.DrawPolyline(CurveRasterizer.EdgePoints(edge), color);
    }

    private static void DrawHandleHighlight(HitTarget? highlight, IRenderTarget target)
    {
        switch (highlight)
        {
            case VertexHit v:
                target.DrawCircle(v.Vertex.Position, VertexRadius + 4, Palette.Highlight);
                break;
            case ControlPointHit c:
                target.DrawCircle(c.Position, ControlPointRadius + 4, Palette.Highlight);
                break;
        }
    }

    private static void DrawVertex(Vertex vertex, IRenderTarget target)
    {
        target.FillCircle(vertex.Position, VertexRadius, Palette.Vertex);
        if (vertex.TouchesCurve)
            target.DrawLabel(vertex.Continuity.ToString(), vertex.Position + new Vec2(14, -14),
                Palette.LabelText, Palette.ContinuityBackground);
    }

    /// <summary>Icons sit next to the edge midpoint, on its normal; several icons on one edge are stacked.</summary>
    private static void DrawConstraintIcons(Scene scene, IRenderTarget target)
    {
        var slots = new Dictionary<Edge, int>();
        foreach (var constraint in scene.Constraints)
        {
            foreach (Edge edge in constraint.Edges)
            {
                int slot = slots.TryGetValue(edge, out int used) ? used : 0;
                slots[edge] = slot + 1;

                Vec2 normal = edge.Chord.Perp().Normalized();
                Vec2 position = EdgeGeometry.PointAt(edge, 0.5) + normal * (16 + 20 * slot);
                target.DrawLabel(constraint.IconText, position, Palette.LabelText, Palette.ConstraintBackground);
            }
        }
    }
}
