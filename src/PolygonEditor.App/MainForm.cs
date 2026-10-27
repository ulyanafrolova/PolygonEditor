using PolygonEditor.App.Canvas;
using PolygonEditor.Core.Editing;
using PolygonEditor.Core.Geometry;
using PolygonEditor.Core.Model;
using PolygonEditor.Core.Persistence;
using PolygonEditor.Core.Rendering;
using PolygonEditor.Core.Scenes;

namespace PolygonEditor.App;

public sealed class MainForm : Form
{
    private const string Hints = "LPM: przeciągnij  |  PPM: menu  |  Del: usuń wierzchołek  |  Esc: anuluj  |  Ctrl+S / Ctrl+O: zapisz / wczytaj  |  F1: dokumentacja";
    private const string SceneFileFilter = "Scena edytora wielokątów (*.json)|*.json|Wszystkie pliki (*.*)|*.*";

    private readonly SceneEditor _editor = new(InitialScene.Create());
    private readonly EditorController _controller;
    private readonly EditorContextMenu _contextMenu;
    private readonly CanvasControl _canvas;
    private readonly ScrollPanel _viewport;
    private HelpForm? _help;

    // Free space kept to the right of / below the scene (labels are drawn next to points).
    private const int SceneMargin = 60;

    // The canvas grows with the scene only up to this size (logical px). A G1 arc whose far end lies
    // almost on its tangent line has a huge radius; it is simply cut off instead of making the
    // canvas (and its bitmap) gigantic.
    private const int MaxCanvasSize = 4000;
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

        var statusStrip = new StatusStrip();
        statusStrip.Items.Add(_status);

        Controls.Add(CreateToolbar());
        Controls.Add(statusStrip);
        Controls.Add(_viewport);
        _viewport.BringToFront(); // fill the space left by the toolbar and status bar
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

    /// <summary>
    /// Canvas = max(visible area, scene extent + margin), the scene part capped at MaxCanvasSize;
    /// scroll bars appear when the scene is larger than the visible area.
    /// </summary>
    private void UpdateCanvasSize()
    {
        Vec2 corner = SceneBounds.MaxCorner(_editor.Scene);
        int limit = LogicalToDeviceUnits(MaxCanvasSize);
        int sceneWidth = (int)Math.Min(limit, Math.Ceiling(corner.X) + SceneMargin);
        int sceneHeight = (int)Math.Min(limit, Math.Ceiling(corner.Y) + SceneMargin);
        var size = new Size(
            Math.Max(_viewport.ClientSize.Width, sceneWidth),
            Math.Max(_viewport.ClientSize.Height, sceneHeight));
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
            case Keys.S when e.Control:
                SaveScene();
                e.Handled = true;
                break;
            case Keys.O when e.Control:
                LoadScene();
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

    private void SaveScene()
    {
        if (_controller.IsDragging)
            return;
        using var dialog = new SaveFileDialog { Filter = SceneFileFilter, DefaultExt = "json", FileName = "scena.json", Title = "Zapisz scenę" };
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;
        try
        {
            SceneFile.Save(_editor.Scene, dialog.FileName);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, $"Nie udało się zapisać sceny:\n{ex.Message}", "Zapis sceny", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    /// <summary>Replaces the whole scene; an invalid file leaves the current scene untouched.</summary>
    private void LoadScene()
    {
        if (_controller.IsDragging)
            return;
        using var dialog = new OpenFileDialog { Filter = SceneFileFilter, Title = "Wczytaj scenę" };
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;
        try
        {
            Scene scene = SceneFile.Load(dialog.FileName);
            _controller.CancelPick();
            _editor.LoadScene(scene);
            OnSceneChanged();
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, $"Nie udało się wczytać sceny:\n{ex.Message}", "Wczytywanie sceny", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
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
            EdgeHit e => $"Krawędź {e.Edge.Name} ({e.Edge.Kind}, {(e.Edge.Kind == EdgeKind.Line ? "długość" : "cięciwa")} {e.Edge.Length:0.#}, grubość {e.Edge.Thickness} px)",
            PolygonHit p => $"Wielokąt {p.Target.Name}",
            _ => null,
        } ?? "";
        _status.Text = hovered.Length > 0 ? $"{hovered}    —    {Hints}" : Hints;
    }

    /// <summary>
    /// One-row bar at the top: line algorithm (radio buttons, as the task requires) on the left,
    /// scene file and documentation buttons on the right. A single-row TableLayoutPanel keeps every control
    /// vertically centred and sizes the bar to its content at any display scaling.
    /// </summary>
    private TableLayoutPanel CreateToolbar()
    {
        var bar = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 8,
            RowCount = 1,
            Padding = new Padding(6, 2, 6, 2),
        };
        for (int i = 0; i < 4; i++)
            bar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); // free space pushes the buttons right
        for (int i = 0; i < 3; i++)
            bar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        bar.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var label = new Label { Text = "Rysowanie odcinków:", AutoSize = true, Anchor = AnchorStyles.Left };
        var library = new RadioButton { Text = "Biblioteczny (GDI+)", AutoSize = true, Checked = true, Anchor = AnchorStyles.Left };
        var bresenham = new RadioButton { Text = "Bresenham (własny)", AutoSize = true, Anchor = AnchorStyles.Left };
        var wu = new RadioButton { Text = "Wu – antyaliasing (własny)", AutoSize = true, Anchor = AnchorStyles.Left };
        var saveButton = new Button { Text = "Zapisz scenę… (Ctrl+S)", AutoSize = true, Anchor = AnchorStyles.Right };
        var loadButton = new Button { Text = "Wczytaj scenę… (Ctrl+O)", AutoSize = true, Anchor = AnchorStyles.Right };
        var helpButton = new Button { Text = "Dokumentacja (F1)", AutoSize = true, Anchor = AnchorStyles.Right };
        saveButton.Click += (_, _) => SaveScene();
        loadButton.Click += (_, _) => LoadScene();
        helpButton.Click += (_, _) => ShowHelp();

        library.CheckedChanged += (_, _) =>
        {
            if (library.Checked) _canvas.LineAlgorithm = LineAlgorithm.Library;
        };
        bresenham.CheckedChanged += (_, _) =>
        {
            if (bresenham.Checked) _canvas.LineAlgorithm = LineAlgorithm.Bresenham;
        };
        wu.CheckedChanged += (_, _) =>
        {
            if (wu.Checked) _canvas.LineAlgorithm = LineAlgorithm.Wu;
        };

        // Radio buttons with the same parent form one group: checking one unchecks the other.
        bar.Controls.Add(label, 0, 0);
        bar.Controls.Add(library, 1, 0);
        bar.Controls.Add(bresenham, 2, 0);
        bar.Controls.Add(wu, 3, 0);
        bar.Controls.Add(saveButton, 5, 0);
        bar.Controls.Add(loadButton, 6, 0);
        bar.Controls.Add(helpButton, 7, 0);
        return bar;
    }
}
