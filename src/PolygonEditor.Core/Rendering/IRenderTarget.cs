using System.Drawing;
using PolygonEditor.Core.Geometry;

namespace PolygonEditor.Core.Rendering;

/// <summary>
/// Drawing surface implemented by the UI layer. SetPixel / BlendPixel are all the custom algorithms
/// need; DrawLine is the library (GDI+) line used for the "library" option.
/// </summary>
public interface IRenderTarget
{
    int Width { get; }
    int Height { get; }

    void Clear(Color color);
    void SetPixel(int x, int y, Color color);

    /// <summary>Mixes the color into the pixel: result = (1 − coverage)·old + coverage·color (antialiasing).</summary>
    void BlendPixel(int x, int y, Color color, double coverage);

    void DrawLine(Vec2 a, Vec2 b, Color color, bool dashed, int thickness);
    void FillCircle(Vec2 center, double radius, Color color);
    void DrawCircle(Vec2 center, double radius, Color color);

    /// <summary>Small text with a background box, centered at the given point.</summary>
    void DrawLabel(string text, Vec2 center, Color foreground, Color background);
}
