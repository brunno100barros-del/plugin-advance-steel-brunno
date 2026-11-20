using System.Collections;
using System.Text.Json.Nodes;
using Autodesk.AdvanceSteel.CADAccess;
using Autodesk.AdvanceSteel.Geometry;
using Pasb.Diagnostics.Core;
using AsId = Autodesk.AdvanceSteel.CADLink.Database.ObjectId;

namespace Pasb.Diagnostics.AdvanceSteel;

/// <summary>
/// Serialização sem perda dos tipos da Autodesk (docs/DIAGNOSTIC-DESIGN.md, seção 6).
/// Só lê propriedades de valor (x, y, z, Values...): não chama métodos que alteram o objeto.
/// </summary>
internal static class AsValueSerializer
{
    public static bool TrySerialize(object value, out JsonNode? node, out ReadStatus status)
    {
        status = ReadStatus.Read;
        node = Convert(value, depth: 0);
        if (node is null) return false;
        if (node is JsonArray { Count: 0 }) status = ReadStatus.Empty;
        return true;
    }

    /// <summary>Retorna null quando o tipo não é conhecido.</summary>
    private static JsonNode? Convert(object? value, int depth)
    {
        switch (value)
        {
            case null:
                return null;
            case JsonNode already:
                // Resultado de métodos com parâmetros "out", já montado pelo leitor.
                return already.DeepClone();
            case Point3d p:
                return new JsonArray(N(p.x), N(p.y), N(p.z));
            case Vector3d v:
                return new JsonArray(N(v.x), N(v.y), N(v.z));
            case Point2d p2:
                return new JsonArray(N(p2.x), N(p2.y));
            case Vector2d v2:
                return new JsonArray(N(v2.x), N(v2.y));
            case Matrix3d m:
                return Matrix(m);
            case Plane pl:
                return new JsonObject { ["origin"] = Convert(pl.Origin, depth + 1), ["normal"] = Convert(pl.Normal, depth + 1) };
            case Extents e:
                return new JsonObject
                {
                    ["isValid"] = e.IsValid,
                    ["min"] = Convert(e.MinPoint, depth + 1),
                    ["max"] = Convert(e.MaxPoint, depth + 1),
                };
            case Polyline3d poly:
                return Polyline(poly, depth);
            case AsId id:
                return Id(id);
            case FilerObject fo:
                return new JsonObject { ["handle"] = fo.Handle, ["clrType"] = fo.GetType().FullName };
            case string:
                return null; // tratado pelo núcleo
        }

        // Coleções de tipos conhecidos (ObjectId, FilerObject, Point3d...). Limite de profundidade evita recursão.
        if (depth < 2 && value is IEnumerable seq)
        {
            var arr = new JsonArray();
            foreach (object? item in seq)
            {
                if (ValueSerializer.TrySerialize(item, out JsonNode? basic, out _))
                {
                    arr.Add(basic);
                    continue;
                }
                JsonNode? n = Convert(item, depth + 1);
                if (n is null) return null; // item de tipo desconhecido: a coleção inteira fica para revisão
                arr.Add(n);
            }
            return arr;
        }
        return null;
    }

    private static JsonNode N(double d) => ValueSerializer.Number(d);

    private static JsonNode Id(AsId id)
    {
        if (id.IsNull()) return new JsonObject { ["isNull"] = true };
        return new JsonObject { ["handle"] = id.Handle };
    }

    private static JsonNode Matrix(Matrix3d m)
    {
        var rows = new JsonArray();
        double[][] values = m.Values;
        foreach (double[] row in values)
        {
            var r = new JsonArray();
            foreach (double d in row) r.Add(N(d));
            rows.Add(r);
        }
        return rows;
    }

    private static JsonNode Polyline(Polyline3d poly, int depth)
    {
        var vertices = new JsonArray();
        foreach (Point3d p in poly.Vertices) vertices.Add(Convert(p, depth + 1));
        return new JsonObject
        {
            ["isClosed"] = poly.IsClosed,
            ["vertexCount"] = poly.VertexCount,
            ["vertices"] = vertices,
        };
    }
}
