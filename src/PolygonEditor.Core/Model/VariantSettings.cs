namespace PolygonEditor.Core.Model;

/// <summary>
/// Requirements that depend on the author's surname (task variant).
/// Surname starting with A–K: parallel-edges constraint and default G1 continuity.
/// </summary>
public static class VariantSettings
{
    /// <summary>Continuity set on both ends when an edge becomes a Bezier segment (unless set explicitly by the user).</summary>
    public const Continuity DefaultBezierContinuity = Continuity.G1;
}
