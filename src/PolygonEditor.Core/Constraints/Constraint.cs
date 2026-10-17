using PolygonEditor.Core.Constraints.Solver;
using PolygonEditor.Core.Model;

namespace PolygonEditor.Core.Constraints;

/// <summary>Geometric relation defined by the user on one or more edges (possibly from different polygons).</summary>
public abstract class Constraint : ISolvable
{
    protected Constraint(int id) => Id = id;

    /// <summary>Number shown in the icon, so the two edges of a pair can be matched visually.</summary>
    public int Id { get; }

    public abstract IReadOnlyList<Edge> Edges { get; }

    /// <summary>Short text drawn near the middle of each constrained edge.</summary>
    public abstract string IconText { get; }

    /// <summary>Longer text for menus.</summary>
    public abstract string Description { get; }

    /// <summary>How far the current geometry is from satisfying the constraint (0 = satisfied).</summary>
    public abstract double Error { get; }

    public abstract double Tolerance { get; }

    public abstract void Project(SolverContext context);

    /// <summary>Constraints act on edge endpoints (for curved edges: on the chord).</summary>
    public IEnumerable<PointRef> Points => Edges.SelectMany(e => new[] { PointRef.Of(e.Start), PointRef.Of(e.End) });

    public bool Involves(Edge edge) => Edges.Contains(edge);
    public bool IsSatisfied() => Error <= Tolerance;
}
