using PolygonEditor.Core.Constraints;
using PolygonEditor.Core.Constraints.Solver;
using PolygonEditor.Core.Geometry;
using PolygonEditor.Core.Model;

namespace PolygonEditor.Core.Editing;

/// <summary>
/// Single entry point for every change the user makes to the scene (mouse, menu, keyboard).
/// After each change the constraint solver restores all relations; the element the user
/// manipulates is pinned, so it always ends up exactly where the cursor is.
/// </summary>
public sealed class SceneEditor
{
    private readonly ConstraintSolver _solver = new();

    public SceneEditor(Scene scene) => Scene = scene;

    public Scene Scene { get; }

    /// <summary>True if the last move could not be solved and the whole polygon was translated instead.</summary>
    public bool LastMoveWasRigid { get; private set; }

    // ---------- moving ----------

    /// <summary>
    /// Moves the vertex (with its adjacent control points) and re-solves with the vertex pinned.
    /// If the relations cannot be satisfied, the whole polygon is translated instead
    /// (a rigid translation preserves every constraint and continuity relation).
    /// </summary>
    public void MoveVertex(Vertex vertex, Vec2 target)
    {
        var snapshot = GeometrySnapshot.Capture(Scene);
        Vec2 delta = target - vertex.Position;
        foreach (PointRef handle in SolverContext.AdjacentHandles(vertex))
            handle.Set(handle.Position + delta);
        vertex.Position = target;

        LastMoveWasRigid = !Solve(PointRef.Of(vertex));
        if (LastMoveWasRigid)
        {
            snapshot.Restore();
            vertex.Polygon.Translate(delta);
        }
    }

    /// <summary>
    /// Moves a control point. First the segment's own vertices are pinned too (a control point
    /// should not move its segment); only if that is unsolvable are they released. Last resort:
    /// translate the whole polygon.
    /// </summary>
    public void MoveControlPoint(Edge edge, int index, Vec2 target)
    {
        var point = PointRef.Control(edge, index);
        var snapshot = GeometrySnapshot.Capture(Scene);
        Vec2 delta = target - point.Position;

        LastMoveWasRigid = false;
        point.Set(target);
        if (Solve(point, PointRef.Of(edge.Start), PointRef.Of(edge.End)))
            return;

        snapshot.Restore();
        point.Set(target);
        if (Solve(point))
            return;

        snapshot.Restore();
        edge.Polygon.Translate(delta);
        LastMoveWasRigid = true;
    }

    /// <summary>Translation keeps every relation satisfied, so no solving is needed.</summary>
    public void MovePolygon(Polygon polygon, Vec2 delta)
    {
        polygon.Translate(delta);
        LastMoveWasRigid = false;
    }

    // ---------- edge kind and continuity ----------

    public void SetEdgeKind(Edge edge, EdgeKind kind)
    {
        if (edge.Kind == kind)
            return;
        switch (kind)
        {
            case EdgeKind.Bezier: edge.MakeBezier(); break;
            case EdgeKind.Arc: edge.MakeArc(); break;
            default: edge.MakeLine(); break;
        }
        // New control points are adjusted to the neighbours' continuity; the segment's vertices stay.
        Solve(PointRef.Of(edge.Start), PointRef.Of(edge.End));
    }

    public bool CanSetContinuity(Vertex vertex, Continuity continuity) => ContinuityRules.IsAllowed(vertex, continuity);

    /// <summary>Sets continuity and enforces it (vertex stays, handles adjust). Returns false and reverts if unsolvable.</summary>
    public bool SetContinuity(Vertex vertex, Continuity continuity)
    {
        if (!CanSetContinuity(vertex, continuity))
            return false;

        Continuity previous = vertex.Continuity;
        bool wasExplicit = vertex.IsContinuityExplicit;
        return TryChange(
            () => vertex.SetContinuity(continuity),
            () => vertex.RestoreContinuity(previous, wasExplicit),
            new[] { PointRef.Of(vertex) });
    }

    // ---------- constraints ----------

    public FixedLengthConstraint? FixedLengthOn(Edge edge) =>
        Scene.ConstraintsOn(edge).OfType<FixedLengthConstraint>().FirstOrDefault();

    /// <summary>Adds a fixed length (or changes the existing one). The start vertex stays. Returns false and reverts if unsolvable.</summary>
    public bool SetFixedLength(Edge edge, double length)
    {
        if (length <= 0)
            return false;

        FixedLengthConstraint? existing = FixedLengthOn(edge);
        PointRef[][] pins = { new[] { PointRef.Of(edge.Start) }, new[] { PointRef.Of(edge.End) } };
        if (existing is not null)
        {
            double previous = existing.Length;
            return TryChange(() => existing.Length = length, () => existing.Length = previous, pins);
        }

        FixedLengthConstraint? added = null;
        return TryChange(() => added = Scene.AddFixedLength(edge, length), () => Scene.RemoveConstraint(added!), pins);
    }

    /// <summary>
    /// Parallelism is allowed between any two edges except the same edge and two neighbouring
    /// edges of one polygon (they would collapse onto one line); a pair can be constrained once.
    /// </summary>
    public bool CanAddParallel(Edge a, Edge b) =>
        a != b
        && !(a.Polygon == b.Polygon && (a.Next == b || b.Next == a))
        && !Scene.Constraints.OfType<ParallelConstraint>().Any(c => (c.A == a && c.B == b) || (c.A == b && c.B == a));

    /// <summary>Adds a parallel constraint; edge a keeps its direction, b is rotated. Returns false and reverts if unsolvable.</summary>
    public bool AddParallel(Edge a, Edge b)
    {
        if (!CanAddParallel(a, b))
            return false;

        ParallelConstraint? added = null;
        return TryChange(
            () => added = Scene.AddParallel(a, b),
            () => Scene.RemoveConstraint(added!),
            new[] { PointRef.Of(a.Start), PointRef.Of(a.End) },
            new[] { PointRef.Of(a.Start) });
    }

    public void RemoveConstraint(Constraint constraint) => Scene.RemoveConstraint(constraint);

    // ---------- topology ----------

    /// <summary>Geometry does not change (Bezier is split exactly), so all remaining relations still hold.</summary>
    public Vertex InsertVertex(Edge edge) => Scene.InsertVertex(edge);

    public bool CanRemoveVertex(Vertex vertex) => vertex.Polygon.Vertices.Count > 3;

    /// <summary>Removes the vertex; continuity at the neighbours is re-established with the new edge's ends pinned.</summary>
    public void RemoveVertex(Vertex vertex)
    {
        Vertex before = vertex.EdgeBefore.Start;
        Scene.RemoveVertex(vertex);
        Edge merged = before.EdgeAfter;
        Solve(PointRef.Of(merged.Start), PointRef.Of(merged.End));
    }

    // ---------- helpers ----------

    private bool Solve(params PointRef[] pinned) => _solver.Solve(Scene, pinned);

    /// <summary>
    /// Applies a change and tries to solve with each set of pinned points in turn
    /// (starting from the same geometry). If none works, geometry and the change are reverted.
    /// </summary>
    private bool TryChange(Action apply, Action undo, params PointRef[][] pinAlternatives)
    {
        var snapshot = GeometrySnapshot.Capture(Scene);
        apply();
        foreach (PointRef[] pinned in pinAlternatives)
        {
            if (Solve(pinned))
                return true;
            snapshot.Restore();
        }
        undo();
        return false;
    }
}
