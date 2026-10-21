using PolygonEditor.Core.Geometry;
using PolygonEditor.Core.Model;

namespace PolygonEditor.Core.Constraints.Solver;

/// <summary>
/// Check-only relation: an arc must not degenerate into a huge loop (see ArcGeometry.LoopExcess).
/// An arc has no free parameters of its own, so there is nothing to project; when it is violated
/// the solve fails and the caller's usual fallback applies (the change is rejected, or the dragged
/// polygon is translated as a whole instead of being deformed).
/// </summary>
internal sealed class ArcShapeConstraint : ISolvable
{
    private readonly Edge _arc;

    private ArcShapeConstraint(Edge arc) => _arc = arc;

    public static IEnumerable<ArcShapeConstraint> For(Scene scene) =>
        scene.Polygons.SelectMany(p => p.Edges).Where(e => e.Kind == EdgeKind.Arc).Select(e => new ArcShapeConstraint(e));

    public IEnumerable<PointRef> Points => new[] { PointRef.Of(_arc.Start), PointRef.Of(_arc.End) };

    public double Error => ArcGeometry.LoopExcess(_arc);

    public double Tolerance => 1e-6;

    public void Project(SolverContext context)
    {
        // Nothing to move: the arc's shape follows from its ends and the neighbouring tangent.
    }
}
