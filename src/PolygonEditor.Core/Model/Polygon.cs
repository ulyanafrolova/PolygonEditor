using PolygonEditor.Core.Geometry;

namespace PolygonEditor.Core.Model;

/// <summary>
/// Closed polygon (self-intersections allowed). Edge i always connects
/// Vertices[i] with Vertices[(i + 1) % n].
/// </summary>
public sealed class Polygon
{
    private readonly List<Vertex> _vertices = new();
    private readonly List<Edge> _edges = new();

    internal Polygon(IEnumerable<Vec2> points, string name)
    {
        Name = name;
        foreach (Vec2 p in points)
            _vertices.Add(new Vertex(this, p));
        if (_vertices.Count < 3)
            throw new ArgumentException("A polygon needs at least 3 vertices.", nameof(points));

        for (int i = 0; i < _vertices.Count; i++)
            _edges.Add(new Edge(this, _vertices[i], _vertices[(i + 1) % _vertices.Count]));
    }

    public string Name { get; }
    public IReadOnlyList<Vertex> Vertices => _vertices;
    public IReadOnlyList<Edge> Edges => _edges;

    public int IndexOf(Vertex vertex) => _vertices.IndexOf(vertex);
    public int IndexOf(Edge edge) => _edges.IndexOf(edge);

    public Edge EdgeAfter(Vertex vertex) => _edges[IndexOf(vertex)];
    public Edge EdgeBefore(Vertex vertex) => _edges[(IndexOf(vertex) - 1 + _edges.Count) % _edges.Count];

    /// <summary>Moves the whole polygon, including Bezier control points.</summary>
    internal void Translate(Vec2 delta)
    {
        foreach (Vertex v in _vertices)
            v.Position += delta;
        foreach (Edge e in _edges)
            e.Translate(delta);
    }

    /// <summary>Rotates the whole polygon (with control points) around a point; its shape does not change.</summary>
    internal void Rotate(Vec2 center, double angle)
    {
        double cos = Math.Cos(angle), sin = Math.Sin(angle);
        Vec2 Turn(Vec2 p) => center + (p - center).Rotate(cos, sin);

        foreach (Vertex v in _vertices)
            v.Position = Turn(v.Position);
        foreach (Edge e in _edges)
        {
            e.Control1 = Turn(e.Control1);
            e.Control2 = Turn(e.Control2);
        }
    }

    /// <summary>
    /// Inserts a vertex in the middle of the edge (the curve's midpoint P(1/2) for curves).
    /// The edge is replaced by two new ones of the same kind:
    /// - Bezier: split with de Casteljau at t = 1/2, so the shape stays exactly the same
    ///   (each half has half the tangent length at the old ends — SceneEditor re-solves C1);
    /// - arc: two arcs through the arc's midpoint (G0 at the new vertex). An arc is defined by its
    ///   ends and continuity, so the half next to a G1 end keeps the original circle, a half with
    ///   G0 at both ends becomes a semicircle;
    /// - line: two straight edges.
    /// </summary>
    internal Vertex InsertVertex(Edge edge)
    {
        int index = IndexOf(edge);
        if (index < 0)
            throw new ArgumentException("Edge does not belong to this polygon.", nameof(edge));

        Vec2 p0 = edge.Start.Position, p3 = edge.End.Position;
        Vec2 p01 = default, p012 = default, p123 = default, p23 = default;
        Vec2 middlePoint;
        if (edge.Kind == EdgeKind.Bezier)
        {
            Vec2 p1 = edge.Control1, p2 = edge.Control2;
            p01 = Vec2.Lerp(p0, p1, 0.5);
            Vec2 p12 = Vec2.Lerp(p1, p2, 0.5);
            p23 = Vec2.Lerp(p2, p3, 0.5);
            p012 = Vec2.Lerp(p01, p12, 0.5);
            p123 = Vec2.Lerp(p12, p23, 0.5);
            middlePoint = Vec2.Lerp(p012, p123, 0.5);
        }
        else
        {
            middlePoint = EdgeGeometry.PointAt(edge, 0.5);
        }

        var middle = new Vertex(this, middlePoint);
        var first = new Edge(this, edge.Start, middle);
        var second = new Edge(this, middle, edge.End);

        // Topology first: making the halves Bezier consults the neighbouring edges of their ends.
        _vertices.Insert(index + 1, middle);
        _edges[index] = first;
        _edges.Insert(index + 1, second);

        switch (edge.Kind)
        {
            case EdgeKind.Bezier:
                first.MakeBezier(p01, p012);
                second.MakeBezier(p123, p23);
                break;
            case EdgeKind.Arc:
                first.MakeArc();
                second.MakeArc();
                break;
            default:
                first.MakeLine();
                second.MakeLine();
                break;
        }
        return middle;
    }

    /// <summary>
    /// Removes the vertex; its two edges are replaced by one edge. If both were Bezier segments the
    /// new one is a Bezier segment with the two outer control points (the tangents at its ends stay),
    /// if both were arcs it is an arc, otherwise a straight edge.
    /// Returns the removed edges so the scene can drop their constraints.
    /// </summary>
    internal IReadOnlyList<Edge> RemoveVertex(Vertex vertex)
    {
        if (_vertices.Count <= 3)
            throw new InvalidOperationException("A polygon must keep at least 3 vertices.");

        int index = IndexOf(vertex);
        if (index < 0)
            throw new ArgumentException("Vertex does not belong to this polygon.", nameof(vertex));
        int beforeIndex = (index - 1 + _edges.Count) % _edges.Count;
        Edge before = _edges[beforeIndex];
        Edge after = _edges[index];

        var merged = new Edge(this, before.Start, after.End);
        _edges[beforeIndex] = merged;
        _edges.RemoveAt(index);
        _vertices.RemoveAt(index);

        // Topology first: the kind change consults the neighbouring edges of the merged edge's ends.
        if (before.Kind == EdgeKind.Bezier && after.Kind == EdgeKind.Bezier)
            merged.MakeBezier(before.Control1, after.Control2);
        else if (before.Kind == EdgeKind.Arc && after.Kind == EdgeKind.Arc)
            merged.MakeArc();
        else
            merged.MakeLine();
        return new[] { before, after };
    }
}
