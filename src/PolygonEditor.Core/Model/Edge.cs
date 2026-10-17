using PolygonEditor.Core.Geometry;

namespace PolygonEditor.Core.Model;

/// <summary>
/// Edge between two consecutive vertices of a polygon. Its endpoints never change:
/// inserting/removing a vertex replaces edges with new instances, which makes it
/// easy to drop constraints attached to edges that no longer exist.
/// </summary>
public sealed class Edge
{
    internal Edge(Polygon polygon, Vertex start, Vertex end)
    {
        Polygon = polygon;
        Start = start;
        End = end;
    }

    public Polygon Polygon { get; }
    public Vertex Start { get; }
    public Vertex End { get; }

    public EdgeKind Kind { get; private set; } = EdgeKind.Line;

    /// <summary>Bezier control points V1 and V2 (V0 = Start, V3 = End). Used only when Kind == Bezier.</summary>
    public Vec2 Control1 { get; set; }
    public Vec2 Control2 { get; set; }

    public Edge Previous => Polygon.EdgeBefore(Start);
    public Edge Next => Polygon.EdgeAfter(End);

    public Vec2 Chord => End.Position - Start.Position;
    public double Length => Chord.Length;

    public void MakeLine() => Kind = EdgeKind.Line;

    /// <summary>Switches to Bezier with control points at 1/3 and 2/3 of the chord, pushed slightly sideways.</summary>
    public void MakeBezier()
    {
        Vec2 a = Start.Position, chord = Chord;
        Vec2 offset = chord.Perp() * 0.25;
        MakeBezier(a + chord / 3 + offset, a + chord * (2.0 / 3) + offset);
    }

    public void MakeBezier(Vec2 control1, Vec2 control2)
    {
        Kind = EdgeKind.Bezier;
        Control1 = control1;
        Control2 = control2;
        Start.ApplyDefaultContinuity(VariantSettings.DefaultBezierContinuity);
        End.ApplyDefaultContinuity(VariantSettings.DefaultBezierContinuity);
    }

    /// <summary>Switches to a circular arc; its shape is derived from endpoints and continuity (see ArcGeometry).</summary>
    public void MakeArc() => Kind = EdgeKind.Arc;

    internal void Translate(Vec2 delta)
    {
        Control1 += delta;
        Control2 += delta;
    }

    /// <summary>Human-readable name, e.g. "W1[2]".</summary>
    public string Name => $"{Polygon.Name}[{Polygon.IndexOf(this)}]";

    public override string ToString() => $"{Name} {Kind}";
}
