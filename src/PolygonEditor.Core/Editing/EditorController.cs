using PolygonEditor.Core.Geometry;
using PolygonEditor.Core.Model;

namespace PolygonEditor.Core.Editing;

/// <summary>
/// Mouse interaction state machine, independent of the UI toolkit: Idle (hover tracking) ⇄ Dragging,
/// plus two modes entered from the UI: picking a second edge and creating a polygon (clicking vertices).
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
    private Action<Edge>? _onEdgePicked;
    private List<Vec2>? _draft; // vertices of the polygon being created

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

    /// <summary>True while waiting for the user to click a second edge (e.g. for a parallel constraint).</summary>
    public bool IsPickingEdge => _onEdgePicked is not null;

    /// <summary>Next left click on an edge (or its control point) calls the callback instead of starting a drag.</summary>
    public void BeginPickEdge(Action<Edge> onPicked) => _onEdgePicked = onPicked;

    public bool CancelPick()
    {
        bool wasPicking = IsPickingEdge;
        _onEdgePicked = null;
        return wasPicking;
    }

    // ---------- creating a polygon ----------

    /// <summary>True while the user clicks the vertices of a new polygon.</summary>
    public bool IsCreatingPolygon => _draft is not null;

    /// <summary>Vertices clicked so far (empty when not creating).</summary>
    public IReadOnlyList<Vec2> DraftPoints => _draft ?? (IReadOnlyList<Vec2>)Array.Empty<Vec2>();

    /// <summary>Current cursor position while creating (the rubber-band edge ends here).</summary>
    public Vec2 DraftCursor { get; private set; }

    /// <summary>Distance below which a click closes the polygon (first vertex) or is ignored (last vertex).</summary>
    public double CloseTolerance { get; init; } = 8;

    /// <summary>Next left clicks add vertices of a new polygon instead of editing.</summary>
    public void BeginCreatePolygon()
    {
        CancelPick();
        _draft = new List<Vec2>();
    }

    /// <summary>Closes the polygon (Enter or a click on its first vertex). Needs at least 3 vertices.</summary>
    public bool FinishCreatePolygon()
    {
        if (_draft is not { Count: >= 3 } points)
            return false;
        _draft = null;
        Editor.AddPolygon(points);
        return true;
    }

    public bool CancelCreatePolygon()
    {
        bool wasCreating = IsCreatingPolygon;
        _draft = null;
        return wasCreating;
    }

    private bool DraftClick(List<Vec2> points, Vec2 p)
    {
        if (points.Count >= 3 && Vec2.Distance(p, points[0]) <= CloseTolerance)
            return FinishCreatePolygon();
        if (points.Count > 0 && Vec2.Distance(p, points[^1]) <= CloseTolerance)
            return false; // double click on the same spot would give a zero-length edge
        points.Add(p);
        DraftCursor = p;
        return true;
    }

    // ---------- mouse ----------

    public bool MouseDown(Vec2 p)
    {
        if (_draft is { } draft)
            return DraftClick(draft, p);

        HitTarget? hit = HitTest(p);
        Hovered = hit;

        if (_onEdgePicked is { } onPicked)
        {
            Edge? edge = hit switch
            {
                EdgeHit e => e.Edge,
                ControlPointHit c => c.Edge,
                _ => null,
            };
            if (edge is null)
                return false;
            _onEdgePicked = null;
            onPicked(edge);
            return true;
        }

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
        if (IsCreatingPolygon)
        {
            DraftCursor = p;
            return true;
        }

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
