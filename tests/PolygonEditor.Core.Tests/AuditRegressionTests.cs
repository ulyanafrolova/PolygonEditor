using PolygonEditor.Core.Constraints.Solver;
using PolygonEditor.Core.Editing;
using PolygonEditor.Core.Geometry;
using PolygonEditor.Core.Model;
using PolygonEditor.Core.Scenes;

namespace PolygonEditor.Core.Tests;

/// <summary>Regression tests for the problems found in the external code review.</summary>
public class AuditRegressionTests
{
    [Fact]
    public void BezierNextToArc_DoesNotGiveTheArcG1AtBothEnds()
    {
        var scene = InitialScene.Create();
        var editor = new SceneEditor(scene);
        var w3 = scene.Polygons[2];
        Edge arc = w3.Edges[1];

        Assert.True(editor.SetEdgeKind(w3.Edges[2], EdgeKind.Bezier));

        Assert.Equal(Continuity.G1, arc.Start.Continuity);
        Assert.Equal(Continuity.G0, arc.End.Continuity);
        Assert.True(ConstraintSolver.AllSatisfied(scene));
    }

    [Fact]
    public void G1BetweenTwoArcs_IsNotAllowedAndNormalizedAway()
    {
        var scene = new Scene();
        var p = scene.AddPolygon((0, 0), (100, 0), (200, 0), (100, 150));
        var editor = new SceneEditor(scene);
        editor.SetEdgeKind(p.Edges[0], EdgeKind.Arc);
        Assert.True(editor.SetContinuity(p.Vertices[1], Continuity.G1)); // arc next to a line: fine

        editor.SetEdgeKind(p.Edges[1], EdgeKind.Arc);                     // now arc–arc at vertex 1

        Assert.Equal(Continuity.G0, p.Vertices[1].Continuity);
        Assert.False(editor.CanSetContinuity(p.Vertices[1], Continuity.G1));
        Assert.False(editor.SetContinuity(p.Vertices[1], Continuity.G1));
        Assert.True(ConstraintSolver.AllSatisfied(scene));
    }

    [Fact]
    public void AllSatisfied_SeesBrokenContinuityAtArcs()
    {
        var scene = new Scene();
        var p = scene.AddPolygon((0, 0), (100, 0), (100, 100), (0, 100));
        p.Edges[1].MakeArc();
        p.Vertices[1].SetContinuity(Continuity.G1);
        p.Vertices[2].SetContinuity(Continuity.G1); // bypasses the rules on purpose: both arc ends G1

        Assert.False(ConstraintSolver.AllSatisfied(scene));
        Assert.Contains(p.Vertices[2], ContinuityRules.Violations(scene));
    }

    [Fact]
    public void InsertVertexOnBezier_KeepsC1AtTheOldEnds()
    {
        var scene = InitialScene.Create();
        var editor = new SceneEditor(scene);
        var w1 = scene.Polygons[0];
        Vec2 start = w1.Vertices[1].Position, end = w1.Vertices[2].Position;

        Assert.True(editor.InsertVertex(w1.Edges[1], out Vertex inserted));

        Assert.True(ConstraintSolver.AllSatisfied(scene));
        Assert.Equal(0, ContinuityRules.MeasureError(w1.Vertices[3]), 1); // old end with C1 (index shifted by 1)
        Assert.Equal(start, w1.Vertices[1].Position);
        Assert.Equal(end, w1.Vertices[3].Position);
        Assert.Same(inserted, w1.Vertices[2]);
    }

    [Fact]
    public void InsertVertexOnArc_PutsTheVertexOnTheArc()
    {
        var scene = InitialScene.Create();
        var editor = new SceneEditor(scene);
        Edge arc = scene.Polygons[1].Edges[1];
        Vec2 arcMiddle = EdgeGeometry.PointAt(arc, 0.5);

        editor.InsertVertex(arc, out Vertex inserted);

        Assert.True(Vec2.Distance(arcMiddle, inserted.Position) < 1e-9);
    }

