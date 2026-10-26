using PolygonEditor.Core.Constraints.Solver;
using PolygonEditor.Core.Editing;
using PolygonEditor.Core.Geometry;
using PolygonEditor.Core.Model;

namespace PolygonEditor.Core.Scenes;

/// <summary>
/// Demo scene loaded at startup. To add a polygon: scene.AddPolygon((x, y), ...), then
/// optionally change edge kinds (edge.MakeBezier / MakeArc), continuity and constraints.
/// Edge i connects vertex i with vertex i + 1 (the last one closes the polygon).
/// Control points only need to be roughly right: at the end the solver adjusts them to the
/// required continuity (vertices stay where they were written if they already satisfy the
/// constraints; otherwise the solver moves them as little as needed, see Settle).
/// </summary>
public static class InitialScene
{
    public static Scene Create()
    {
        var scene = new Scene();

        // W1: pentagon with a Bezier edge (default G1 at its start, explicit C1 at its end)
        // and a fixed length on an edge that is not next to the curve.
        var w1 = scene.AddPolygon((120, 140), (360, 120), (420, 320), (250, 420), (90, 320));
        w1.Vertices[2].SetContinuity(Continuity.C1);
        w1.Edges[1].MakeBezier(P(420, 115), P(475, 285));
        scene.AddFixedLength(w1.Edges[3]);

        // W2: quadrilateral with a semicircular arc (G0 at both ends);
        // its edge 2 is parallel to edge 0 of W1 (constraint between two polygons).
        var w2 = scene.AddPolygon((560, 140), (800, 120), (840, 330), (600, 350));
        w2.Edges[1].MakeArc();
        scene.AddParallel(w1.Edges[0], w2.Edges[2]);
        scene.AddFixedLength(w2.Edges[0]);

        // W3: self-intersecting "bow tie" with an arc tangent (G1) to the previous edge.
        var w3 = scene.AddPolygon((200, 520), (420, 640), (420, 520), (200, 640));
        w3.Vertices[1].SetContinuity(Continuity.G1);
        w3.Edges[1].MakeArc();

        Settle(scene);
        return scene;
    }

    /// <summary>
    /// Lets the solver fit the scene to its relations. First every vertex is pinned, so only control
    /// points move and the vertices stay exactly where they were written. If the written vertices do
    /// not satisfy the lengths / parallels (approximate coordinates), only the first vertex of each
    /// polygon is pinned and the solver adjusts the rest. Throws only for contradictory constraints.
    /// </summary>
    internal static void Settle(Scene scene)
    {
        var snapshot = GeometrySnapshot.Capture(scene);
        var solver = new ConstraintSolver();

        var vertices = scene.Polygons.SelectMany(p => p.Vertices).Select(PointRef.Of).ToList();
        if (solver.Solve(scene, vertices))
            return;

        snapshot.Restore();
        var anchors = scene.Polygons.Select(p => PointRef.Of(p.Vertices[0])).ToList();
        if (!solver.Solve(scene, anchors))
            throw new InvalidOperationException("The initial scene is inconsistent: check its constraints.");
    }

    private static Vec2 P(double x, double y) => new(x, y);
}
