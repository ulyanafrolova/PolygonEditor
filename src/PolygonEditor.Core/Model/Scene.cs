using PolygonEditor.Core.Constraints;
using PolygonEditor.Core.Geometry;

namespace PolygonEditor.Core.Model;

/// <summary>
/// Root of the data model: all polygons plus all constraints. Constraints live here
/// (not inside edges) because one constraint may relate edges of different polygons.
/// </summary>
public sealed class Scene
{
    private readonly List<Polygon> _polygons = new();
    private readonly List<Constraint> _constraints = new();
    private int _nextConstraintId = 1;

    public IReadOnlyList<Polygon> Polygons => _polygons;
    public IReadOnlyList<Constraint> Constraints => _constraints;

    public Polygon AddPolygon(params (double X, double Y)[] points) =>
        AddPolygon(points.Select(p => new Vec2(p.X, p.Y)));

    public Polygon AddPolygon(IEnumerable<Vec2> points)
    {
        var polygon = new Polygon(points, $"W{_polygons.Count + 1}");
        _polygons.Add(polygon);
        return polygon;
    }

    /// <summary>Fixed edge length; by default the current length.</summary>
    public FixedLengthConstraint AddFixedLength(Edge edge, double? length = null) =>
        Add(new FixedLengthConstraint(_nextConstraintId++, edge, length ?? edge.Length));

    public ParallelConstraint AddParallel(Edge a, Edge b)
    {
        if (a == b)
            throw new ArgumentException("An edge cannot be parallel to itself.");
        return Add(new ParallelConstraint(_nextConstraintId++, a, b));
    }

    public bool RemoveConstraint(Constraint constraint) => _constraints.Remove(constraint);

    public IEnumerable<Constraint> ConstraintsOn(Edge edge) => _constraints.Where(c => c.Involves(edge));

    /// <summary>Adds a vertex in the middle of the edge and drops constraints of the replaced edge.</summary>
    public Vertex InsertVertex(Edge edge)
    {
        Vertex inserted = edge.Polygon.InsertVertex(edge);
        RemoveConstraintsOn(edge);
        return inserted;
    }

    /// <summary>Removes the vertex and drops constraints of both edges that met at it.</summary>
    public void RemoveVertex(Vertex vertex)
    {
        foreach (Edge removed in vertex.Polygon.RemoveVertex(vertex))
            RemoveConstraintsOn(removed);
    }

    private T Add<T>(T constraint) where T : Constraint
    {
        _constraints.Add(constraint);
        return constraint;
    }

    private void RemoveConstraintsOn(Edge edge) => _constraints.RemoveAll(c => c.Involves(edge));
}
