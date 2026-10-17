namespace PolygonEditor.Core.Model;

/// <summary>Which continuity classes may be set at a vertex.</summary>
public static class ContinuityRules
{
    /// <summary>
    /// Arcs support only G0/G1 (no C1), and at most one end of an arc may be G1.
    /// </summary>
    public static bool IsAllowed(Vertex vertex, Continuity continuity)
    {
        if (continuity == Continuity.G0)
            return true;

        foreach (Edge edge in new[] { vertex.EdgeBefore, vertex.EdgeAfter })
        {
            if (edge.Kind != EdgeKind.Arc)
                continue;
            if (continuity == Continuity.C1)
                return false;
            Vertex otherEnd = edge.Start == vertex ? edge.End : edge.Start;
            if (otherEnd.Continuity != Continuity.G0)
                return false;
        }
        return true;
    }

    /// <summary>
    /// Brings the ends of an arc in line with the rules after an edge becomes an arc:
    /// C1 becomes G1, and if both ends ask for G1 the end vertex falls back to G0.
    /// </summary>
    internal static void NormalizeArcEnds(Edge arc)
    {
        foreach (Vertex end in new[] { arc.Start, arc.End })
            if (end.Continuity == Continuity.C1)
                end.SetContinuity(Continuity.G1);

        if (arc.Start.Continuity != Continuity.G0 && arc.End.Continuity != Continuity.G0)
            arc.End.SetContinuity(Continuity.G0);
    }
}
