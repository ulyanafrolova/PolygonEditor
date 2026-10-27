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
    public void Wu_ThinLineGivesClassicPairOfIntensities()
    {
        // y = 0.3·x: in column x the center is 0.3x, so pixels floor and floor + 1 get 1 − frac and frac.
        var pixels = WuLineDrawer.RasterizeLine(new Vec2(0, 0), new Vec2(10, 3)).ToList();

        foreach (int x in Enumerable.Range(1, 9))
        {
            double center = 0.3 * x, frac = center - Math.Floor(center);
            var column = pixels.Where(p => p.X == x).ToDictionary(p => p.Y, p => p.Coverage);
            Assert.Equal(1 - frac, column[(int)Math.Floor(center)], 6);
            if (frac > 1e-9)
                Assert.Equal(frac, column[(int)Math.Floor(center) + 1], 6);
            Assert.Equal(1, column.Values.Sum(), 6);
        }
        // End columns are weighted by xgap: the segment covers half of pixel 0 and half of pixel 10.
        Assert.Equal(0.5, pixels.Where(p => p.X == 0).Sum(p => p.Coverage), 6);
        Assert.Equal(0.5, pixels.Where(p => p.X == 10).Sum(p => p.Coverage), 6);
    }

    [Theory]
    [InlineData(0, 0, 40, 10, 5)]
    [InlineData(0, 0, 10, 40, 4)]   // steep: rows instead of columns
    [InlineData(30, 30, 0, 0, 6)]   // 45°, reversed
    public void Wu_ThickLineCoversWidthTimesLength(double ax, double ay, double bx, double by, int thickness)
    {
        Vec2 a = new(ax, ay), b = new(bx, by);

        var pixels = WuLineDrawer.RasterizeLine(a, b, thickness).ToList();

        // Total coverage = area of the band ≈ length · width (ends are cut across the driving axis).
        Assert.Equal(Vec2.Distance(a, b) * thickness, pixels.Sum(p => p.Coverage), 0);
        Assert.All(pixels, p => Assert.InRange(p.Coverage, 0, 1));
        Assert.Equal(pixels.Count, pixels.Select(p => (p.X, p.Y)).Distinct().Count());
    }

    [Theory]
    [InlineData(40.0, 1)]
    [InlineData(40.3, 1)]
    [InlineData(25.0, 6)]
    public void Wu_ArcStaysOnRingWithinSweepAndCoversItsArea(double radius, int thickness)
    {
        var arc = new ArcShape(new Vec2(60.4, 50.7), radius, 0.3, Math.PI); // half circle, non-integer center

        var pixels = WuLineDrawer.RasterizeArc(arc, thickness, 0, 0, 200, 200).ToList();

        Assert.Equal(pixels.Count, pixels.Select(p => (p.X, p.Y)).Distinct().Count()); // nothing blended twice
        Assert.All(pixels, p =>
        {
            Vec2 offset = new Vec2(p.X, p.Y) - arc.Center;
            Assert.True(Math.Abs(offset.Length - radius) <= thickness / 2.0 + 1, $"({p.X}, {p.Y}) off the ring");
            double angle = Vec2.Angle(offset) - arc.StartAngle;
            angle = (angle % (2 * Math.PI) + 2 * Math.PI) % (2 * Math.PI);
            Assert.True(angle <= arc.Sweep + 1e-9, $"({p.X}, {p.Y}) outside the sweep");
        });
        // Thick: area of the half ring. Thin (classic Wu): intensities sum to 1 per column/row step,
        // and a half circle spans 4 · r/√2 steps.
        double area = thickness == 1 ? 2 * Math.Sqrt(2) * radius : Math.PI * radius * thickness;
        Assert.InRange(pixels.Sum(p => p.Coverage), area * 0.93, area * 1.07);
    }

    [Fact]
    public void Wu_HugeArcIsClippedToTheTarget()
    {
        var arc = ArcGeometry.TangentArc(new Vec2(0, 50), new Vec2(1, 1e-6), new Vec2(100, 50))!.Value; // radius ~ 10⁸

        var pixels = WuLineDrawer.RasterizeArc(arc, 3, 0, 0, 99, 99).ToList();

        Assert.All(pixels, p => Assert.True(p.X is >= 0 and <= 99 && p.Y is >= 0 and <= 99));
        Assert.InRange(pixels.Sum(p => p.Coverage), 3 * 100 * 0.9, 3 * 100 * 1.1);
    }

    [Fact]
    public void Bresenham_ThicknessReplicatesPixelsAcrossTheDrivingAxis()
    {
        var target = new RecordingTarget(100, 100);
        var drawer = new BresenhamLineDrawer(target);

        drawer.DrawLine(new Vec2(10, 50), new Vec2(60, 60), System.Drawing.Color.Black, thickness: 3);

        Assert.Equal(51 * 3, target.Pixels.Count);
        Assert.Contains((10, 49), target.Pixels);
        Assert.Contains((10, 51), target.Pixels);
    }

    [Fact]
    public void Bresenham_ThickDiagonalIsAsWideAsThickHorizontal()
    {
        var target = new RecordingTarget(100, 100);
        var drawer = new BresenhamLineDrawer(target);

        drawer.DrawLine(new Vec2(10, 10), new Vec2(50, 50), System.Drawing.Color.Black, thickness: 6);

        // Run across the driving axis: 6·√2 ≈ 8 pixels, i.e. 6 px perpendicular to the line.
        Assert.Equal(41 * 8, target.Pixels.Count);
    }

    // Right-angle turn: the horizontal segment is cut at x = 40, the vertical one at y = 50, so
    // without a round join the outer corner around (42, 48) would stay empty.
    private static readonly Vec2[] RightAngle = { new(10, 50), new(40, 50), new(40, 80) };

    [Fact]
    public void Wu_ThickPolylineHasNoNotchAtJoint()
    {
        var target = new RecordingTarget(100, 100);

        new WuLineDrawer(target).DrawPolyline(RightAngle, System.Drawing.Color.Black, thickness: 6);

        Assert.Contains((42, 48), target.Pixels);
        Assert.True(target.Coverage[(41, 49)] >= 0.99);
        Assert.All(target.Coverage.Values, c => Assert.InRange(c, 0, 1));
    }

    [Fact]
    public void Bresenham_ThickPolylineHasNoNotchAtJoint()
    {
        var target = new RecordingTarget(100, 100);

        new BresenhamLineDrawer(target).DrawPolyline(RightAngle, System.Drawing.Color.Black, thickness: 6);

        Assert.Contains((42, 48), target.Pixels);
    }

    [Fact]
    public void ThinPolylineGetsNoJoints()
    {
        var thin = new RecordingTarget(100, 100);
        new BresenhamLineDrawer(thin).DrawPolyline(RightAngle, System.Drawing.Color.Black);

        Assert.DoesNotContain((42, 48), thin.Pixels);
        Assert.Equal(31 + 31 - 1, thin.Pixels.Count);
    }

    private sealed class RecordingTarget : IRenderTarget
    {
        public RecordingTarget(int width, int height) => (Width, Height) = (width, height);

        public HashSet<(int, int)> Pixels { get; } = new();
        public Dictionary<(int, int), double> Coverage { get; } = new();
        public int Width { get; }
        public int Height { get; }

        public void Clear(System.Drawing.Color color) => Pixels.Clear();
        public void SetPixel(int x, int y, System.Drawing.Color color) => Pixels.Add((x, y));

        public void BlendPixel(int x, int y, System.Drawing.Color color, double coverage)
        {
            Pixels.Add((x, y));
            Coverage[(x, y)] = coverage;
        }
        public void DrawLine(Vec2 a, Vec2 b, System.Drawing.Color color, bool dashed, int thickness) { }
        public void FillCircle(Vec2 center, double radius, System.Drawing.Color color) { }
        public void DrawCircle(Vec2 center, double radius, System.Drawing.Color color) { }
        public void DrawLabel(string text, Vec2 center, System.Drawing.Color foreground, System.Drawing.Color background) { }
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
