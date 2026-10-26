namespace PolygonEditor.App;

/// <summary>Asks for an edge length; the current length is the default value.</summary>
public sealed class LengthDialog : Form
{
    private readonly NumericUpDown _value;
    private readonly double _initial;

    private LengthDialog(string edgeName, double initial, bool isCurve)
    {
        _initial = initial;
        SuspendLayout();
        // Layout below is written for 96 DPI; WinForms rescales positions and sizes to the actual DPI.
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        Text = isCurve ? $"Długość cięciwy krawędzi {edgeName}" : $"Długość krawędzi {edgeName}";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = MinimizeBox = ShowInTaskbar = false;
        ClientSize = new Size(260, 90);

        var label = new Label { Text = "Długość:", Location = new Point(12, 16), AutoSize = true };
        _value = new NumericUpDown
        {
            Location = new Point(80, 12),
            Width = 160,
            DecimalPlaces = 2,
            Minimum = 1,
            Maximum = 100000,
            Value = (decimal)Math.Clamp(initial, 1, 100000),
        };
        var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Location = new Point(84, 52), Width = 75 };
        var cancel = new Button { Text = "Anuluj", DialogResult = DialogResult.Cancel, Location = new Point(165, 52), Width = 75 };

        Controls.AddRange(new Control[] { label, _value, ok, cancel });
        AcceptButton = ok;
        CancelButton = cancel;
        Shown += (_, _) => _value.Select(0, _value.Text.Length);
        ResumeLayout(false);
        PerformLayout();
    }

    /// <returns>The entered length, or null if cancelled.</returns>
    /// <param name="isCurve">Curved edge: the length is the chord's, which the title says.</param>
    public static double? Ask(IWin32Window owner, string edgeName, double initial, bool isCurve)
    {
        using var dialog = new LengthDialog(edgeName, initial, isCurve);
        return dialog.ShowDialog(owner) == DialogResult.OK ? dialog.Result : null;
    }

    /// <summary>
    /// The box shows the value rounded to 0.01; if the user did not change it, the exact current
    /// length is kept, so confirming the default does not move the edge.
    /// </summary>
    private double Result
    {
        get
        {
            double entered = (double)_value.Value;
            return Math.Abs(entered - _initial) < 0.005 ? _initial : entered;
        }
    }
}
