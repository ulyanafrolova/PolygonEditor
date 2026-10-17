using PolygonEditor.App.Canvas;
using PolygonEditor.Core.Editing;
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
    private HelpForm? _help;
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
        _canvas = new CanvasControl { Dock = DockStyle.Fill, Controller = _controller, BackColor = Color.White };
        _canvas.HoverChanged += (_, _) => UpdateStatus();
        _canvas.MouseMove += (_, _) => UpdateStatus();
        _canvas.ContextMenuRequested += (target, location) => _contextMenu.Show(_canvas, target, location);

        // The panel and its contents size themselves to their text, so nothing is clipped at any scaling.
        var sidePanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Padding = new Padding(8),
        };
        sidePanel.Controls.Add(CreateLineAlgorithmGroup());
        var helpButton = new Button
        {
            Text = "Dokumentacja (F1)",
            AutoSize = true,
            Padding = new Padding(4),
            Margin = new Padding(3, 10, 3, 3),
            Dock = DockStyle.Fill, // in a FlowLayoutPanel: stretch to the widest control (the group)
        };
        helpButton.Click += (_, _) => ShowHelp();
        sidePanel.Controls.Add(helpButton);

        var statusStrip = new StatusStrip();
        statusStrip.Items.Add(_status);

        Controls.Add(sidePanel);
        Controls.Add(statusStrip);
        Controls.Add(_canvas);
        _canvas.BringToFront(); // fill the space left by the side panel and status bar
        UpdateStatus();
        ResumeLayout(false);
        PerformLayout();
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
            case Keys.Escape when _controller.CancelDrag() || _controller.CancelPick():
                OnSceneChanged();
                e.Handled = true;
                break;
            case Keys.Delete when !_controller.IsDragging && _controller.Hovered is VertexHit v && _editor.CanRemoveVertex(v.Vertex):
                _editor.RemoveVertex(v.Vertex);
                OnSceneChanged();
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
