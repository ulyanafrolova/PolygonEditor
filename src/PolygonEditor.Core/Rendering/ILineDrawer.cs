using System.Drawing;
using PolygonEditor.Core.Geometry;

namespace PolygonEditor.Core.Rendering;

public enum LineAlgorithm
{
    Library,
    Bresenham,
    Wu,
}

/// <summary>Line drawing strategy; every edge, curve and helper line goes through it.</summary>
public interface ILineDrawer
{
    void DrawLine(Vec2 a, Vec2 b, Color color, bool dashed = false, int thickness = 1);

    /// <summary>Consecutive points joined by segments; by default segment by segment.</summary>
    void DrawPolyline(IReadOnlyList<Vec2> points, Color color, bool dashed = false, int thickness = 1)
    {
        for (int i = 0; i + 1 < points.Count; i++)
            DrawLine(points[i], points[i + 1], color, dashed, thickness);
    }

    /// <summary>Circular arc; by default as a polyline (overridden by algorithms that draw circles directly).</summary>
    void DrawArc(ArcShape arc, Color color, int thickness = 1) =>
        DrawPolyline(CurveRasterizer.ArcPoints(arc), color, thickness: thickness);
}

/// <summary>Delegates to the graphics library (GDI+ Graphics.DrawLine; thickness = pen width).</summary>
public sealed class LibraryLineDrawer : ILineDrawer
{
    private readonly IRenderTarget _target;

    public LibraryLineDrawer(IRenderTarget target) => _target = target;

    public void DrawLine(Vec2 a, Vec2 b, Color color, bool dashed = false, int thickness = 1)
    {
        if (LineClipper.ClipToTarget(ref a, ref b, _target, thickness))
            _target.DrawLine(a, b, color, dashed, thickness);
    }
}
