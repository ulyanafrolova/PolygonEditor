using PolygonEditor.Core.Geometry;

namespace PolygonEditor.Core.Model;

/// <summary>
/// Single source of truth about continuity: which classes are allowed where, which defaults
/// are applied automatically, and whether the geometry actually satisfies them.
/// </summary>
public static class ContinuityRules
{
    /// <summary>
    /// - Arcs support only G0/G1 (no C1).
    /// - At most one end of an arc may be G1.
    /// - No G1 between two arcs: an arc with G1 takes its tangent from the neighbour, so two
    ///   arcs would each wait for the other (the shared tangent direction is not determined).
    /// </summary>
    public static bool IsAllowed(Vertex vertex, Continuity continuity)
    {
        if (continuity == Continuity.G0)
            return true;
        if (vertex.EdgeBefore.Kind == EdgeKind.Arc && vertex.EdgeAfter.Kind == EdgeKind.Arc)
            return false;

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

    /// <summary>Vertices whose continuity breaks the rules (should never happen after editing).</summary>
    public static IEnumerable<Vertex> Violations(Scene scene) =>
        scene.Polygons.SelectMany(p => p.Vertices).Where(v => !IsAllowed(v, v.Continuity));

    /// <summary>
    /// Defaults after an edge became a Bezier segment: each end that the user has not set
    /// explicitly gets the variant default (G1 or C1), or G1 if that is not allowed there,
    /// or keeps its value if neither is allowed (e.g. the far end of an arc that already has G1).
    /// </summary>
    internal static void ApplyBezierDefaults(Edge bezier)
    {
        foreach (Vertex end in new[] { bezier.Start, bezier.End })
        {
            if (end.IsContinuityExplicit)
                continue;
            foreach (Continuity candidate in new[] { VariantSettings.DefaultBezierContinuity, Continuity.G1 })
                if (IsAllowed(end, candidate))
                {
                    end.ApplyDefaultContinuity(candidate);
                    break;
                }
        }
    }

    /// <summary>
    /// Brings the ends of a new arc in line with the rules: C1 becomes G1, G1 next to another
    /// arc becomes G0, and if both ends still ask for G1 the end vertex falls back to G0.
    /// The "explicit" flag of the vertex is kept.
    /// </summary>
    internal static void NormalizeArcEnds(Edge arc)
    {
        foreach (Vertex end in new[] { arc.Start, arc.End })
        {
            if (end.Continuity == Continuity.C1)
                end.ForceContinuity(Continuity.G1);
            if (end.EdgeBefore.Kind == EdgeKind.Arc && end.EdgeAfter.Kind == EdgeKind.Arc)
                end.ForceContinuity(Continuity.G0);
        }

        if (arc.Start.Continuity != Continuity.G0 && arc.End.Continuity != Continuity.G0)
            arc.End.ForceContinuity(Continuity.G0);
    }

    /// <summary>
    /// How far the actual curves are from the required continuity at the vertex, measured on
    /// the real tangent vectors of both edges (arcs included):
    /// G1 → |unit tangent in − unit tangent out|, C1 → |tangent in − tangent out|, G0 / no curve → 0.
    /// </summary>
    public static double MeasureError(Vertex vertex)
    {
        if (vertex.Continuity == Continuity.G0 || !vertex.TouchesCurve)
            return 0;

        Vec2 incoming = EdgeGeometry.DerivativeAtEnd(vertex.EdgeBefore);
        Vec2 outgoing = EdgeGeometry.DerivativeAtStart(vertex.EdgeAfter);
        if (incoming.LengthSquared < 1e-12 || outgoing.LengthSquared < 1e-12)
            return 0; // tangent undefined (degenerate handle)
        return vertex.Continuity == Continuity.C1
            ? (incoming - outgoing).Length
            : (incoming.Normalized() - outgoing.Normalized()).Length;
    }

    /// <summary>Tolerance matching the solver's: 0.05 for C1 (vector length), 1e-4 for G1 (unit vectors).</summary>
    public static double Tolerance(Vertex vertex) => vertex.Continuity == Continuity.C1 ? 0.05 : 1e-4;
}
