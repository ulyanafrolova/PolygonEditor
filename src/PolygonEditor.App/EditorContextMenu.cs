using PolygonEditor.Core.Editing;
using PolygonEditor.Core.Model;

namespace PolygonEditor.App;

/// <summary>Builds the right-click menu for the element under the cursor. All actions go through SceneEditor.</summary>
public sealed class EditorContextMenu
{
    private const string Unsolvable = "Nie można spełnić wszystkich ograniczeń – zmiana została cofnięta.";

    /// <summary>For topology changes (add/remove vertex), which are kept even when re-solving fails.</summary>
    public const string PartiallySolved = "Wierzchołek zmieniono, ale nie udało się przywrócić wszystkich ograniczeń i ciągłości.";
    private static readonly Font HeaderFont = new(SystemFonts.MenuFont ?? Control.DefaultFont, FontStyle.Bold);

    private readonly ContextMenuStrip _menu = new();
    private readonly SceneEditor _editor;
    private readonly EditorController _controller;
    private readonly Action _sceneChanged;
    private Control? _owner;

    public EditorContextMenu(SceneEditor editor, EditorController controller, Action sceneChanged)
    {
        _editor = editor;
        _controller = controller;
        _sceneChanged = sceneChanged;
    }

    public void Show(Control owner, HitTarget? target, Point location)
    {
        _owner = owner;
        // The menu instance is reused; old items are disposed only once the previous menu is closed.
        foreach (ToolStripItem old in _menu.Items.Cast<ToolStripItem>().ToList())
            old.Dispose();
        _menu.Items.Clear();

        switch (target)
        {
            case VertexHit v:
                AddVertexItems(v.Vertex);
                break;
            case ControlPointHit c:
                AddEdgeItems(c.Edge);
                break;
            case EdgeHit e:
                AddEdgeItems(e.Edge);
                break;
        }

        if (_menu.Items.Count > 0)
            _menu.Show(owner, location);
    }

    private void AddVertexItems(Vertex vertex)
    {
        _menu.Items.Add(Header($"Wierzchołek {vertex.Name}"));
        _menu.Items.Add(Item("Usuń wierzchołek", () => ReportTopology(_editor.RemoveVertex(vertex)), _editor.CanRemoveVertex(vertex)));

        if (!vertex.TouchesCurve)
            return;

        var continuity = new ToolStripMenuItem("Ciągłość");
        foreach (Continuity c in Enum.GetValues<Continuity>())
        {
            var item = Item(c.ToString(), () => Report(_editor.SetContinuity(vertex, c)), _editor.CanSetContinuity(vertex, c));
            item.Checked = vertex.Continuity == c;
            continuity.DropDownItems.Add(item);
        }
        _menu.Items.Add(continuity);
    }

    private void AddEdgeItems(Edge edge)
    {
        _menu.Items.Add(Header($"Krawędź {edge.Name}"));
        _menu.Items.Add(Item("Dodaj wierzchołek w środku", () => ReportTopology(_editor.InsertVertex(edge, out _))));
        _menu.Items.Add(new ToolStripSeparator());

        foreach (var (kind, text) in new[] { (EdgeKind.Line, "Odcinek"), (EdgeKind.Bezier, "Krzywa Béziera"), (EdgeKind.Arc, "Łuk okręgu") })
        {
            var item = Item(text, () => Report(_editor.SetEdgeKind(edge, kind)));
            item.Checked = edge.Kind == kind;
            _menu.Items.Add(item);
        }

        var thickness = new ToolStripMenuItem($"Grubość ({edge.Thickness} px)");
        for (int width = Edge.MinThickness; width <= Edge.MaxThickness; width++)
        {
            int chosen = width;
            var item = Item($"{width} px", () => _editor.SetThickness(edge, chosen));
            item.Checked = edge.Thickness == width;
            thickness.DropDownItems.Add(item);
        }
        _menu.Items.Add(thickness);
        _menu.Items.Add(new ToolStripSeparator());

        var fixedLength = _editor.FixedLengthOn(edge);
        string what = edge.Kind == EdgeKind.Line ? "długość" : "długość cięciwy";
        string lengthText = fixedLength is null ? $"Ustal {what}…" : $"Zmień {what} ({fixedLength.Length:0.#})…";
        _menu.Items.Add(Item(lengthText, () => AskLength(edge, fixedLength?.Length ?? edge.Length)));
        _menu.Items.Add(Item("Równoległa do… (kliknij drugą krawędź)", () => BeginParallel(edge)));

        var constraints = _editor.Scene.ConstraintsOn(edge).ToList();
        var remove = new ToolStripMenuItem("Usuń ograniczenie") { Enabled = constraints.Count > 0 };
        foreach (var constraint in constraints)
            remove.DropDownItems.Add(Item(constraint.Description, () => _editor.RemoveConstraint(constraint)));
        _menu.Items.Add(remove);
    }

    private void AskLength(Edge edge, double initial)
    {
        if (LengthDialog.Ask(_owner!, edge.Name, initial, edge.Kind != EdgeKind.Line) is { } length)
            Report(_editor.SetFixedLength(edge, length));
    }

    private void BeginParallel(Edge first)
    {
        _controller.BeginPickEdge(second =>
        {
            if (!_editor.CanAddParallel(first, second))
                Message("Tej pary krawędzi nie można uczynić równoległą: to ta sama krawędź, krawędzie są już równoległe " +
                        "(także pośrednio) albo dwie sąsiednie krawędzie stałyby się równoległe (bezpośrednio lub przez inne równoległości).");
            else
                Report(_editor.AddParallel(first, second));
            _sceneChanged();
        });
    }

    private void Report(bool success)
    {
        if (!success)
            Message(Unsolvable);
    }

    private void ReportTopology(bool solved)
    {
        if (!solved)
            Message(PartiallySolved);
    }

    private void Message(string text) =>
        MessageBox.Show(_owner, text, "Ograniczenia", MessageBoxButtons.OK, MessageBoxIcon.Warning);

    private ToolStripMenuItem Item(string text, Action action, bool enabled = true)
    {
        var item = new ToolStripMenuItem(text) { Enabled = enabled };
        item.Click += (_, _) =>
        {
            action();
            _sceneChanged();
        };
        return item;
    }

    /// <summary>
    /// Bold, non-clickable title. A ToolStripMenuItem (not a ToolStripLabel) so the menu measures
    /// its width correctly — a label in a context menu can be cut off.
    /// </summary>
    private static ToolStripMenuItem Header(string text) => new(text) { Font = HeaderFont, Enabled = false };
}
