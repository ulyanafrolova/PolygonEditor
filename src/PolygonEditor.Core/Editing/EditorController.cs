using PolygonEditor.Core.Geometry;

namespace PolygonEditor.Core.Editing;

/// <summary>
/// Mouse interaction state machine, independent of the UI toolkit: Idle (hover tracking) ⇄ Dragging.
/// Dragging a vertex or control point keeps it exactly under the cursor (grab offset preserved);
/// dragging an edge or the polygon interior moves the whole polygon.
/// Each method returns true when the scene must be redrawn.
/// </summary>
public sealed class EditorController
{
    private readonly HitTester _hitTester;
    private GeometrySnapshot? _snapshot;
    private Vec2 _lastMouse;
    private Vec2 _grabOffset;

    public EditorController(SceneEditor editor, HitTester? hitTester = null)
    {
        Editor = editor;
        _hitTester = hitTester ?? new HitTester();
    }

    public SceneEditor Editor { get; }
    public HitTarget? Hovered { get; private set; }
    public HitTarget? Dragged { get; private set; }
    public bool IsDragging => Dragged is not null;

    /// <summary>Element to highlight: the dragged one, otherwise the hovered one.</summary>
    public HitTarget? Highlighted => Dragged ?? Hovered;

    public HitTarget? HitTest(Vec2 p) => _hitTester.HitTest(Editor.Scene, p);

    public bool MouseDown(Vec2 p)
    {
        HitTarget? hit = HitTest(p);
        Hovered = hit;
        if (hit is null)
            return false;

        _snapshot = GeometrySnapshot.Capture(Editor.Scene);
        Dragged = hit;
        _lastMouse = p;
        _grabOffset = hit switch
        {
            VertexHit v => v.Vertex.Position - p,
            ControlPointHit c => c.Position - p,
            _ => Vec2.Zero,
        };
        return true;
    }

    public bool MouseMove(Vec2 p)
    {
        if (Dragged is null)
        {
            HitTarget? hit = HitTest(p);
            bool changed = hit != Hovered;
            Hovered = hit;
            return changed;
        }

        switch (Dragged)
        {
            case VertexHit v:
                Editor.MoveVertex(v.Vertex, p + _grabOffset);
                break;
            case ControlPointHit c:
                Editor.MoveControlPoint(c.Edge, c.Index, p + _grabOffset);
                break;
            default:
                Editor.MovePolygon(Dragged.Polygon, p - _lastMouse);
                break;
        }
        _lastMouse = p;
        return true;
    }

    public bool MouseUp()
    {
        if (Dragged is null)
            return false;
        Dragged = null;
        _snapshot = null;
        return true;
    }

    /// <summary>Esc during a drag: restores the geometry from before the drag started.</summary>
    public bool CancelDrag()
    {
        if (Dragged is null)
            return false;
        _snapshot?.Restore();
        Dragged = null;
        _snapshot = null;
        return true;
    }

    /// <summary>Forget the hovered element (call after the scene topology changed, e.g. a vertex was removed).</summary>
    public void ResetHover() => Hovered = null;
}
