using PolygonEditor.Core.Constraints.Solver;
using PolygonEditor.Core.Editing;
using PolygonEditor.Core.Geometry;
using PolygonEditor.Core.Model;
using PolygonEditor.Core.Scenes;

namespace PolygonEditor.Core.Tests;

/// <summary>Parallel constraints across polygons and degenerate (huge-loop) arcs.</summary>
public class ArcAndParallelTests
{
    private static double[] PairwiseDistances(Polygon polygon) =>
        polygon.Vertices.SelectMany((a, i) => polygon.Vertices.Skip(i + 1).Select(b => Vec2.Distance(a.Position, b.Position))).ToArray();

    [Fact]
    public void ParallelToEdgeOfAnotherPolygon_RotatesThatPolygonRigidly()
    {
        var scene = InitialScene.Create();
        var editor = new SceneEditor(scene);
        var w2 = scene.Polygons[1];
        var w3 = scene.Polygons[2];
        Edge arc = w3.Edges[1];
        double[] shapeBefore = PairwiseDistances(w3);
        double radiusBefore = ArcGeometry.Compute(arc)!.Value.Radius;

        Assert.True(editor.AddParallel(w2.Edges[2], arc));

        double[] shapeAfter = PairwiseDistances(w3);
        for (int i = 0; i < shapeBefore.Length; i++)
            Assert.Equal(shapeBefore[i], shapeAfter[i], 6);
        Assert.Equal(radiusBefore, ArcGeometry.Compute(arc)!.Value.Radius, 6);
        Assert.True(ConstraintSolver.AllSatisfied(scene));
    }

    /// <summary>Polygon whose edge 1 is a G1 arc starting at (100, 0) with tangent +x (from edge 0).</summary>
    private static (Scene, SceneEditor, Polygon) G1ArcScene()
    {
        var scene = new Scene();
        var p = scene.AddPolygon((0, 0), (100, 0), (100, 100), (0, 100));
        var editor = new SceneEditor(scene);
        Assert.True(editor.SetEdgeKind(p.Edges[1], EdgeKind.Arc));
        Assert.True(editor.SetContinuity(p.Vertices[1], Continuity.G1));
        return (scene, editor, p);
    }

    [Fact]
    public void DraggingArcEndBehindTheTangent_DoesNotCreateAHugeLoop()
    {
        var (scene, editor, p) = G1ArcScene();
        // Far end of the arc almost on the tangent line, behind the start: the exact G1 arc would be enormous.
        Vec2 target = new(-60, 1);

        editor.MoveVertex(p.Vertices[2], target);

        Assert.Equal(target, p.Vertices[2].Position);       // still follows the cursor…
        Assert.True(editor.LastMoveWasRigid);                // …by moving the polygon as a whole
        Assert.True(ArcGeometry.IsWellShaped(p.Edges[1]));
        Assert.True(ConstraintSolver.AllSatisfied(scene));
    }

    [Fact]
    public void MakingAnArcThatWouldLoopHugely_IsRejected()
    {
        var scene = new Scene();
        var p = scene.AddPolygon((0, 0), (100, 0), (-50, 2), (-60, 80));
        var editor = new SceneEditor(scene);
        Assert.True(editor.SetContinuity(p.Vertices[1], Continuity.G1));

        bool changed = editor.SetEdgeKind(p.Edges[1], EdgeKind.Arc);

        Assert.False(changed);
        Assert.Equal(EdgeKind.Line, p.Edges[1].Kind);
    }

    [Fact]
    public void ArcsUpToASemicircle_AreAlwaysWellShaped()
    {
        var (_, editor, p) = G1ArcScene();
        // Far end in front of the tangent: a flat arc with a large radius is fine.
        editor.MoveVertex(p.Vertices[2], new Vec2(400, 3));

        Assert.False(editor.LastMoveWasRigid);
        Assert.True(ArcGeometry.IsWellShaped(p.Edges[1]));
    }
}
