using PolygonEditor.Core.Editing;
using PolygonEditor.Core.Model;
using PolygonEditor.Core.Persistence;
using PolygonEditor.Core.Scenes;

namespace PolygonEditor.Core.Tests;

public class SceneFileTests
{
    [Fact]
    public void RoundTrip_KeepsGeometryContinuityConstraintsAndThickness()
    {
        var scene = InitialScene.Create();
        var editor = new SceneEditor(scene);
        editor.SetThickness(scene.Polygons[0].Edges[1], 4);
        editor.SetThickness(scene.Polygons[1].Edges[1], 7);

        var loaded = SceneFile.Deserialize(SceneFile.Serialize(scene));

        Assert.Equal(scene.Polygons.Count, loaded.Polygons.Count);
        for (int p = 0; p < scene.Polygons.Count; p++)
        {
            Polygon a = scene.Polygons[p], b = loaded.Polygons[p];
            Assert.Equal(a.Name, b.Name);
            for (int i = 0; i < a.Vertices.Count; i++)
            {
                Assert.Equal(a.Vertices[i].Position, b.Vertices[i].Position);
                Assert.Equal(a.Vertices[i].Continuity, b.Vertices[i].Continuity);
                Assert.Equal(a.Vertices[i].IsContinuityExplicit, b.Vertices[i].IsContinuityExplicit);
                Assert.Equal(a.Edges[i].Kind, b.Edges[i].Kind);
                Assert.Equal(a.Edges[i].Thickness, b.Edges[i].Thickness);
                if (a.Edges[i].Kind == EdgeKind.Bezier)
                    Assert.Equal((a.Edges[i].Control1, a.Edges[i].Control2), (b.Edges[i].Control1, b.Edges[i].Control2));
            }
        }
        Assert.Equal(scene.Constraints.Select(c => (c.Id, c.Description)), loaded.Constraints.Select(c => (c.Id, c.Description)));
        Assert.True(Constraints.Solver.ConstraintSolver.AllSatisfied(loaded));
    }

    [Fact]
    public void Load_NewConstraintsGetNumbersAfterLoadedOnes()
    {
        var scene = InitialScene.Create();
        var loaded = SceneFile.Deserialize(SceneFile.Serialize(scene));

        var added = loaded.AddFixedLength(loaded.Polygons[2].Edges[0]);

        Assert.True(added.Id > scene.Constraints.Max(c => c.Id));
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("{\"format\":\"Other\",\"version\":1,\"polygons\":[]}")]
    [InlineData("{\"format\":\"PolygonEditor.Scene\",\"version\":1,\"polygons\":[{\"vertices\":[{\"x\":0,\"y\":0,\"continuity\":\"G0\"},{\"x\":9,\"y\":0,\"continuity\":\"G0\"}],\"edges\":[{\"kind\":\"Line\"},{\"kind\":\"Line\"}]}]}")]
    [InlineData("{\"format\":\"PolygonEditor.Scene\",\"version\":1,\"polygons\":[" + Triangle + "],\"constraints\":[{\"type\":\"FixedLength\",\"id\":1,\"edges\":[{\"polygon\":0,\"edge\":5}],\"length\":10}]}")]
    [InlineData("{\"format\":\"PolygonEditor.Scene\",\"version\":1,\"polygons\":[" + Triangle + "],\"constraints\":[{\"type\":\"FixedLength\",\"id\":1,\"edges\":[{\"polygon\":0,\"edge\":0}],\"length\":-3}]}")]
    [InlineData("{\"format\":\"PolygonEditor.Scene\",\"version\":1,\"polygons\":[{\"vertices\":[{\"x\":0,\"y\":0,\"continuity\":\"C1\"},{\"x\":90,\"y\":0,\"continuity\":\"G0\"},{\"x\":0,\"y\":90,\"continuity\":\"G0\"}],\"edges\":[{\"kind\":\"Arc\"},{\"kind\":\"Line\"},{\"kind\":\"Line\"}]}]}")]
    [InlineData("{\"format\":\"PolygonEditor.Scene\",\"version\":1,\"polygons\":[{\"vertices\":[{\"x\":0,\"y\":0,\"continuity\":\"G0\"},{\"x\":90,\"y\":0,\"continuity\":\"G0\"},{\"x\":0,\"y\":90,\"continuity\":\"G0\"}],\"edges\":[{\"kind\":\"Line\",\"thickness\":99},{\"kind\":\"Line\"},{\"kind\":\"Line\"}]}]}")]
    public void Load_RejectsInvalidFiles(string json)
    {
        Assert.Throws<InvalidDataException>(() => SceneFile.Deserialize(json));
    }

    private const string Triangle =
        "{\"vertices\":[{\"x\":0,\"y\":0,\"continuity\":\"G0\"},{\"x\":90,\"y\":0,\"continuity\":\"G0\"},{\"x\":0,\"y\":90,\"continuity\":\"G0\"}]," +
        "\"edges\":[{\"kind\":\"Line\"},{\"kind\":\"Line\"},{\"kind\":\"Line\"}]}";

    [Fact]
    public void Thickness_IsKeptWhenSplittingAndMergingEdges()
    {
        var scene = new Scene();
        var p = scene.AddPolygon((0, 0), (100, 0), (100, 100), (0, 100));
        var editor = new SceneEditor(scene);
        editor.SetThickness(p.Edges[0], 5);

        editor.InsertVertex(p.Edges[0], out Vertex middle);
        Assert.Equal(5, p.Edges[0].Thickness);
        Assert.Equal(5, p.Edges[1].Thickness);

        editor.RemoveVertex(middle);
        Assert.Equal(5, p.Edges[0].Thickness);

        editor.SetThickness(p.Edges[0], 99);
        Assert.Equal(Edge.MaxThickness, p.Edges[0].Thickness);
    }
}
