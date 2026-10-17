using PolygonEditor.Core.Geometry;
using PolygonEditor.Core.Model;
using PolygonEditor.Core.Scenes;

namespace PolygonEditor.Core.Tests;

public class ModelTests
{
    private static Scene SquareScene(out Polygon square)
    {
        var scene = new Scene();
        square = scene.AddPolygon((0, 0), (100, 0), (100, 100), (0, 100));
        return scene;
    }

    private static void AssertClosedChain(Polygon polygon)
    {
        int n = polygon.Vertices.Count;
        Assert.Equal(n, polygon.Edges.Count);
        for (int i = 0; i < n; i++)
        {
            Assert.Same(polygon.Vertices[i], polygon.Edges[i].Start);
            Assert.Same(polygon.Vertices[(i + 1) % n], polygon.Edges[i].End);
        }
    }

    [Fact]
    public void InsertVertex_SplitsEdgeAndDropsItsConstraints()
    {
        var scene = SquareScene(out var square);
        Edge edge = square.Edges[3];
        scene.AddFixedLength(edge);
        scene.AddParallel(square.Edges[0], square.Edges[2]);

        Vertex inserted = scene.InsertVertex(edge);

        Assert.Equal(new Vec2(0, 50), inserted.Position);
        AssertClosedChain(square);
        Assert.Single(scene.Constraints); // only the parallel one survives
    }

    [Fact]
    public void InsertVertex_OnBezierKeepsCurveShape()
    {
        var scene = SquareScene(out var square);
        Edge edge = square.Edges[0];
        edge.MakeBezier(new Vec2(30, -60), new Vec2(70, -60));
        Vec2 midpoint = EdgeGeometry.PointAt(edge, 0.5);

        Vertex inserted = scene.InsertVertex(edge);

        Assert.True(Vec2.Distance(midpoint, inserted.Position) < 1e-9);
        Assert.Equal(EdgeKind.Bezier, square.Edges[0].Kind);
        Assert.Equal(EdgeKind.Bezier, square.Edges[1].Kind);
        AssertClosedChain(square);
    }

    [Fact]
    public void RemoveVertex_MergesEdgesAndDropsTheirConstraints()
    {
        var scene = SquareScene(out var square);
        scene.AddFixedLength(square.Edges[0]);
        scene.AddFixedLength(square.Edges[2]);

        scene.RemoveVertex(square.Vertices[0]);

        Assert.Equal(3, square.Vertices.Count);
        AssertClosedChain(square);
        Assert.Single(scene.Constraints);
    }

    [Fact]
    public void MakeBezier_AppliesDefaultContinuityButKeepsExplicitOne()
    {
        SquareScene(out var square);
        square.Vertices[1].SetContinuity(Continuity.C1);

        square.Edges[0].MakeBezier();

        Assert.Equal(VariantSettings.DefaultBezierContinuity, square.Vertices[0].Continuity);
        Assert.Equal(Continuity.C1, square.Vertices[1].Continuity);
    }

    [Fact]
    public void Arc_WithG0EndsIsSemicircle_WithG1IsTangentToNeighbour()
    {
        SquareScene(out var square);
        Edge edge = square.Edges[1];
        edge.MakeArc();

        var semicircle = ArcGeometry.Compute(edge)!.Value;
        Assert.Equal(50, semicircle.Radius, 9);

        square.Vertices[1].SetContinuity(Continuity.G1);
        var tangentArc = ArcGeometry.Compute(edge)!.Value;
        Vec2 incoming = EdgeGeometry.DerivativeAtEnd(square.Edges[0]);
        Assert.Equal(0, Vec2.Cross(incoming.Normalized(), tangentArc.DerivativeAtStart.Normalized()), 9);
        Assert.True(Vec2.Dot(incoming, tangentArc.DerivativeAtStart) > 0);
        Assert.True(Vec2.Distance(tangentArc.PointAt(1), square.Vertices[2].Position) < 1e-9);
    }

    [Fact]
    public void InitialScene_SatisfiesItsConstraints()
    {
        var scene = InitialScene.Create();

        Assert.True(scene.Polygons.Count >= 2);
        Assert.Contains(scene.Polygons.SelectMany(p => p.Edges), e => e.Kind == EdgeKind.Bezier);
        Assert.NotEmpty(scene.Constraints);
        Assert.All(scene.Constraints, c => Assert.True(c.IsSatisfied(), $"{c.GetType().Name} error {c.Error}"));
    }
}
