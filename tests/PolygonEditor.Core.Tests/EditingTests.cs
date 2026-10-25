using PolygonEditor.Core.Editing;
using PolygonEditor.Core.Geometry;
using PolygonEditor.Core.Model;

namespace PolygonEditor.Core.Tests;

public class EditingTests
{
    private static EditorController SquareController(out Polygon square)
    {
        var scene = new Scene();
        square = scene.AddPolygon((100, 100), (300, 100), (300, 300), (100, 300));
        return new EditorController(new SceneEditor(scene));
    }

    [Fact]
    public void HitTest_PrefersControlPointThenVertexThenEdgeThenInterior()
    {
        var controller = SquareController(out var square);
        square.Edges[0].MakeBezier(new Vec2(150, 100), new Vec2(250, 60)); // control point lies on the edge

        Assert.IsType<ControlPointHit>(controller.HitTest(new Vec2(151, 101)));
        Assert.Equal(new VertexHit(square.Vertices[1]), controller.HitTest(new Vec2(302, 98)));
        Assert.Equal(new EdgeHit(square.Edges[1]), controller.HitTest(new Vec2(304, 200)));
        Assert.Equal(new PolygonHit(square), controller.HitTest(new Vec2(200, 200)));
        Assert.Null(controller.HitTest(new Vec2(500, 500)));
    }

    [Fact]
    public void HitTest_InteriorUsesEvenOddRuleForSelfIntersectingPolygon()
    {
        var scene = new Scene();
        var bowTie = scene.AddPolygon((0, 0), (200, 100), (200, 0), (0, 100));

        Assert.True(HitTester.Contains(bowTie, new Vec2(20, 50)));
        Assert.True(HitTester.Contains(bowTie, new Vec2(180, 50)));
        Assert.False(HitTester.Contains(bowTie, new Vec2(100, 10)));
    }

    [Fact]
    public void DragVertex_FollowsCursorKeepingGrabOffset()
    {
        var controller = SquareController(out var square);

        controller.MouseDown(new Vec2(103, 102));
        controller.MouseMove(new Vec2(153, 132));
        controller.MouseUp();

        Assert.Equal(new Vec2(150, 130), square.Vertices[0].Position);
        Assert.Equal(new Vec2(300, 100), square.Vertices[1].Position);
    }

    [Fact]
    public void DragControlPoint_DoesNotMoveSegmentVertices()
    {
        var controller = SquareController(out var square);
        square.Edges[0].MakeBezier(new Vec2(150, 50), new Vec2(250, 50));

        controller.MouseDown(new Vec2(250, 50));
        controller.MouseMove(new Vec2(260, 20));

        Assert.Equal(new Vec2(260, 20), square.Edges[0].Control2);
        Assert.Equal(new Vec2(100, 100), square.Vertices[0].Position);
        Assert.Equal(new Vec2(300, 100), square.Vertices[1].Position);
    }

    [Fact]
    public void DragInterior_MovesWholePolygonWithControlPoints()
    {
        var controller = SquareController(out var square);
        square.Edges[0].MakeBezier(new Vec2(150, 50), new Vec2(250, 50));

        controller.MouseDown(new Vec2(200, 200));
        controller.MouseMove(new Vec2(210, 205));
        controller.MouseMove(new Vec2(220, 210));

        Assert.Equal(new Vec2(120, 110), square.Vertices[0].Position);
        Assert.Equal(new Vec2(170, 60), square.Edges[0].Control1);
    }

    [Fact]
    public void CancelDrag_RestoresGeometry()
    {
        var controller = SquareController(out var square);

        controller.MouseDown(new Vec2(200, 200));
        controller.MouseMove(new Vec2(260, 260));
        Assert.True(controller.CancelDrag());

        Assert.Equal(new Vec2(100, 100), square.Vertices[0].Position);
        Assert.False(controller.IsDragging);
    }

