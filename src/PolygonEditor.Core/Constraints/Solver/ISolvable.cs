namespace PolygonEditor.Core.Constraints.Solver;

/// <summary>Anything the solver can enforce: user constraints and derived continuity relations.</summary>
public interface ISolvable
{
    /// <summary>Points the relation reads or moves (used to build the dependency graph).</summary>
    IEnumerable<PointRef> Points { get; }

    double Error { get; }
    double Tolerance { get; }

    /// <summary>Moves free points so that this relation alone becomes satisfied.</summary>
    void Project(SolverContext context);
}
