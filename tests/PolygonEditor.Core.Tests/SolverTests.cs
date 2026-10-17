using PolygonEditor.Core.Constraints.Solver;
using PolygonEditor.Core.Editing;
using PolygonEditor.Core.Geometry;
using PolygonEditor.Core.Model;
using PolygonEditor.Core.Scenes;

namespace PolygonEditor.Core.Tests;

public class SolverTests
{
    private static void AssertNear(Vec2 expected, Vec2 actual, double tolerance = 1e-6) =>
        Assert.True(Vec2.Distance(expected, actual) <= tolerance, $"expected {expected}, got {actual}");

    private static double G1Error(Vec2 incoming, Vec2 outgoing) => (incoming.Normalized() - outgoing.Normalized()).Length;

    [Fact]
    public void FixedLength_DraggedVertexFollowsCursorAndLengthIsKept()
    {
        var scene = new Scene();
        var square = scene.AddPolygon((0, 0), (100, 0), (100, 100), (0, 100));
        scene.AddFixedLength(square.Edges[0]);
        var editor = new SceneEditor(scene);

        editor.MoveVertex(square.Vertices[0], new Vec2(-30, 20));

        AssertNear(new Vec2(-30, 20), square.Vertices[0].Position);
        Assert.Equal(100, square.Edges[0].Length, 1);
        Assert.False(editor.LastMoveWasRigid);
    }

    [Fact]
    public void AllEdgesFixed_SquareDeformsIntoRhombusWithoutTranslating()
    {
        var scene = new Scene();
        var square = scene.AddPolygon((0, 0), (100, 0), (100, 100), (0, 100));
        foreach (Edge edge in square.Edges)
            scene.AddFixedLength(edge);
        var editor = new SceneEditor(scene);

        editor.MoveVertex(square.Vertices[2], new Vec2(130, 90));

        AssertNear(new Vec2(130, 90), square.Vertices[2].Position);
        Assert.True(ConstraintSolver.AllSatisfied(scene));
        Assert.False(editor.LastMoveWasRigid);
    }

    [Fact]
    public void Parallel_AcrossPolygons_OtherEdgeRotates()
    {
        var scene = new Scene();
        var p1 = scene.AddPolygon((0, 0), (100, 0), (100, 100), (0, 100));
        var p2 = scene.AddPolygon((300, 0), (400, 0), (400, 100), (300, 100));
        scene.AddParallel(p1.Edges[0], p2.Edges[0]);
        var editor = new SceneEditor(scene);

        editor.MoveVertex(p1.Vertices[1], new Vec2(100, 40));

        AssertNear(new Vec2(100, 40), p1.Vertices[1].Position);
        Assert.True(ConstraintSolver.AllSatisfied(scene));
        Assert.True(Math.Abs(Vec2.Cross(p2.Edges[0].Chord.Normalized(), new Vec2(100, 40).Normalized())) < 1e-3);
    }

    [Fact]
    public void AddParallel_RotatesSecondEdgeAndKeepsFirst()
    {
        var scene = new Scene();
        var p1 = scene.AddPolygon((0, 0), (100, 20), (100, 100), (0, 100));
        var p2 = scene.AddPolygon((300, 0), (400, 0), (400, 100), (300, 100));
        var editor = new SceneEditor(scene);

        Assert.True(editor.AddParallel(p1.Edges[0], p2.Edges[0]));

        AssertNear(new Vec2(0, 0), p1.Vertices[0].Position);
        AssertNear(new Vec2(100, 20), p1.Vertices[1].Position);
        Assert.True(ConstraintSolver.AllSatisfied(scene));
    }

    [Fact]
    public void CanAddParallel_RejectsSameAndNeighbouringEdgesAndDuplicates()
    {
        var scene = new Scene();
        var p = scene.AddPolygon((0, 0), (100, 0), (100, 100), (0, 100));
        var editor = new SceneEditor(scene);

        Assert.False(editor.CanAddParallel(p.Edges[0], p.Edges[0]));
        Assert.False(editor.CanAddParallel(p.Edges[0], p.Edges[1]));
        Assert.False(editor.CanAddParallel(p.Edges[3], p.Edges[0]));
        Assert.True(editor.AddParallel(p.Edges[0], p.Edges[2]));
        Assert.False(editor.CanAddParallel(p.Edges[2], p.Edges[0]));
    }

    [Fact]
    public void SetFixedLength_ChangesEdgeKeepingStartVertex()
    {
        var scene = new Scene();
        var p = scene.AddPolygon((0, 0), (100, 0), (100, 100), (0, 100));
        var editor = new SceneEditor(scene);

        Assert.True(editor.SetFixedLength(p.Edges[0], 150));
        Assert.True(editor.SetFixedLength(p.Edges[0], 120)); // edits the same constraint

        Assert.Single(scene.Constraints);
        AssertNear(new Vec2(0, 0), p.Vertices[0].Position);
        Assert.Equal(120, p.Edges[0].Length, 1);
    }

    [Fact]
    public void G1_DraggingLineVertexRealignsControlPoint()
    {
        var scene = new Scene();
        var p = scene.AddPolygon((0, 0), (100, 0), (200, 100), (0, 100));
        Edge bezier = p.Edges[1];
        bezier.MakeBezier(new Vec2(150, 0), new Vec2(200, 50)); // G1 at vertex 1 by default (variant)
        var editor = new SceneEditor(scene);
        Assert.Equal(Continuity.G1, p.Vertices[1].Continuity);

        editor.MoveVertex(p.Vertices[0], new Vec2(0, -40));

        Vec2 v = p.Vertices[1].Position;
        AssertNear(new Vec2(100, 0), v);
        Assert.True(G1Error(v - p.Vertices[0].Position, bezier.Control1 - v) < 1e-3);
        Assert.Equal(50, Vec2.Distance(bezier.Control1, v), 1); // G1 keeps the handle length
    }