    [Fact]
    public void ContinuityRules_ArcAllowsNoC1AndOnlyOneG1End()
    {
        var controller = SquareController(out var square);
        var editor = controller.Editor;
        Edge arc = square.Edges[1];
        editor.SetEdgeKind(arc, EdgeKind.Arc);

        Assert.False(editor.CanSetContinuity(arc.Start, Continuity.C1));
        editor.SetContinuity(arc.Start, Continuity.G1);
        Assert.False(editor.CanSetContinuity(arc.End, Continuity.G1));
        Assert.True(editor.CanSetContinuity(arc.End, Continuity.G0));
    }

    [Fact]
    public void MakeArc_NormalizesEndsThatBreakTheRules()
    {
        SquareController(out var square);
        Edge edge = square.Edges[1];
        edge.Start.SetContinuity(Continuity.C1);
        edge.End.SetContinuity(Continuity.G1);

        edge.MakeArc();

        Assert.Equal(Continuity.G1, edge.Start.Continuity);
        Assert.Equal(Continuity.G0, edge.End.Continuity);
    }

    [Fact]
    public void CreatePolygon_ClicksAddVerticesAndClickOnFirstClosesIt()
    {
        var controller = SquareController(out _);
        var scene = controller.Editor.Scene;
        controller.BeginCreatePolygon();

        controller.MouseDown(new Vec2(500, 100));
        controller.MouseDown(new Vec2(503, 102)); // too close to the last vertex: ignored
        controller.MouseDown(new Vec2(600, 100));
        controller.MouseDown(new Vec2(550, 200));
        Assert.Equal(3, controller.DraftPoints.Count);
        Assert.Single(scene.Polygons);

        Assert.True(controller.MouseDown(new Vec2(502, 98))); // first vertex closes the polygon

        Assert.False(controller.IsCreatingPolygon);
        Assert.Equal(2, scene.Polygons.Count);
        Polygon created = scene.Polygons[1];
        Assert.Equal(new Vec2(550, 200), created.Vertices[2].Position);
        Assert.All(created.Edges, e => Assert.Equal(EdgeKind.Line, e.Kind));
    }

    [Fact]
    public void CreatePolygon_EnterNeedsThreeVerticesAndEscCancels()
    {
        var controller = SquareController(out _);
        controller.BeginCreatePolygon();
        controller.MouseDown(new Vec2(500, 100));
        controller.MouseDown(new Vec2(600, 100));

        Assert.False(controller.FinishCreatePolygon());
        Assert.True(controller.IsCreatingPolygon);

        Assert.True(controller.CancelCreatePolygon());
        Assert.False(controller.IsCreatingPolygon);
        Assert.Single(controller.Editor.Scene.Polygons);

        controller.BeginCreatePolygon();
        foreach (var p in new[] { new Vec2(0, 0), new Vec2(50, 0), new Vec2(0, 50) })
            controller.MouseDown(p);
        Assert.True(controller.FinishCreatePolygon());
        Assert.Equal(2, controller.Editor.Scene.Polygons.Count);
    }

    [Fact]
    public void RemovePolygon_DropsItsConstraintsAndKeepsNamesUnique()
    {
        var scene = new Scene();
        var a = scene.AddPolygon((0, 0), (100, 0), (100, 100));
        var b = scene.AddPolygon((200, 0), (300, 0), (300, 100));
        var editor = new SceneEditor(scene);
        Assert.True(editor.SetFixedLength(a.Edges[0], 100));
        Assert.True(editor.AddParallel(a.Edges[0], b.Edges[0]));

        editor.RemovePolygon(b);

        Assert.Equal(new[] { a }, scene.Polygons);
        Assert.Single(scene.Constraints); // only a's length is left
        Assert.Equal("W3", editor.AddPolygon(new[] { new Vec2(0, 200), new Vec2(50, 200), new Vec2(0, 250) }).Name);
    }
}
