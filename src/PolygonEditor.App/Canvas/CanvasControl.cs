using PolygonEditor.Core.Editing;
using PolygonEditor.Core.Geometry;
using PolygonEditor.Core.Rendering;

namespace PolygonEditor.App.Canvas;

/// <summary>
/// Drawing area: renders the scene into an off-screen bitmap and forwards mouse input
/// to the toolkit-independent EditorController.
/// </summary>
public sealed class CanvasControl : Control
{
    private readonly SceneRenderer _renderer = new();
    private DirectBitmap? _bitmap;
    private LineAlgorithm _lineAlgorithm = LineAlgorithm.Library;

    public CanvasControl()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
    }

    public EditorController? Controller { get; set; }

    /// <summary>Hovered element changed (for the status bar).</summary>
    public event EventHandler? HoverChanged;

    /// <summary>Right click: element under the cursor (may be null) and the click location.</summary>
    public event Action<HitTarget?, Point>? ContextMenuRequested;

    public LineAlgorithm LineAlgorithm
    {
        get => _lineAlgorithm;
        set
        {
            _lineAlgorithm = value;
            Invalidate();
        }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (Controller is null || Controller.IsDragging)
            return;

        Vec2 p = ToVec(e.Location);
        if (e.Button == MouseButtons.Left && Controller.MouseDown(p))
            Invalidate();
        else if (e.Button == MouseButtons.Right && !Controller.IsCreatingPolygon)
            ContextMenuRequested?.Invoke(Controller.HitTest(p), e.Location);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (Controller is null)
            return;

        HitTarget? hoveredBefore = Controller.Hovered;
        if (Controller.MouseMove(ToVec(e.Location)))
            Invalidate();
        if (hoveredBefore != Controller.Hovered)
            HoverChanged?.Invoke(this, EventArgs.Empty);
        Cursor = Controller.IsPickingEdge || Controller.IsCreatingPolygon ? Cursors.Cross : CursorFor(Controller.Highlighted);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button == MouseButtons.Left && Controller?.MouseUp() == true)
            Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        if (Controller is { IsDragging: false, Hovered: not null })
        {
            Controller.ResetHover();
            HoverChanged?.Invoke(this, EventArgs.Empty);
            Invalidate();
        }
    }

    private static Cursor CursorFor(HitTarget? target) => target switch
    {
        VertexHit or ControlPointHit => Cursors.Hand,
        EdgeHit or PolygonHit => Cursors.SizeAll,
        _ => Cursors.Default,
    };

    private static Vec2 ToVec(Point p) => new(p.X, p.Y);

    protected override void OnPaint(PaintEventArgs e)
    {
        if (Controller is null || Width <= 0 || Height <= 0)
            return;

        DirectBitmap bitmap = EnsureBitmap();
        using (var target = new BitmapRenderTarget(bitmap))
        {
            ILineDrawer lines = _lineAlgorithm == LineAlgorithm.Bresenham
                ? new BresenhamLineDrawer(target)
                : new LibraryLineDrawer(target);
            PolygonDraft? draft = Controller.IsCreatingPolygon
                ? new PolygonDraft(Controller.DraftPoints, Controller.DraftCursor)
                : null;
            _renderer.Render(Controller.Editor.Scene, target, lines, Controller.Highlighted, draft);
        }
        e.Graphics.DrawImageUnscaled(bitmap.Bitmap, 0, 0);
    }

    private DirectBitmap EnsureBitmap()
    {
        if (_bitmap is null || _bitmap.Width != Width || _bitmap.Height != Height)
        {
            _bitmap?.Dispose();
            _bitmap = new DirectBitmap(Width, Height);
        }
        return _bitmap;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _bitmap?.Dispose();
        base.Dispose(disposing);
    }
}
