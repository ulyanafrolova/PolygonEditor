using PolygonEditor.Core.Constraints.Solver;
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
    public override double Tolerance => 1e-4;

    /// <summary>
    /// The less mobile edge (closer to the dragged element) dictates the common direction; with
    /// equal mobility the average direction is used. Each edge is rotated to it keeping its length,
    /// around the weighted point between its endpoints, so a pinned endpoint stays in place.
    /// </summary>
    public override void Project(SolverContext context)
    {
        double wA = Mobility(A, context), wB = Mobility(B, context);
        if (wA + wB <= 0)
            return;

        Vec2 uA = A.Chord.Normalized(), uB = B.Chord.Normalized();
        if (uA == Vec2.Zero || uB == Vec2.Zero)
            return;

        double sign = Vec2.Dot(uA, uB) < 0 ? -1 : 1; // keep the edges' current orientation
        Vec2 target = wA < wB ? uA : wB < wA ? uB * sign : (uA + uB * sign).Normalized();
        if (target == Vec2.Zero)
            target = uA;

        RotateEdge(A, target, context);
        RotateEdge(B, target * sign, context);
    }

    private static double Mobility(Edge edge, SolverContext context) =>
        context.Weight(PointRef.Of(edge.Start)) + context.Weight(PointRef.Of(edge.End));

    private static void RotateEdge(Edge edge, Vec2 direction, SolverContext context)
    {
        PointRef a = PointRef.Of(edge.Start), b = PointRef.Of(edge.End);
        double wa = context.Weight(a), wb = context.Weight(b);
        if (wa + wb <= 0)
            return;

        Vec2 pa = a.Position, pb = b.Position;
        Vec2 pivot = (pa * wb + pb * wa) / (wa + wb);
        Vec2 u = (pb - pa).Normalized();
        context.Move(a, pivot + direction * Vec2.Dot(pa - pivot, u));
        context.Move(b, pivot + direction * Vec2.Dot(pb - pivot, u));
    }
}
