namespace PolygonEditor.Core.Model;

/// <summary>Shape of a polygon edge.</summary>
public enum EdgeKind
{
    Line,
    Bezier,
    Arc,
}

/// <summary>Continuity class required at a vertex adjacent to a curved edge.</summary>
public enum Continuity
{
    /// <summary>Position only.</summary>
    G0,
    /// <summary>Position + direction of the unit tangent vector.</summary>
    G1,
    /// <summary>Position + full tangent vector (direction and length). Not allowed for arcs.</summary>
    C1,
}
