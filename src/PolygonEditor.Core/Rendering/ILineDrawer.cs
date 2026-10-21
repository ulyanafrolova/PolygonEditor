using System.Drawing;
using PolygonEditor.Core.Geometry;

namespace PolygonEditor.Core.Rendering;

public enum LineAlgorithm
{
    Library,
    Bresenham,
}

/// <summary>Line drawing strategy; every edge, curve and helper line goes through it.</summary>
public interface ILineDrawer
{
    void DrawLine(Vec2 a, Vec2 b, Color color, bool dashed = false);
}

public static class LineDrawerExtensions
{
    public static void DrawPolyline(this ILineDrawer drawer, IReadOnlyList<Vec2> points, Color color, bool dashed = false)
    {
        for (int i = 0; i + 1 < points.Count; i++)
            drawer.DrawLine(points[i], points[i + 1], color, dashed);
    }
}

/// <summary>Delegates to the graphics library (GDI+ Graphics.DrawLine).</summary>
public sealed class LibraryLineDrawer : ILineDrawer
{
    private readonly IRenderTarget _target;

    public LibraryLineDrawer(IRenderTarget target) => _target = target;

    public void DrawLine(Vec2 a, Vec2 b, Color color, bool dashed = false)
    {
        if (LineClipper.ClipToTarget(ref a, ref b, _target))
            _target.DrawLine(a, b, color, dashed);
    }
}
