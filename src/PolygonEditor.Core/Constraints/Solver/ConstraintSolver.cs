using PolygonEditor.Core.Model;

namespace PolygonEditor.Core.Constraints.Solver;

/// <summary>
/// Iterative relaxation (Gauss–Seidel projections): every relation in turn moves the free points
/// to satisfy itself, repeated until all relations hold or the iteration limit is reached.
///
/// Who moves: points are weighted by their distance from the pinned points in the dependency
/// graph (polygon edges and vertex–handle links cost 1, links through a user constraint cost 2);
/// weight = distance².
/// Corrections therefore flow away from the element under the cursor, and nearby points stay
/// almost where the user put them.
/// </summary>
public sealed class ConstraintSolver
{
    public int MaxIterations { get; init; } = 300;

    public bool Solve(Scene scene, IReadOnlyCollection<PointRef> pinned)
    {
        List<ISolvable> relations = Relations(scene);
        if (relations.Count == 0)
            return true;

        var context = new SolverContext(ComputeWeights(scene, relations, pinned));
        for (int iteration = 0; iteration < MaxIterations; iteration++)
        {
            if (relations.All(IsSatisfied))
                return true;
            foreach (ISolvable relation in relations)
                relation.Project(context);
        }
        return relations.All(IsSatisfied);
    }

    /// <summary>
    /// User constraints, continuity relations derived from vertex settings, and the check that no
    /// arc has degenerated into a huge loop.
    /// </summary>
    public static List<ISolvable> Relations(Scene scene) =>
        scene.Constraints.Cast<ISolvable>()
            .Concat(ContinuityConstraint.For(scene))
            .Concat(ArcShapeConstraint.For(scene))
            .ToList();

    /// <summary>
    /// Full consistency check: every solver relation holds, every vertex respects the continuity
    /// rules, and the continuity measured on the real curves (arcs included) holds at every vertex.
    /// The last two catch errors the solver's own relations cannot see (arcs are not relations).
    /// </summary>
    public static bool AllSatisfied(Scene scene) =>
        Relations(scene).All(IsSatisfied)
        && !ContinuityRules.Violations(scene).Any()
        && scene.Polygons.SelectMany(p => p.Vertices).All(v => ContinuityRules.MeasureError(v) <= ContinuityRules.Tolerance(v));

    private static bool IsSatisfied(ISolvable relation) => relation.Error <= relation.Tolerance;

    // A step through a user constraint (often to another polygon) counts as farther than a step
    // along the polygon itself, so the dragged polygon keeps its shape and the related one adapts.
    private const int StructuralLinkCost = 1;
    private const int ConstraintLinkCost = 2;

    private static Dictionary<PointRef, double> ComputeWeights(Scene scene, List<ISolvable> relations, IReadOnlyCollection<PointRef> pinned)
    {
        var graph = new Dictionary<PointRef, List<(PointRef Point, int Cost)>>();
        void Link(PointRef a, PointRef b, int cost)
        {
            if (a == b) return;
            (graph.TryGetValue(a, out var la) ? la : graph[a] = new()).Add((b, cost));
            (graph.TryGetValue(b, out var lb) ? lb : graph[b] = new()).Add((a, cost));
        }

        foreach (Polygon polygon in scene.Polygons)
            foreach (Edge edge in polygon.Edges)
            {
                Link(PointRef.Of(edge.Start), PointRef.Of(edge.End), StructuralLinkCost);
                if (edge.Kind == EdgeKind.Bezier)
                {
                    Link(PointRef.Of(edge.Start), PointRef.Control(edge, 1), StructuralLinkCost);
                    Link(PointRef.Of(edge.End), PointRef.Control(edge, 2), StructuralLinkCost);
                }
            }
        foreach (ISolvable relation in relations)
        {
            int cost = relation is Constraint ? ConstraintLinkCost : StructuralLinkCost;
            var points = relation.Points.ToList();
            for (int i = 0; i < points.Count; i++)
                for (int j = i + 1; j < points.Count; j++)
                    Link(points[i], points[j], cost);
        }

        // Dijkstra from the pinned points.
        var depth = new Dictionary<PointRef, int>();
        var queue = new PriorityQueue<PointRef, int>();
        foreach (PointRef p in pinned)
        {
            depth[p] = 0;
            queue.Enqueue(p, 0);
        }
        while (queue.TryDequeue(out PointRef p, out int d))
        {
            if (d > depth[p] || !graph.TryGetValue(p, out var neighbours)) continue;
            foreach (var (n, cost) in neighbours)
                if (!depth.TryGetValue(n, out int known) || d + cost < known)
                {
                    depth[n] = d + cost;
                    queue.Enqueue(n, d + cost);
                }
        }

        int unreachable = (depth.Count > 0 ? depth.Values.Max() : 0) + 1;
        var weights = new Dictionary<PointRef, double>();
        foreach (PointRef p in graph.Keys)
        {
            int d = depth.TryGetValue(p, out int known) ? known : unreachable;
            weights[p] = (double)d * d;
        }
        foreach (PointRef p in pinned)
            weights[p] = 0;
        return weights;
    }
}
