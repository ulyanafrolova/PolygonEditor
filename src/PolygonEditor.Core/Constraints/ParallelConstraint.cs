using PolygonEditor.Core.Geometry;
using PolygonEditor.Core.Model;

namespace PolygonEditor.Core.Constraints;

/// <summary>Chords of edges A and B must be parallel (same or opposite direction).</summary>
public sealed class ParallelConstraint : Constraint
{
    public ParallelConstraint(int id, Edge a, Edge b) : base(id)
    {
        A = a;
        B = b;
    }

    public Edge A { get; }
    public Edge B { get; }

    public override IReadOnlyList<Edge> Edges => new[] { A, B };
    public override string IconText => $"∥{Id}";
    public override string Description => $"Równoległość ∥{Id}: {A.Name} – {B.Name}";

    /// <summary>|sin| of the angle between the edges.</summary>
    public override double Error => Math.Abs(Vec2.Cross(A.Chord.Normalized(), B.Chord.Normalized()));
}
