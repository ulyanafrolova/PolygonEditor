using PolygonEditor.Core.Geometry;
using PolygonEditor.Core.Model;

namespace PolygonEditor.Core.Editing;

/// <summary>
/// Positions of all vertices and control points. Used to cancel a drag (Esc); a drag may move
/// elements of other polygons too (through constraints), so the whole scene is captured.
/// </summary>
public sealed class GeometrySnapshot
{
    private readonly List<(Vertex Vertex, Vec2 Position)> _vertices = new();
    private readonly List<(Edge Edge, Vec2 Control1, Vec2 Control2)> _edges = new();

    public static GeometrySnapshot Capture(Scene scene)
    {
        var snapshot = new GeometrySnapshot();
        foreach (Polygon polygon in scene.Polygons)
        {
            foreach (Vertex v in polygon.Vertices)
                snapshot._vertices.Add((v, v.Position));
            foreach (Edge e in polygon.Edges)
                snapshot._edges.Add((e, e.Control1, e.Control2));
        }
        return snapshot;
    }

    public void Restore()
    {
        foreach (var (vertex, position) in _vertices)
            vertex.Position = position;
        foreach (var (edge, control1, control2) in _edges)
        {
            edge.Control1 = control1;
            edge.Control2 = control2;
        }
    }
}
