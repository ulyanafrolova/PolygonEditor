using PolygonEditor.Core.Model;

namespace PolygonEditor.Core.Constraints;

/// <summary>Geometric relation on one or more edges (possibly from different polygons).</summary>
public abstract class Constraint
{
    protected Constraint(int id) => Id = id;

    /// <summary>Number shown in the icon, so the two edges of a pair can be matched visually.</summary>
    public int Id { get; }

    public abstract IReadOnlyList<Edge> Edges { get; }

    /// <summary>Short text drawn near the middle of each constrained edge.</summary>
    public abstract string IconText { get; }

    /// <summary>How far the current geometry is from satisfying the constraint (0 = satisfied).</summary>
    public abstract double Error { get; }

    public bool Involves(Edge edge) => Edges.Contains(edge);
    public bool IsSatisfied(double tolerance = 1e-3) => Error <= tolerance;
}
