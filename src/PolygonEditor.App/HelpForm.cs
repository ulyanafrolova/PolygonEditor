using PolygonEditor.Core.Documentation;

namespace PolygonEditor.App;

/// <summary>Built-in documentation: one tab per page, Markdown rendered into a read-only RichTextBox.</summary>
public sealed class HelpForm : Form
{
    private static readonly Font BodyFont = new("Segoe UI", 10f);
    private static readonly Font BoldFont = new("Segoe UI", 10f, FontStyle.Bold);
    private static readonly Font CodeFont = new("Consolas", 9.5f);
    private static readonly Font[] HeadingFonts =
    {
        new("Segoe UI", 16f, FontStyle.Bold),
        new("Segoe UI", 13f, FontStyle.Bold),
        new("Segoe UI", 11f, FontStyle.Bold),
    };

    public HelpForm()
    {
        SuspendLayout();
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        Text = "Dokumentacja – Edytor wielokątów";
        Size = new Size(820, 720);
        StartPosition = FormStartPosition.CenterParent;
        KeyPreview = true;

        var tabs = new TabControl { Dock = DockStyle.Fill };
        foreach (DocPage page in DocumentationPages.All())
        {
            var tab = new TabPage(page.Title);
            tab.Controls.Add(CreateView(page.Markdown));
            tabs.TabPages.Add(tab);
        }
        Controls.Add(tabs);
        ResumeLayout(false);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode == Keys.Escape)
            Close();
    }

    private static RichTextBox CreateView(string markdown)
    {
        var box = new RichTextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            BackColor = Color.White,
            BorderStyle = BorderStyle.None,
            DetectUrls = false,
            Font = BodyFont,
        };

        foreach (DocBlock block in MarkdownLite.Parse(markdown))
        {
            switch (block.Kind)
            {
                case DocBlockKind.Heading:
                    Append(box, block.Runs, HeadingFonts[block.Level - 1], indent: 0, bullet: false);
                    break;
                case DocBlockKind.Bullet:
                    Append(box, block.Runs, null, indent: block.Level == 1 ? 10 : 34, bullet: true);
                    break;
                case DocBlockKind.Code:
                    Append(box, block.Runs, CodeFont, indent: 20, bullet: false);
                    break;
                default:
                    Append(box, block.Runs, null, indent: 0, bullet: false);
                    break;
            }
        }
        box.Select(0, 0);
        return box;
    }

    /// <param name="font">Forced font for the whole block (headings, code); null = per-run body fonts.</param>
    private static void Append(RichTextBox box, IReadOnlyList<DocRun> runs, Font? font, int indent, bool bullet)
    {
        box.Select(box.TextLength, 0);
        box.SelectionIndent = indent;
        box.SelectionBullet = bullet;
        foreach (DocRun run in runs)
        {
            box.Select(box.TextLength, 0);
            box.SelectionFont = font ?? (run.Code ? CodeFont : run.Bold ? BoldFont : BodyFont);
            box.AppendText(run.Text);
        }
        box.Select(box.TextLength, 0);
        box.SelectionFont = BodyFont;
        box.AppendText(bullet ? "\n" : "\n\n");
        box.SelectionBullet = false;
        box.SelectionIndent = 0;
    }
}
