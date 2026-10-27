using System.Text.Json;
using System.Text.Json.Serialization;
using PolygonEditor.Core.Constraints;
using PolygonEditor.Core.Geometry;
using PolygonEditor.Core.Model;

namespace PolygonEditor.Core.Persistence;

/// <summary>
/// Saves and loads a scene as JSON: polygons (vertices with continuity, edges with kind, control
/// points and thickness) and constraints, which refer to edges by (polygon index, edge index).
/// Arcs need no extra data: their shape follows from the endpoints and continuity (ArcGeometry).
/// Positions are stored exactly, so a loaded scene satisfies its relations without re-solving.
/// </summary>
public static class SceneFile
{
    private const string FormatName = "PolygonEditor.Scene";
    private const int FormatVersion = 1;

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) },
    };

    public static void Save(Scene scene, string path) => File.WriteAllText(path, Serialize(scene));

    /// <exception cref="InvalidDataException">The file is not a valid scene.</exception>
    public static Scene Load(string path) => Deserialize(File.ReadAllText(path));

    public static string Serialize(Scene scene)
    {
        var polygons = scene.Polygons.Select(p => new PolygonDto(
            p.Vertices.Select(v => new VertexDto(v.Position.X, v.Position.Y, v.Continuity, v.IsContinuityExplicit)).ToList(),
            p.Edges.Select(e => new EdgeDto(
                e.Kind,
                e.Kind == EdgeKind.Bezier ? PointDto.Of(e.Control1) : null,
                e.Kind == EdgeKind.Bezier ? PointDto.Of(e.Control2) : null,
                e.Thickness)).ToList())).ToList();

        EdgeRefDto Ref(Edge edge) => new(IndexOf(scene, edge.Polygon), edge.Polygon.IndexOf(edge));
        var constraints = scene.Constraints.Select(c => c switch
        {
            FixedLengthConstraint f => new ConstraintDto(ConstraintType.FixedLength, f.Id, new[] { Ref(f.Edge) }, f.Length),
            ParallelConstraint p => new ConstraintDto(ConstraintType.Parallel, p.Id, new[] { Ref(p.A), Ref(p.B) }, null),
            _ => throw new NotSupportedException($"Unknown constraint type {c.GetType().Name}."),
        }).ToList();

        return JsonSerializer.Serialize(new SceneDto(FormatName, FormatVersion, polygons, constraints), Options);
    }

    /// <exception cref="InvalidDataException">The text is not a valid scene.</exception>
    public static Scene Deserialize(string json)
    {
        try
        {
            SceneDto dto = JsonSerializer.Deserialize<SceneDto>(json, Options) ?? throw Invalid("empty file");
            return Build(dto);
        }
        catch (Exception e) when (e is JsonException or ArgumentException or NullReferenceException)
        {
            throw Invalid(e.Message);
        }
    }

    private static Scene Build(SceneDto dto)
    {
        if (dto.Format != FormatName)
            throw Invalid("not a polygon editor scene");
        if (dto.Version != FormatVersion)
            throw Invalid($"unsupported version {dto.Version}");

        var scene = new Scene();
        foreach (PolygonDto p in dto.Polygons ?? throw Invalid("missing polygons"))
        {
            if (p.Vertices is null || p.Edges is null || p.Vertices.Count != p.Edges.Count)
                throw Invalid("each polygon needs as many edges as vertices");

            Polygon polygon = scene.AddPolygon(p.Vertices.Select(v => new Vec2(v.X, v.Y)));
            for (int i = 0; i < p.Vertices.Count; i++)
            {
                VertexDto v = p.Vertices[i];
                polygon.Vertices[i].RestoreContinuity(v.Continuity, v.Explicit);

                EdgeDto e = p.Edges[i];
                if (e.Kind == EdgeKind.Bezier && (e.Control1 is null || e.Control2 is null))
                    throw Invalid($"Bezier edge {polygon.Name}[{i}] has no control points");
                if (e.Thickness is < Edge.MinThickness or > Edge.MaxThickness)
                    throw Invalid($"thickness of {polygon.Name}[{i}] must be {Edge.MinThickness}–{Edge.MaxThickness}");
                Edge edge = polygon.Edges[i];
                edge.Restore(e.Kind, e.Control1?.ToVec() ?? default, e.Control2?.ToVec() ?? default);
                edge.Thickness = e.Thickness;
            }
        }

        Edge Resolve(EdgeRefDto r)
        {
            if (r.Polygon < 0 || r.Polygon >= scene.Polygons.Count)
                throw Invalid($"constraint refers to missing polygon {r.Polygon}");
            Polygon polygon = scene.Polygons[r.Polygon];
            if (r.Edge < 0 || r.Edge >= polygon.Edges.Count)
                throw Invalid($"constraint refers to missing edge {polygon.Name}[{r.Edge}]");
            return polygon.Edges[r.Edge];
        }

        foreach (ConstraintDto c in dto.Constraints ?? new List<ConstraintDto>())
        {
            var edges = c.Edges ?? throw Invalid($"constraint {c.Id} has no edges");
            switch (c.Type)
            {
                case ConstraintType.FixedLength when edges.Length == 1 && c.Length is { } length:
                    scene.AddLoaded(new FixedLengthConstraint(c.Id, Resolve(edges[0]), length));
                    break;
                case ConstraintType.Parallel when edges.Length == 2:
                    Edge a = Resolve(edges[0]), b = Resolve(edges[1]);
                    if (a == b)
                        throw Invalid($"constraint {c.Id}: an edge cannot be parallel to itself");
                    scene.AddLoaded(new ParallelConstraint(c.Id, a, b));
                    break;
                default:
                    throw Invalid($"constraint {c.Id} is malformed");
            }
        }

        if (ContinuityRules.Violations(scene).FirstOrDefault() is { } bad)
            throw Invalid($"continuity {bad.Continuity} is not allowed at {bad.Name}");
        return scene;
    }

    private static int IndexOf(Scene scene, Polygon polygon)
    {
        for (int i = 0; i < scene.Polygons.Count; i++)
            if (scene.Polygons[i] == polygon)
                return i;
        throw new ArgumentException("Polygon does not belong to the scene.");
    }

    private static InvalidDataException Invalid(string reason) => new($"Invalid scene file: {reason}.");

    // ---------- file format ----------

    private enum ConstraintType
    {
        FixedLength,
        Parallel,
    }

    private sealed record SceneDto(string? Format, int Version, List<PolygonDto>? Polygons, List<ConstraintDto>? Constraints);

    private sealed record PolygonDto(List<VertexDto>? Vertices, List<EdgeDto>? Edges);

    private sealed record VertexDto(double X, double Y, Continuity Continuity, bool Explicit);

    private sealed record EdgeDto(EdgeKind Kind, PointDto? Control1, PointDto? Control2, int Thickness = Edge.MinThickness);

    private sealed record PointDto(double X, double Y)
    {
        public static PointDto Of(Vec2 v) => new(v.X, v.Y);
        public Vec2 ToVec() => new(X, Y);
    }

    private sealed record EdgeRefDto(int Polygon, int Edge);

    private sealed record ConstraintDto(ConstraintType Type, int Id, EdgeRefDto[]? Edges, double? Length);
}
