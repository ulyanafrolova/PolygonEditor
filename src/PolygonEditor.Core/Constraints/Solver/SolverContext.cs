using PolygonEditor.Core.Geometry;
using PolygonEditor.Core.Model;

namespace PolygonEditor.Core.Constraints.Solver;

/// <summary>
/// Mobility of every point during one solve. Weight 0 = pinned (never moved);
/// larger weight = farther from the dragged element = takes a larger share of corrections.
/// </summary>
public sealed class SolverContext
{
    private readonly IReadOnlyDictionary<PointRef, double> _weights;

    internal SolverContext(IReadOnlyDictionary<PointRef, double> weights) => _weights = weights;

    public double Weight(PointRef point) => _weights.TryGetValue(point, out double w) ? w : 1;

    /// <summary>
    /// Moves a point unless it is pinned. A vertex carries along the adjacent control points
    /// (handles stay attached to their vertex), unless those are pinned.
    /// </summary>
    public void Move(PointRef point, Vec2 position)
    {
        if (Weight(point) <= 0)
            return;

        if (point.Vertex is { } vertex)
        {
            Vec2 delta = position - vertex.Position;
            foreach (PointRef handle in AdjacentHandles(vertex))
                if (Weight(handle) > 0)
                    handle.Set(handle.Position + delta);
        }
        point.Set(position);
    }

    /// <summary>Moves a point (unless pinned) without carrying the adjacent control points.</summary>
    public void MoveAlone(PointRef point, Vec2 position)
    {
        if (Weight(point) > 0)
            point.Set(position);
    }

    /// <summary>Control points of Bezier edges that touch the vertex (the ones next to it).</summary>
    public static IEnumerable<PointRef> AdjacentHandles(Vertex vertex)
    {
        if (vertex.EdgeBefore.Kind == EdgeKind.Bezier)
            yield return PointRef.Control(vertex.EdgeBefore, 2);
        if (vertex.EdgeAfter.Kind == EdgeKind.Bezier)
            yield return PointRef.Control(vertex.EdgeAfter, 1);
    }
}
