using System.Text;

namespace PolygonEditor.Core.Documentation;

public enum DocBlockKind
{
    Heading,
    Paragraph,
    Bullet,
    Code,
}

/// <summary>Inline text fragment: plain, **bold** or `code`.</summary>
public sealed record DocRun(string Text, bool Bold = false, bool Code = false);

/// <summary>Level: heading level (1–3) or bullet nesting (1–2).</summary>
public sealed record DocBlock(DocBlockKind Kind, int Level, IReadOnlyList<DocRun> Runs);

/// <summary>
/// Minimal Markdown subset used by the built-in documentation:
/// "#".."###" headings, "- " bullets (2-space indent = nested), ``` code blocks,
/// paragraphs of consecutive lines, inline **bold** and `code`.
/// </summary>
public static class MarkdownLite
{
    public static IReadOnlyList<DocBlock> Parse(string markdown)
    {
        var blocks = new List<DocBlock>();
        var paragraph = new List<string>();
        var code = new StringBuilder();
        bool inCode = false;

        void FlushParagraph()
        {
            if (paragraph.Count == 0) return;
            blocks.Add(new DocBlock(DocBlockKind.Paragraph, 0, ParseInline(string.Join(' ', paragraph))));
            paragraph.Clear();
        }

        foreach (string rawLine in markdown.Replace("\r\n", "\n").Split('\n'))
        {
            string line = rawLine.TrimEnd();

            if (line.TrimStart().StartsWith("```"))
            {
                if (inCode)
                {
                    blocks.Add(new DocBlock(DocBlockKind.Code, 0, new[] { new DocRun(code.ToString().TrimEnd('\n'), Code: true) }));
                    code.Clear();
                }
                else
                    FlushParagraph();
                inCode = !inCode;
                continue;
            }
            if (inCode)
            {
                code.Append(line).Append('\n');
                continue;
            }

            string trimmed = line.TrimStart();
            if (trimmed.Length == 0)
            {
                FlushParagraph();
            }
            else if (trimmed.StartsWith('#'))
            {
                FlushParagraph();
                int level = trimmed.TakeWhile(c => c == '#').Count();
                blocks.Add(new DocBlock(DocBlockKind.Heading, Math.Min(level, 3), ParseInline(trimmed[level..].Trim())));
            }
            else if (trimmed.StartsWith("- "))
            {
                FlushParagraph();
                int indent = line.Length - trimmed.Length;
                blocks.Add(new DocBlock(DocBlockKind.Bullet, indent >= 2 ? 2 : 1, ParseInline(trimmed[2..])));
            }
            else
            {
                paragraph.Add(trimmed);
            }
        }
        FlushParagraph();
        return blocks;
    }

    /// <summary>Splits text into runs on ** (bold) and ` (code) markers.</summary>
    public static IReadOnlyList<DocRun> ParseInline(string text)
    {
        var runs = new List<DocRun>();
        var current = new StringBuilder();
        bool bold = false, code = false;

        void Flush()
        {
            if (current.Length > 0)
                runs.Add(new DocRun(current.ToString(), bold, code));
            current.Clear();
        }

        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] == '`')
            {
                Flush();
                code = !code;
            }
            else if (!code && text[i] == '*' && i + 1 < text.Length && text[i + 1] == '*')
            {
                Flush();
                bold = !bold;
                i++;
            }
            else
                current.Append(text[i]);
        }
        Flush();
        return runs;
    }
}
