namespace PolygonEditor.Core.Documentation;

public sealed record DocPage(string Title, string Markdown);

/// <summary>
/// Built-in documentation pages: the Documentation/*.md files embedded in this assembly,
/// ordered by file name; the page title is the first "# " heading.
/// </summary>
public static class DocumentationPages
{
    private const string Prefix = "Documentation.";

    public static IReadOnlyList<DocPage> All()
    {
        var assembly = typeof(DocumentationPages).Assembly;
        return assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith(Prefix) && name.EndsWith(".md"))
            .OrderBy(name => name, StringComparer.Ordinal)
            .Select(name =>
            {
                using var stream = assembly.GetManifestResourceStream(name)!;
                using var reader = new StreamReader(stream);
                string markdown = reader.ReadToEnd();
                string title = markdown.Split('\n').FirstOrDefault(l => l.StartsWith("# "))?[2..].Trim() ?? name;
                return new DocPage(title, markdown);
            })
            .ToList();
    }
}