    [Fact]
    public void Parallel_IsCheckedTransitively()
    {
        var scene = new Scene();
        var p = scene.AddPolygon((0, 0), (100, 0), (150, 80), (0, 100));
        var q = scene.AddPolygon((300, 0), (400, 10), (400, 100), (300, 100));
        var editor = new SceneEditor(scene);

        Assert.True(editor.AddParallel(q.Edges[0], p.Edges[0]));
        Assert.False(editor.CanAddParallel(q.Edges[0], p.Edges[1])); // p0 ∥ q0 ∥ p1, p0 and p1 are neighbours
        Assert.False(editor.CanAddParallel(p.Edges[3], q.Edges[0])); // same via the other neighbour p3

        Assert.True(editor.AddParallel(q.Edges[0], p.Edges[2]));     // p0 ∥ p2 indirectly: not neighbours, fine
        Assert.False(editor.CanAddParallel(p.Edges[0], p.Edges[2])); // already parallel (indirectly)
        Assert.True(ConstraintSolver.AllSatisfied(scene));
    }

    [Fact]
    public void RejectedEdgeKindChange_IsFullyUndone()
    {
        var scene = new Scene();
        var p = scene.AddPolygon((0, 0), (100, 0), (100, 100), (0, 100));
        var editor = new SceneEditor(scene);
        // Fixed length = 50 on an edge whose ends are pinned while changing the kind of that edge:
        // unsolvable, so the change must be reverted.
        scene.AddFixedLength(p.Edges[1], 50);

        bool changed = editor.SetEdgeKind(p.Edges[1], EdgeKind.Bezier);

        Assert.False(changed);
        Assert.Equal(EdgeKind.Line, p.Edges[1].Kind);
        Assert.Equal(Continuity.G0, p.Vertices[1].Continuity);
        Assert.Equal(Continuity.G0, p.Vertices[2].Continuity);
    }

    /// <summary>
    /// Random sequence of every editing operation on the demo scene. After each operation that
    /// reports success the scene must be fully consistent (relations, rules and measured continuity).
    /// </summary>
    [Fact]
    public void RandomEditingSession_KeepsTheSceneConsistent()
    {
        var scene = InitialScene.Create();
        var editor = new SceneEditor(scene);
        var random = new Random(12345);
        Vec2 Jitter(double r) => new(random.NextDouble() * 2 * r - r, random.NextDouble() * 2 * r - r);

        for (int step = 0; step < 600; step++)
        {
            Polygon polygon = scene.Polygons[random.Next(scene.Polygons.Count)];
            Edge edge = polygon.Edges[random.Next(polygon.Edges.Count)];
            Vertex vertex = polygon.Vertices[random.Next(polygon.Vertices.Count)];
            bool ok = true;
            string operation;

            var beziers = scene.Polygons.SelectMany(p => p.Edges).Where(e => e.Kind == EdgeKind.Bezier).ToList();
            var allEdges = scene.Polygons.SelectMany(p => p.Edges).ToList();

            switch (random.Next(12))
            {
                case <= 2:
                    operation = $"move {vertex.Name}";
                    editor.MoveVertex(vertex, vertex.Position + Jitter(8));
                    break;
                case <= 4 when beziers.Count > 0:
                    Edge bezier = beziers[random.Next(beziers.Count)];
                    int index = random.Next(1, 3);
                    operation = $"move C{index} {bezier.Name}";
                    editor.MoveControlPoint(bezier, index, (index == 1 ? bezier.Control1 : bezier.Control2) + Jitter(8));
                    break;
                case 9:
                    operation = $"fixed length {edge.Name}";
                    ok = editor.SetFixedLength(edge, edge.Length);
                    break;
                case 10:
                    Edge other = allEdges[random.Next(allEdges.Count)];
                    operation = $"parallel {edge.Name} {other.Name}";
                    ok = !editor.CanAddParallel(edge, other) || editor.AddParallel(edge, other);
                    break;
                case 5:
                    var kind = (EdgeKind)random.Next(3);
                    operation = $"kind {edge.Name} {kind}";
                    ok = editor.SetEdgeKind(edge, kind);
                    break;
                case 6:
                    var continuity = (Continuity)random.Next(3);
                    operation = $"continuity {vertex.Name} {continuity}";
                    ok = editor.SetContinuity(vertex, continuity) || !editor.CanSetContinuity(vertex, continuity);
                    break;
                case 7 when polygon.Vertices.Count < 9:
                    operation = $"insert on {edge.Name}";
                    ok = editor.InsertVertex(edge, out _);
                    break;
                case 8 when editor.CanRemoveVertex(vertex):
                    operation = $"remove {vertex.Name}";
                    ok = editor.RemoveVertex(vertex);
                    break;
                default:
                    operation = $"move polygon {polygon.Name}";
                    editor.MovePolygon(polygon, Jitter(8));
                    break;
            }

            if (ok)
                Assert.True(ConstraintSolver.AllSatisfied(scene), $"step {step}: {operation}");
        }
    }
}
