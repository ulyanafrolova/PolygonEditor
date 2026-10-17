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

    public Polygon(IEnumerable<Vec2> points, string name)
    {
        Name = name;
        foreach (Vec2 p in points)
            _vertices.Add(new Vertex(this, p));
        if (_vertices.Count < 3)
            throw new ArgumentException("A polygon needs at least 3 vertices.", nameof(points));

        for (int i = 0; i < _vertices.Count; i++)
            _edges.Add(new Edge(this, _vertices[i], _vertices[(i + 1) % _vertices.Count]));
    }

    public string Name { get; set; }
    public IReadOnlyList<Vertex> Vertices => _vertices;
    public IReadOnlyList<Edge> Edges => _edges;

    public int IndexOf(Vertex vertex) => _vertices.IndexOf(vertex);
    public int IndexOf(Edge edge) => _edges.IndexOf(edge);

    public Edge EdgeAfter(Vertex vertex) => _edges[IndexOf(vertex)];
    public Edge EdgeBefore(Vertex vertex) => _edges[(IndexOf(vertex) - 1 + _edges.Count) % _edges.Count];

    /// <summary>Moves the whole polygon, including Bezier control points.</summary>
    public void Translate(Vec2 delta)
    {
        foreach (Vertex v in _vertices)
            v.Position += delta;
        foreach (Edge e in _edges)
            e.Translate(delta);
    }

    /// <summary>
    /// Inserts a vertex in the middle of the edge. The edge is replaced by two new ones;
    /// a Bezier edge is split with de Casteljau at t = 1/2 so the shape stays the same.
    /// </summary>
    internal Vertex InsertVertex(Edge edge)
    {
        int index = IndexOf(edge);
        if (index < 0)
            throw new ArgumentException("Edge does not belong to this polygon.", nameof(edge));

        Vertex middle;
        Edge first, second;
        if (edge.Kind == EdgeKind.Bezier)
        {
            Vec2 p0 = edge.Start.Position, p1 = edge.Control1, p2 = edge.Control2, p3 = edge.End.Position;
            Vec2 p01 = Vec2.Lerp(p0, p1, 0.5), p12 = Vec2.Lerp(p1, p2, 0.5), p23 = Vec2.Lerp(p2, p3, 0.5);
            Vec2 p012 = Vec2.Lerp(p01, p12, 0.5), p123 = Vec2.Lerp(p12, p23, 0.5);
            middle = new Vertex(this, Vec2.Lerp(p012, p123, 0.5));
            first = new Edge(this, edge.Start, middle);
            second = new Edge(this, middle, edge.End);
            first.MakeBezier(p01, p012);
            second.MakeBezier(p123, p23);
        }
        else
        {
            middle = new Vertex(this, Vec2.Lerp(edge.Start.Position, edge.End.Position, 0.5));
            first = new Edge(this, edge.Start, middle);
            second = new Edge(this, middle, edge.End);
        }

        _vertices.Insert(index + 1, middle);
        _edges[index] = first;
        _edges.Insert(index + 1, second);
        return middle;
    }

    /// <summary>
    /// Removes the vertex; its two edges are replaced by one straight edge.
    /// Returns the removed edges so the scene can drop their constraints.
    /// </summary>
    internal IReadOnlyList<Edge> RemoveVertex(Vertex vertex)
    {
        if (_vertices.Count <= 3)
            throw new InvalidOperationException("A polygon must keep at least 3 vertices.");

        int index = IndexOf(vertex);
        int beforeIndex = (index - 1 + _edges.Count) % _edges.Count;
        Edge before = _edges[beforeIndex];
        Edge after = _edges[index];

        _edges[beforeIndex] = new Edge(this, before.Start, after.End);
        _edges.RemoveAt(index);
        _vertices.RemoveAt(index);
        return new[] { before, after };
    }
}
