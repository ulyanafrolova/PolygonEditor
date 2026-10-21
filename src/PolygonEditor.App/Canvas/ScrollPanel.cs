namespace PolygonEditor.App.Canvas;

/// <summary>
/// Auto-scrolling container for the canvas. A plain Panel scrolls back to the top-left corner of
/// its child whenever the child gets focus (e.g. on every click), which would make the view jump.
/// </summary>
public sealed class ScrollPanel : Panel
{
    public ScrollPanel() => AutoScroll = true;

    protected override Point ScrollToControl(Control activeControl) => DisplayRectangle.Location;
}