    [Fact]
    public void C1_DraggingControlPointMovesNeighbourButNotSegmentVertices()
    {
        var scene = new Scene();
        var p = scene.AddPolygon((0, 0), (90, 0), (200, 100), (0, 100));
        Edge bezier = p.Edges[1];
        p.Vertices[1].SetContinuity(Continuity.C1);
        bezier.MakeBezier(new Vec2(120, 0), new Vec2(200, 50));
        var editor = new SceneEditor(scene);

        editor.MoveControlPoint(bezier, 1, new Vec2(110, 20));

        AssertNear(new Vec2(110, 20), bezier.Control1);
        AssertNear(new Vec2(90, 0), p.Vertices[1].Position);
        AssertNear(new Vec2(200, 100), p.Vertices[2].Position);
        // C1 between a line and a Bezier: line vector = 3 * (V1 - V0)
        AssertNear(3 * (bezier.Control1 - p.Vertices[1].Position), p.Vertices[1].Position - p.Vertices[0].Position, 0.1);
    }

    [Fact]
    public void C1_BetweenTwoBeziers_ControlPointsAreMirrored()
    {
        var scene = new Scene();
        var p = scene.AddPolygon((0, 0), (100, 0), (200, 0), (100, 100));
        p.Vertices[1].SetContinuity(Continuity.C1);
        p.Edges[0].MakeBezier(new Vec2(30, -30), new Vec2(70, -20));
        p.Edges[1].MakeBezier(new Vec2(130, 20), new Vec2(170, 30));
        var editor = new SceneEditor(scene);

        editor.MoveControlPoint(p.Edges[0], 2, new Vec2(60, 30));

        AssertNear(new Vec2(140, -30), p.Edges[1].Control1, 0.05);
    }

    [Fact]
    public void SwitchingEdgeToBezier_AlignsNewControlPointsWithNeighbours()
    {
        var scene = new Scene();
        var p = scene.AddPolygon((0, 0), (100, 0), (150, 100), (0, 100));
        var editor = new SceneEditor(scene);

        editor.SetEdgeKind(p.Edges[1], EdgeKind.Bezier);

        Assert.True(ConstraintSolver.AllSatisfied(scene));
        AssertNear(new Vec2(100, 0), p.Vertices[1].Position);
        AssertNear(new Vec2(150, 100), p.Vertices[2].Position);
    }

    [Fact]
    public void Solver_ReportsFailureWhenEverythingIsPinned()
    {
        var scene = new Scene();
        var p = scene.AddPolygon((0, 0), (100, 0), (100, 100));
        scene.AddFixedLength(p.Edges[0], 50);

        bool solved = new ConstraintSolver().Solve(scene, new[] { PointRef.Of(p.Vertices[0]), PointRef.Of(p.Vertices[1]) });

        Assert.False(solved);
    }

    [Theory]
    [InlineData(17, -11)]
    [InlineData(-60, 45)]
    public void InitialScene_DraggingEveryVertexIsSolvedWithoutRigidFallback(double dx, double dy)
    {
        var scene = InitialScene.Create();
        var editor = new SceneEditor(scene);
        Assert.True(ConstraintSolver.AllSatisfied(scene));

        foreach (Vertex vertex in scene.Polygons.SelectMany(p => p.Vertices).ToList())
        {
            Vec2 target = vertex.Position + new Vec2(dx, dy);
            editor.MoveVertex(vertex, target);
            AssertNear(target, vertex.Position);
            Assert.True(ConstraintSolver.AllSatisfied(scene), $"after moving {vertex.Name}");
            Assert.False(editor.LastMoveWasRigid, $"rigid fallback for {vertex.Name}");
        }
    }

    [Fact]
    public void InitialScene_G1ControlPointDragKeepsSegmentVertices()
    {
        var scene = InitialScene.Create();
        var editor = new SceneEditor(scene);
        Edge bezier = scene.Polygons[0].Edges[1];
        Vec2 start = bezier.Start.Position, end = bezier.End.Position;
        Vec2 target = bezier.Control1 + new Vec2(30, -40);

        editor.MoveControlPoint(bezier, 1, target);

        AssertNear(target, bezier.Control1);
        Assert.True(ConstraintSolver.AllSatisfied(scene));
        Assert.False(editor.LastMoveWasRigid);
        AssertNear(start, bezier.Start.Position);
        AssertNear(end, bezier.End.Position);
    }

    /// <summary>
    /// C1 next to a fixed-length line forces |V − C2| = L/3, so dragging C2 must move the joint
    /// vertex (the "exceptional case" allowed by the specification) — but not the whole polygon.
    /// </summary>
    [Fact]
    public void InitialScene_C1ControlPointNextToFixedLengthMovesJointVertexOnly()
    {
        var scene = InitialScene.Create();
        var editor = new SceneEditor(scene);
        Edge bezier = scene.Polygons[0].Edges[1];
        Vec2 start = bezier.Start.Position;
        Vec2 target = bezier.Control2 + new Vec2(40, 25);

        editor.MoveControlPoint(bezier, 2, target);

        AssertNear(target, bezier.Control2);
        Assert.True(ConstraintSolver.AllSatisfied(scene));
        Assert.False(editor.LastMoveWasRigid);
        AssertNear(start, bezier.Start.Position);
    }
}
