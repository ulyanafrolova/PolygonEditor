using PolygonEditor.App.Canvas;
using PolygonEditor.Core.Editing;
using PolygonEditor.Core.Geometry;
using PolygonEditor.Core.Rendering;
using PolygonEditor.Core.Scenes;

namespace PolygonEditor.App;

public sealed class MainForm : Form
{
    private const string Hints = "LPM: przeciągnij  |  PPM: menu  |  Del: usuń wierzchołek  |  Esc: anuluj  |  F1: dokumentacja";

    private readonly SceneEditor _editor = new(InitialScene.Create());
    private readonly EditorController _controller;
    private readonly EditorContextMenu _contextMenu;
    private readonly CanvasControl _canvas;
    private readonly ScrollPanel _viewport;
    private HelpForm? _help;

    // Free space kept to the right of / below the scene (labels are drawn next to points).
    private const int SceneMargin = 60;
    private readonly ToolStripStatusLabel _status = new() { Spring = true, TextAlign = ContentAlignment.MiddleLeft };

    public MainForm()
    {
        SuspendLayout();
        // Sizes below are written for 100% display scaling (96 DPI); WinForms rescales them to the actual DPI.
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        Text = "Edytor wielokątów";
        ClientSize = new Size(1200, 780);
        StartPosition = FormStartPosition.CenterScreen;
        KeyPreview = true;

        _controller = new EditorController(_editor);
        _contextMenu = new EditorContextMenu(_editor, _controller, OnSceneChanged);
        // The canvas sits in a scrolling viewport: it is at least as large as the viewport and grows
        // to contain the whole scene, so a small window shows scroll bars instead of cutting shapes off.
        _canvas = new CanvasControl { Location = Point.Empty, Controller = _controller, BackColor = Color.White };
        _canvas.HoverChanged += (_, _) => UpdateStatus();
        _canvas.MouseMove += (_, _) =>
        {
            UpdateStatus();
            if (_controller.IsDragging)
                UpdateCanvasSize();
        };
        _canvas.MouseUp += (_, _) => UpdateCanvasSize();
        _canvas.ContextMenuRequested += (target, location) => _contextMenu.Show(_canvas, target, location);
        _viewport = new ScrollPanel { Dock = DockStyle.Fill, BackColor = Color.White };
        _viewport.Controls.Add(_canvas);
        _viewport.Resize += (_, _) => UpdateCanvasSize();

        // One auto-sized column: its width is the group's; the button is stretched to it (Anchor),
        // so the panel is exactly as wide as its widest content at any display scaling.
        var sidePanel = new TableLayoutPanel
        {
            Dock = DockStyle.Right,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            Padding = new Padding(8),
        };
        sidePanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        sidePanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        sidePanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        GroupBox lineGroup = CreateLineAlgorithmGroup();
        lineGroup.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        var helpButton = new Button
        {
            Text = "Dokumentacja (F1)",
            AutoSize = true,
            Padding = new Padding(4),
            Margin = new Padding(3, 10, 3, 3),
            Anchor = AnchorStyles.Left | AnchorStyles.Right,
        };
        helpButton.Click += (_, _) => ShowHelp();
        sidePanel.Controls.Add(lineGroup, 0, 0);
        sidePanel.Controls.Add(helpButton, 0, 1);

        var statusStrip = new StatusStrip();
        statusStrip.Items.Add(_status);

        Controls.Add(sidePanel);
        Controls.Add(statusStrip);
        Controls.Add(_viewport);
        _viewport.BringToFront(); // fill the space left by the side panel and status bar
        UpdateStatus();
        ResumeLayout(false);
        PerformLayout();
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        // Minimum outer size, written for 100% scaling and converted to the actual DPI.
        MinimumSize = LogicalToDeviceUnits(new Size(720, 480));
        UpdateCanvasSize();
    }

    /// <summary>Canvas = max(visible area, scene extent + margin); scroll bars appear when the scene is larger.</summary>
    private void UpdateCanvasSize()
    {
        Vec2 corner = SceneBounds.MaxCorner(_editor.Scene);
        var size = new Size(
            Math.Max(_viewport.ClientSize.Width, (int)Math.Ceiling(corner.X) + SceneMargin),
            Math.Max(_viewport.ClientSize.Height, (int)Math.Ceiling(corner.Y) + SceneMargin));
        if (_canvas.Size != size)
            _canvas.Size = size;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        switch (e.KeyCode)
        {
            case Keys.F1:
                ShowHelp();
                e.Handled = true;
                break;
            case Keys.Escape:
                bool cancelled = _controller.CancelDrag() || _controller.CancelPick();
                if (cancelled)
                {
                    OnSceneChanged();
                    e.Handled = true;
                }
                break;
            case Keys.Delete when !_controller.IsDragging && _controller.Hovered is VertexHit v && _editor.CanRemoveVertex(v.Vertex):
                bool solved = _editor.RemoveVertex(v.Vertex);
                OnSceneChanged();
                if (!solved)
                    MessageBox.Show(this, EditorContextMenu.PartiallySolved, "Ograniczenia", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                e.Handled = true;
                break;
        }
    }

    /// <summary>Non-modal, single instance, so it can stay open next to the editor.</summary>
    private void ShowHelp()
    {
        if (_help is null || _help.IsDisposed)
            _help = new HelpForm();
        if (_help.Visible)
            _help.Activate();
        else
            _help.Show(this);
    }

    private void OnSceneChanged()
    {
        _controller.ResetHover();
        UpdateStatus();
        UpdateCanvasSize();
        _canvas.Invalidate();
    }

    private void UpdateStatus()
    {
        if (_controller.IsPickingEdge)
        {
            _status.Text = "Równoległość: kliknij drugą krawędź (Esc – anuluj)";
            return;
        }
        if (_controller.IsDragging && _editor.LastMoveWasRigid)
        {
            _status.Text = "Ograniczeń nie da się spełnić – przesuwany jest cały wielokąt";
            return;
        }

        string hovered = _controller.Hovered switch
        {
            VertexHit v => $"Wierzchołek {v.Vertex.Name} ({v.Vertex.Continuity})",
            ControlPointHit c => $"Punkt kontrolny {c.Index} krawędzi {c.Edge.Name}",
            EdgeHit e => $"Krawędź {e.Edge.Name} ({e.Edge.Kind}, długość {e.Edge.Length:0.#})",
            PolygonHit p => $"Wielokąt {p.Target.Name}",
            _ => null,
        } ?? "";
        _status.Text = hovered.Length > 0 ? $"{hovered}    —    {Hints}" : Hints;
    }

    private GroupBox CreateLineAlgorithmGroup()
    {
        var group = new GroupBox
        {
            Text = "Rysowanie odcinków",
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(8, 4, 8, 6),
        };
        var radios = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
        };
        var library = new RadioButton { Text = "Biblioteczny (GDI+)", AutoSize = true, Checked = true };
        var bresenham = new RadioButton { Text = "Bresenham (własny)", AutoSize = true };

        library.CheckedChanged += (_, _) =>
        {
            if (library.Checked) _canvas.LineAlgorithm = LineAlgorithm.Library;
        };
        bresenham.CheckedChanged += (_, _) =>
        {
            if (bresenham.Checked) _canvas.LineAlgorithm = LineAlgorithm.Bresenham;
        };

        radios.Controls.Add(library);
        radios.Controls.Add(bresenham);
        group.Controls.Add(radios);
        return group;
    }
}
