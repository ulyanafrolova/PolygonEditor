using PolygonEditor.Core.Constraints.Solver;
using PolygonEditor.Core.Geometry;
using PolygonEditor.Core.Model;

namespace PolygonEditor.Core.Constraints;

/// <summary>Distance between the edge's endpoints must equal Length (for curves: the chord).</summary>
public sealed class FixedLengthConstraint : Constraint
{
    private double _length;

    public FixedLengthConstraint(int id, Edge edge, double length) : base(id)
    {
        Edge = edge;
        Length = length;
    }

    public Edge Edge { get; }

    public double Length
    {
        get => _length;
        set => _length = value > 0 ? value : throw new ArgumentOutOfRangeException(nameof(value), "Length must be positive.");
    }

    public override IReadOnlyList<Edge> Edges => new[] { Edge };
    public override string IconText => $"{Length:0}";
    public override string Description => $"Długość {Edge.Name} = {Length:0.#}";
    public override double Error => Math.Abs(Edge.Length - Length);
    public override double Tolerance => 0.05;

    /// <summary>
    /// Moves both endpoints along the edge line; the length error is split between them
    /// in proportion to their weights (a pinned endpoint does not move at all).
    /// </summary>
    public override void Project(SolverContext context)
    {
        PointRef a = PointRef.Of(Edge.Start), b = PointRef.Of(Edge.End);
        double wa = context.Weight(a), wb = context.Weight(b);
        if (wa + wb <= 0)
            return;

        Vec2 chord = b.Position - a.Position;
        double length = chord.Length;
        Vec2 direction = length < 1e-9 ? new Vec2(1, 0) : chord / length;
        double error = length - Length;

        Vec2 aTarget = a.Position + direction * (error * wa / (wa + wb));
        Vec2 bTarget = b.Position - direction * (error * wb / (wa + wb));
        context.Move(a, aTarget);
        context.Move(b, bTarget);
    }
}
