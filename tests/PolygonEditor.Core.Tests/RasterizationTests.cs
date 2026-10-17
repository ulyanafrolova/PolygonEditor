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

    [Fact]
    public void Arc_PointsStayOnCircleAndEndAtEndpoint()
    {
        var arc = ArcGeometry.TangentArc(new Vec2(0, 0), new Vec2(1, 1), new Vec2(100, 0))!.Value;

        var points = CurveRasterizer.ArcPoints(arc);

        Assert.All(points, p => Assert.True(Math.Abs(Vec2.Distance(p, arc.Center) - arc.Radius) < 1e-6));
        Assert.True(Vec2.Distance(points[^1], new Vec2(100, 0)) < 1e-6);
    }
}
