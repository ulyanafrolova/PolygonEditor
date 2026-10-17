using PolygonEditor.Core.Documentation;

namespace PolygonEditor.Core.Tests;

public class DocumentationTests
{
    [Fact]
    public void AllPagesAreEmbeddedInOrderWithTitles()
    {
        var titles = DocumentationPages.All().Select(p => p.Title).ToList();

        Assert.Equal(new[] { "Instrukcja obsługi", "Przyjęte założenia", "Model danych i architektura", "Algorytmy" }, titles);
    }

    [Theory]
    [InlineData("Instrukcja obsługi", "Mysz")]
    [InlineData("Instrukcja obsługi", "Klawiatura")]
    [InlineData("Przyjęte założenia", "Wariant zadania")]
    [InlineData("Model danych i architektura", "Model (folder Model)")]
    [InlineData("Algorytmy", "Solver ograniczeń")]
    [InlineData("Algorytmy", "Rysowanie odcinków – algorytm Bresenhama")]
    [InlineData("Algorytmy", "Segment Béziera – algorytm przyrostowy")]
    public void RequiredSectionsArePresent(string page, string heading)
    {
        var blocks = MarkdownLite.Parse(DocumentationPages.All().Single(p => p.Title == page).Markdown);

        Assert.Contains(blocks, b => b.Kind == DocBlockKind.Heading && string.Concat(b.Runs.Select(r => r.Text)) == heading);
    }

    [Fact]
    public void Parser_HandlesHeadingsBulletsParagraphsCodeAndInlineStyles()
    {
        const string markdown = "# Title\n\nLine one\nline **two** with `code`.\n\n- item\n  - nested\n\n```\nvar x = 1;\n```\n";

        var blocks = MarkdownLite.Parse(markdown);

        Assert.Equal(DocBlockKind.Heading, blocks[0].Kind);
        Assert.Equal(1, blocks[0].Level);

        Assert.Equal(DocBlockKind.Paragraph, blocks[1].Kind);
        Assert.Equal("Line one line two with code.", string.Concat(blocks[1].Runs.Select(r => r.Text)));
        Assert.Contains(blocks[1].Runs, r => r.Bold && r.Text == "two");
        Assert.Contains(blocks[1].Runs, r => r.Code && r.Text == "code");

        Assert.Equal((DocBlockKind.Bullet, 1), (blocks[2].Kind, blocks[2].Level));
        Assert.Equal((DocBlockKind.Bullet, 2), (blocks[3].Kind, blocks[3].Level));

        Assert.Equal(DocBlockKind.Code, blocks[4].Kind);
        Assert.Equal("var x = 1;", blocks[4].Runs[0].Text);
        Assert.Equal(5, blocks.Count);
    }

    [Fact]
    public void Docs_HaveNoUnbalancedInlineMarkers()
    {
        foreach (var page in DocumentationPages.All())
            foreach (string line in page.Markdown.Split('\n').Where(l => !l.TrimStart().StartsWith("```")))
            {
                Assert.True(line.Split("**").Length % 2 == 1, $"unbalanced ** in {page.Title}: {line}");
                Assert.True(line.Count(c => c == '`') % 2 == 0, $"unbalanced ` in {page.Title}: {line}");
            }
    }
}
