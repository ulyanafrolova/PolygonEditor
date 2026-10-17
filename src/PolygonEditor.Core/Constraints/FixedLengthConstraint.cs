using PolygonEditor.Core.Model;

namespace PolygonEditor.Core.Constraints;

/// <summary>Distance between the edge's endpoints must equal Length (for curves: the chord).</summary>
public sealed class FixedLengthConstraint : Constraint
{
    public FixedLengthConstraint(int id, Edge edge, double length) : base(id)
    {
        if (length <= 0)
            throw new ArgumentOutOfRangeException(nameof(length), "Length must be positive.");
        Edge = edge;
        Length = length;
    }

    public Edge Edge { get; }
    public double Length { get; set; }

    public override IReadOnlyList<Edge> Edges => new[] { Edge };
    public override string IconText => $"{Length:0}";
    public override string Description => $"Długość {Edge.Name} = {Length:0.#}";
    public override double Error => Math.Abs(Edge.Length - Length);
}
