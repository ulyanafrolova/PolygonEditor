using PolygonEditor.Core.Geometry;
using PolygonEditor.Core.Model;

namespace PolygonEditor.Core.Constraints.Solver;

/// <summary>
/// G1/C1 continuity at a vertex V between a Bezier segment and a line or another Bezier segment.
/// Not stored in the scene: derived on every solve from vertex continuity and edge kinds.
///
/// Each side of V is described by a "handle" H and a factor k such that the tangent vector is
///   incoming: k_in (V - H_in),   outgoing: k_out (H_out - V),
/// with H = adjacent control point and k = 3 for a Bezier (P'(0) = 3(V1 - V0)),
/// H = far vertex and k = 1 for a line (P'(t) = B - A).
///   G1: the two tangent vectors have the same direction;
///   C1: the two tangent vectors are equal.
/// Arcs need no relation here: an arc with G1 is constructed from its neighbour's tangent.
/// </summary>
internal sealed class ContinuityConstraint : ISolvable
{
    private readonly Vertex _vertex;
    private readonly PointRef _inHandle, _outHandle;
    private readonly double _inK, _outK;

    private ContinuityConstraint(Vertex vertex)
    {
        _vertex = vertex;
        Edge before = vertex.EdgeBefore, after = vertex.EdgeAfter;
        (_inHandle, _inK) = before.Kind == EdgeKind.Bezier ? (PointRef.Control(before, 2), 3.0) : (PointRef.Of(before.Start), 1.0);
        (_outHandle, _outK) = after.Kind == EdgeKind.Bezier ? (PointRef.Control(after, 1), 3.0) : (PointRef.Of(after.End), 1.0);
    }

    public static IEnumerable<ContinuityConstraint> For(Scene scene)
    {
        foreach (Polygon polygon in scene.Polygons)
            foreach (Vertex vertex in polygon.Vertices)
            {
                if (vertex.Continuity == Continuity.G0)
                    continue;
                EdgeKind before = vertex.EdgeBefore.Kind, after = vertex.EdgeAfter.Kind;
                if (before == EdgeKind.Arc || after == EdgeKind.Arc)
                    continue;
                if (before == EdgeKind.Bezier || after == EdgeKind.Bezier)
                    yield return new ContinuityConstraint(vertex);
            }
    }

    private bool IsC1 => _vertex.Continuity == Continuity.C1;

    public IEnumerable<PointRef> Points => new[] { PointRef.Of(_vertex), _inHandle, _outHandle };

    public double Tolerance => IsC1 ? 0.05 : 1e-4;

    public double Error
    {
        get
        {
            Vec2 incoming = (_vertex.Position - _inHandle.Position) * _inK;
            Vec2 outgoing = (_outHandle.Position - _vertex.Position) * _outK;
            if (incoming.LengthSquared < 1e-12 || outgoing.LengthSquared < 1e-12)
                return 0; // degenerate handle: tangent undefined
            return IsC1 ? (incoming - outgoing).Length : (incoming.Normalized() - outgoing.Normalized()).Length;
        }
    }

    // Preference of what to adjust: a control point is the natural thing to change; moving the far
    // vertex of a line reshapes a neighbouring edge; moving V (a vertex of the curved segment) is
    // the last resort, needed e.g. for C1 next to a line of fixed length.
    private const double ControlPointFactor = 1;
    private const double LineVertexFactor = 0.1;
    private const double JointVertexFactor = 0.25;

    public void Project(SolverContext context)
    {
        if (IsC1)
            ProjectC1(context);
        else
            ProjectG1(context);
    }

    private double Mobility(SolverContext context, PointRef handle) =>
        context.Weight(handle) * (handle.IsVertex ? LineVertexFactor : ControlPointFactor);

    /// <summary>
    /// C1 is linear: f = (k_in + k_out)·V − k_in·H_in − k_out·H_out = 0. Weighted least-change
    /// projection (as in position-based dynamics): x_i −= w_i·g_i·f / Σ w_j·g_j², where g is the
    /// gradient (k_in + k_out for V, −k_in for H_in, −k_out for H_out). A single step satisfies f = 0.
    /// V moves without its handles (moving them along would leave f unchanged).
    /// </summary>
    private void ProjectC1(SolverContext context)
    {
        PointRef v = PointRef.Of(_vertex);
        double gV = _inK + _outK;
        double wV = context.Weight(v) * JointVertexFactor, wIn = Mobility(context, _inHandle), wOut = Mobility(context, _outHandle);
        double denominator = wV * gV * gV + wIn * _inK * _inK + wOut * _outK * _outK;
        if (denominator <= 0)
            return;

        Vec2 f = _vertex.Position * gV - _inHandle.Position * _inK - _outHandle.Position * _outK;
        Vec2 lambda = f / denominator;
        Vec2 newV = _vertex.Position - lambda * (wV * gV);
        Vec2 newIn = _inHandle.Position + lambda * (wIn * _inK);
        Vec2 newOut = _outHandle.Position + lambda * (wOut * _outK);

        context.MoveAlone(v, newV);
        context.Move(_inHandle, newIn);
        context.Move(_outHandle, newOut);
    }

    /// <summary>
    /// G1: the more mobile handle is rotated around V onto the line given by the other handle,
    /// keeping its distance from V (so a fixed-length line stays fixed). If both handles are
    /// pinned, V is moved onto the segment between them instead.
    /// </summary>
    private void ProjectG1(SolverContext context)
    {
        double wIn = Mobility(context, _inHandle), wOut = Mobility(context, _outHandle);
        Vec2 v = _vertex.Position;

        if (wIn + wOut <= 0)
        {
            Vec2 a = _inHandle.Position, b = _outHandle.Position, ab = b - a;
            if (ab.LengthSquared < 1e-12) return;
            double t = Math.Clamp(Vec2.Dot(v - a, ab) / ab.LengthSquared, 0.05, 0.95);
            context.MoveAlone(PointRef.Of(_vertex), a + ab * t);
            return;
        }

        if (wOut >= wIn)
        {
            Vec2 incoming = v - _inHandle.Position;
            if (incoming.LengthSquared < 1e-12) return;
            double length = KeptLength(_outHandle.Position - v, incoming, _inK / _outK);
            context.Move(_outHandle, v + incoming.Normalized() * length);
        }
        else
        {
            Vec2 outgoing = _outHandle.Position - v;
            if (outgoing.LengthSquared < 1e-12) return;
            double length = KeptLength(v - _inHandle.Position, outgoing, _outK / _inK);
            context.Move(_inHandle, v - outgoing.Normalized() * length);
        }
    }

    /// <summary>Current handle length, or a sensible one if the handle collapsed onto the vertex.</summary>
    private static double KeptLength(Vec2 current, Vec2 other, double ratio) =>
        current.Length > 1e-6 ? current.Length : other.Length * ratio;
}
