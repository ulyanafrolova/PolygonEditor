using PolygonEditor.Core.Geometry;

namespace PolygonEditor.Core.Model;

public sealed class Vertex
{
    internal Vertex(Polygon polygon, Vec2 position)
    {
        Polygon = polygon;
        Position = position;
    }

    public Polygon Polygon { get; }

    /// <summary>Changed only through SceneEditor / the solver (setter is internal).</summary>
    public Vec2 Position { get; internal set; }

    /// <summary>Required continuity at this vertex; meaningful only when an adjacent edge is a curve.</summary>
    public Continuity Continuity { get; private set; } = Continuity.G0;

    /// <summary>True once the user chose the continuity; defaults no longer override it.</summary>
    public bool IsContinuityExplicit { get; private set; }

    /// <summary>Continuity chosen by the user (validated by SceneEditor via ContinuityRules).</summary>
    internal void SetContinuity(Continuity continuity)
    {
        Continuity = continuity;
        IsContinuityExplicit = true;
    }

    /// <summary>Change forced by the rules; keeps the "explicit" flag as it was.</summary>
    internal void ForceContinuity(Continuity continuity) => Continuity = continuity;

    /// <summary>Puts back a previous state (undo of a rejected change).</summary>
    internal void RestoreContinuity(Continuity continuity, bool isExplicit)
    {
        Continuity = continuity;
        IsContinuityExplicit = isExplicit;
    }

    /// <summary>Continuity applied automatically (e.g. after switching an edge to Bezier).</summary>
    internal void ApplyDefaultContinuity(Continuity continuity)
    {
        if (!IsContinuityExplicit)
            Continuity = continuity;
    }

    /// <summary>Human-readable name, e.g. "W1[2]".</summary>
    public string Name => $"{Polygon.Name}[{Polygon.IndexOf(this)}]";

    public Edge EdgeBefore => Polygon.EdgeBefore(this);
    public Edge EdgeAfter => Polygon.EdgeAfter(this);

    /// <summary>True when continuity matters here, i.e. at least one adjacent edge is curved.</summary>
    public bool TouchesCurve => EdgeBefore.Kind != EdgeKind.Line || EdgeAfter.Kind != EdgeKind.Line;
}
