using PolygonEditor.Core.Constraints;
using PolygonEditor.Core.Geometry;
using PolygonEditor.Core.Model;

namespace PolygonEditor.Core.Editing;

/// <summary>
/// Single entry point for every change the user makes to the scene (mouse, menu, keyboard).
/// Move operations are where constraint and continuity enforcement plugs in.
/// </summary>
public sealed class SceneEditor
{
    public SceneEditor(Scene scene) => Scene = scene;

    public Scene Scene { get; }

    public void MoveVertex(Vertex vertex, Vec2 target) => vertex.Position = target;

    public void MoveControlPoint(Edge edge, int index, Vec2 target)
    {
        if (index == 1)
            edge.Control1 = target;
        else
            edge.Control2 = target;
    }

    public void MovePolygon(Polygon polygon, Vec2 delta) => polygon.Translate(delta);

    public void SetEdgeKind(Edge edge, EdgeKind kind)
    {
        if (edge.Kind == kind)
            return;
        switch (kind)
        {
            case EdgeKind.Bezier: edge.MakeBezier(); break;
            case EdgeKind.Arc: edge.MakeArc(); break;
            default: edge.MakeLine(); break;
        }
    }

    public bool CanSetContinuity(Vertex vertex, Continuity continuity) => ContinuityRules.IsAllowed(vertex, continuity);

    public void SetContinuity(Vertex vertex, Continuity continuity)
    {
        if (!CanSetContinuity(vertex, continuity))
            throw new InvalidOperationException($"{continuity} is not allowed at {vertex.Name}.");
        vertex.SetContinuity(continuity);
    }

    public Vertex InsertVertex(Edge edge) => Scene.InsertVertex(edge);

    public bool CanRemoveVertex(Vertex vertex) => vertex.Polygon.Vertices.Count > 3;

    public void RemoveVertex(Vertex vertex) => Scene.RemoveVertex(vertex);

    public void RemoveConstraint(Constraint constraint) => Scene.RemoveConstraint(constraint);
}
