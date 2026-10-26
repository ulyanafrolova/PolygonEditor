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

    /// <summary>
    /// Changes the edge kind; new control points are adjusted to the neighbours' continuity while
    /// the segment's vertices stay. Returns false and restores the previous state if unsolvable.
    /// </summary>
    public bool SetEdgeKind(Edge edge, EdgeKind kind)
    {
        if (edge.Kind == kind)
            return true;

        EdgeKind previousKind = edge.Kind;
        Vec2 control1 = edge.Control1, control2 = edge.Control2;
        var (startContinuity, startExplicit) = (edge.Start.Continuity, edge.Start.IsContinuityExplicit);
        var (endContinuity, endExplicit) = (edge.End.Continuity, edge.End.IsContinuityExplicit);

        return TryChange(
            () =>
            {
                switch (kind)
                {
                    case EdgeKind.Bezier: edge.MakeBezier(); break;
                    case EdgeKind.Arc: edge.MakeArc(); break;
                    default: edge.MakeLine(); break;
                }
            },
            () =>
            {
                edge.Restore(previousKind, control1, control2);
                edge.Start.RestoreContinuity(startContinuity, startExplicit);
                edge.End.RestoreContinuity(endContinuity, endExplicit);
            },
            new[] { PointRef.Of(edge.Start), PointRef.Of(edge.End) });
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
    /// Parallelism is transitive (a ∥ c and c ∥ b give a ∥ b), so the check is done on whole
    /// classes of mutually parallel edges. Rejected: a and b already parallel (also indirectly),
    /// and any merged class containing two neighbouring edges of one polygon — they would
    /// collapse onto one line.
    /// </summary>
    public bool CanAddParallel(Edge a, Edge b)
    {
        if (a == b)
            return false;
        HashSet<Edge> classA = ParallelClass(a);
        if (classA.Contains(b))
            return false;

        var merged = new List<Edge>(classA);
        merged.AddRange(ParallelClass(b));
        return !merged.Any(e => merged.Contains(e.Next));
    }

    /// <summary>All edges connected to the given one by parallel constraints (including itself).</summary>
    public HashSet<Edge> ParallelClass(Edge edge)
    {
        var parallels = Scene.Constraints.OfType<ParallelConstraint>().ToList();
        var result = new HashSet<Edge> { edge };
        var queue = new Queue<Edge>(result);
        while (queue.Count > 0)
        {
            Edge current = queue.Dequeue();
            foreach (ParallelConstraint c in parallels)
            {
                Edge? other = c.A == current ? c.B : c.B == current ? c.A : null;
                if (other is not null && result.Add(other))
                    queue.Enqueue(other);
            }
        }
        return result;
    }

    /// <summary>
    /// Adds a parallel constraint; edge a keeps its direction, b is turned to match.
    /// If b belongs to another polygon, that polygon is first rotated as a rigid body around the
    /// middle of b (by the smaller of the two angles that make b parallel), so its shape, curves and
    /// own constraints stay intact; the solver then only fixes what is still needed.
    /// Returns false and reverts if unsolvable.
    /// </summary>
    public bool AddParallel(Edge a, Edge b)
    {
        if (!CanAddParallel(a, b))
            return false;

        ParallelConstraint? added = null;
        return TryChange(
            () =>
            {
                if (a.Polygon != b.Polygon)
                    RotateToParallel(b, a.Chord);
                added = Scene.AddParallel(a, b);
            },
            () => Scene.RemoveConstraint(added!),
            new[] { PointRef.Of(a.Start), PointRef.Of(a.End) },
            new[] { PointRef.Of(a.Start) });
    }

    /// <summary>Rotates the polygon of <paramref name="edge"/> so the edge's chord becomes parallel to <paramref name="direction"/>.</summary>
    private static void RotateToParallel(Edge edge, Vec2 direction)
    {
        Vec2 current = edge.Chord.Normalized(), target = direction.Normalized();
        if (current == Vec2.Zero || target == Vec2.Zero)
            return;
        if (Vec2.Dot(current, target) < 0)
            target = -target; // keep the edge's orientation: turn by less than 90°
        double angle = Math.Atan2(Vec2.Cross(current, target), Vec2.Dot(current, target));
        Vec2 pivot = Vec2.Lerp(edge.Start.Position, edge.End.Position, 0.5);
        edge.Polygon.Rotate(pivot, angle);
    }

    public void RemoveConstraint(Constraint constraint) => Scene.RemoveConstraint(constraint);

    // ---------- topology ----------

    /// <summary>
    /// Inserts a vertex in the middle of the edge and re-solves with the vertices of the split edge
    /// pinned. Needed even though de Casteljau keeps the shape: each half has only half of the old
    /// tangent vector at the old ends, which breaks C1 there (and an arc becomes two segments).
    /// Returns false if the relations could not all be restored (the topology change stays).
    /// </summary>
    public bool InsertVertex(Edge edge, out Vertex inserted)
    {
        Vertex start = edge.Start, end = edge.End;
        inserted = Scene.InsertVertex(edge);
        return SolveOrKeep(PointRef.Of(start), PointRef.Of(inserted), PointRef.Of(end));
    }

    public bool CanRemoveVertex(Vertex vertex) => vertex.Polygon.Vertices.Count > 3;

    /// <summary>
    /// Removes the vertex; continuity at the neighbours is re-established with the new edge's ends
    /// pinned. Returns false if the relations could not all be restored (the topology change stays).
    /// </summary>
    public bool RemoveVertex(Vertex vertex)
    {
        Vertex before = vertex.EdgeBefore.Start;
        Scene.RemoveVertex(vertex);
        Edge merged = before.EdgeAfter;
        return SolveOrKeep(PointRef.Of(merged.Start), PointRef.Of(merged.End));
    }

    // ---------- helpers ----------

    private bool Solve(params PointRef[] pinned) => _solver.Solve(Scene, pinned);

    /// <summary>
    /// For topology changes, which cannot simply be undone: solve; if that fails, put the geometry
    /// back as it was right after the change (no half-solved state) and report false.
    /// </summary>
    private bool SolveOrKeep(params PointRef[] pinned)
    {
        var snapshot = GeometrySnapshot.Capture(Scene);
        if (Solve(pinned))
            return true;
        snapshot.Restore();
        return false;
    }

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
