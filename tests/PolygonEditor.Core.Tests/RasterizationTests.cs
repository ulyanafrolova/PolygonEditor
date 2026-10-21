using PolygonEditor.Core.Geometry;
using PolygonEditor.Core.Rendering;

namespace PolygonEditor.Core.Tests;

public class RasterizationTests
{
    [Theory]
    [InlineData(0, 0, 10, 3)]
    [InlineData(0, 0, 3, 10)]
    [InlineData(10, 3, 0, 0)]
    [InlineData(0, 10, 3, 0)]
    [InlineData(5, 5, -7, 9)]
    [InlineData(5, 5, 5, -4)]
    [InlineData(2, 2, 2, 2)]
    public void Bresenham_CoversEndpointsWithConnectedPixels(int x1, int y1, int x2, int y2)
    {
        var pixels = BresenhamLineDrawer.Rasterize(x1, y1, x2, y2).ToList();

        Assert.Equal((x1, y1), pixels[0]);
        Assert.Equal((x2, y2), pixels[^1]);
        Assert.Equal(Math.Max(Math.Abs(x2 - x1), Math.Abs(y2 - y1)) + 1, pixels.Count);
        for (int i = 1; i < pixels.Count; i++)
        {
            Assert.True(Math.Abs(pixels[i].X - pixels[i - 1].X) <= 1);
            Assert.True(Math.Abs(pixels[i].Y - pixels[i - 1].Y) <= 1);
        }
    }

    [Fact]
    public void Bezier_ForwardDifferencesMatchBernsteinFormula()
    {
        Vec2 v0 = new(0, 0), v1 = new(30, 100), v2 = new(170, -40), v3 = new(200, 60);
        const int segments = 50;

        var points = CurveRasterizer.BezierPoints(v0, v1, v2, v3, segments);

        for (int i = 0; i <= segments; i++)
        {
            double t = (double)i / segments, u = 1 - t;
            Vec2 expected = v0 * (u * u * u) + v1 * (3 * t * u * u) + v2 * (3 * t * t * u) + v3 * (t * t * t);
            Assert.True(Vec2.Distance(expected, points[i]) < 1e-6, $"t={t}: {expected} vs {points[i]}");
        }
    }

    [Theory]
    [InlineData(-50, 50, 150, 50, 0, 50, 100, 50)]         // horizontal, both ends outside
    [InlineData(10, 10, 90, 90, 10, 10, 90, 90)]           // fully inside: unchanged
    [InlineData(-100, -100, 200, 200, 0, 0, 100, 100)]     // diagonal through the whole box
    [InlineData(50, 50, 50, 1e9, 50, 50, 50, 100)]         // far-away end (huge arc)
    public void LineClipper_ClipsToRectangle(double ax, double ay, double bx, double by, double cax, double cay, double cbx, double cby)
    {
        Vec2 a = new(ax, ay), b = new(bx, by);

        Assert.True(LineClipper.Clip(ref a, ref b, 0, 0, 100, 100));

        Assert.True(Vec2.Distance(a, new Vec2(cax, cay)) < 1e-6, $"a = {a}");
        Assert.True(Vec2.Distance(b, new Vec2(cbx, cby)) < 1e-6, $"b = {b}");
    }

    [Fact]
    public void LineClipper_RejectsSegmentsOutside()
    {
        Vec2 a = new(-10, -10), b = new(-1, 200);

        Assert.False(LineClipper.Clip(ref a, ref b, 0, 0, 100, 100));
    }

    [Fact]
    public void SceneBounds_IncludeArcBulgeAndControlPoints()
    {
        var scene = new PolygonEditor.Core.Model.Scene();
        var p = scene.AddPolygon((0, 0), (100, 0), (100, 100), (0, 100));
        p.Edges[1].MakeArc();                                            // semicircle bulging to the right of x = 100
        p.Edges[3].MakeBezier(new Vec2(-20, 80), new Vec2(30, 140));     // control point below y = 100

        Vec2 corner = SceneBounds.MaxCorner(scene);

        Assert.True(corner.X >= 149, $"arc bulge missing: {corner}");
        Assert.True(corner.Y >= 140, $"control point missing: {corner}");
    }

    [Fact]
    public void Arc_PointsStayOnCircleAndEndAtEndpoint()
    {
        var arc = ArcGeometry.TangentArc(new Vec2(0, 0), new Vec2(1, 1), new Vec2(100, 0))!.Value;

        var points = CurveRasterizer.ArcPoints(arc);

        Assert.All(points, p => Assert.True(Math.Abs(Vec2.Distance(p, arc.Center) - arc.Radius) < 1e-6));
        Assert.True(Vec2.Distance(points[^1], new Vec2(100, 0)) < 1e-6);
    }
}
