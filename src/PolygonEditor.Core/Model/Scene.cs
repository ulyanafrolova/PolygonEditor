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
    private int _nextPolygonNumber = 1; // names stay unique after a polygon is removed

    public IReadOnlyList<Polygon> Polygons => _polygons;
    public IReadOnlyList<Constraint> Constraints => _constraints;

    internal Polygon AddPolygon(params (double X, double Y)[] points) =>
        AddPolygon(points.Select(p => new Vec2(p.X, p.Y)));

    internal Polygon AddPolygon(IEnumerable<Vec2> points)
    {
        var polygon = new Polygon(points, $"W{_nextPolygonNumber}");
        _nextPolygonNumber++;
        _polygons.Add(polygon);
        return polygon;
    }

    /// <summary>Removes the polygon together with every constraint that involves one of its edges.</summary>
    internal void RemovePolygon(Polygon polygon)
    {
        if (!_polygons.Remove(polygon))
            throw new ArgumentException("Polygon does not belong to this scene.", nameof(polygon));
        _constraints.RemoveAll(c => c.Edges.Any(e => e.Polygon == polygon));
    }

    /// <summary>Fixed edge length; by default the current length.</summary>
    internal FixedLengthConstraint AddFixedLength(Edge edge, double? length = null) =>
        Add(new FixedLengthConstraint(_nextConstraintId++, edge, length ?? edge.Length));

    internal ParallelConstraint AddParallel(Edge a, Edge b)
    {
        if (a == b)
            throw new ArgumentException("An edge cannot be parallel to itself.");
        return Add(new ParallelConstraint(_nextConstraintId++, a, b));
    }

    internal bool RemoveConstraint(Constraint constraint) => _constraints.Remove(constraint);

    public IEnumerable<Constraint> ConstraintsOn(Edge edge) => _constraints.Where(c => c.Involves(edge));

    /// <summary>Adds a vertex in the middle of the edge and drops constraints of the replaced edge.</summary>
    internal Vertex InsertVertex(Edge edge)
    {
        Vertex inserted = edge.Polygon.InsertVertex(edge);
        RemoveConstraintsOn(edge);
        return inserted;
    }

    /// <summary>Removes the vertex and drops constraints of both edges that met at it.</summary>
    internal void RemoveVertex(Vertex vertex)
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
