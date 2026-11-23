using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Pasb.Model;
using static Pasb.IfcExport.StepWriter;

namespace Pasb.IfcExport;

/// <summary>
/// Corpo sólido real do Advance Steel no IFC4 (docs/IFC-BODY.md):
/// - cada peça: IfcPolygonalFaceSet (vértices numa IfcCartesianPointList3D; faces com furos em IfcIndexedPolygonalFaceWithVoids);
/// - parafusos e chumbadores: o corpo do conjunto é separado por parafuso (face → eixo mais próximo), e cada parafuso igual
///   vira uma IfcRepresentationMap reutilizada por IfcMappedItem (como o steelXbim faz), o que mantém o arquivo leve.
/// </summary>
internal sealed class BodyGeometry
{
    private readonly StepWriter _w;
    private readonly int _bodyCtx;
    private readonly Dictionary<string, int> _maps = new(StringComparer.Ordinal);
    private int _mapOrigin;

    public BodyGeometry(StepWriter w, int bodyCtx) { _w = w; _bodyCtx = bodyCtx; }

    /// <summary>Quantas geometrias de parafuso diferentes foram gravadas (IfcRepresentationMap).</summary>
    public int MapCount => _maps.Count;

    /// <summary>Representação "Body" com o corpo inteiro, em metros, coordenadas do modelo.</summary>
    public int Tessellation(NeutralMesh mesh, double s)
    {
        int faceSet = FaceSet(mesh.Vertices, mesh.Faces, i => new[] { mesh.Vertices[3 * i] * s, mesh.Vertices[3 * i + 1] * s, mesh.Vertices[3 * i + 2] * s });
        return _w.Add("IfcShapeRepresentation", Ref(_bodyCtx), Str("Body"), Str("Tessellation"), "(" + Ref(faceSet) + ")");
    }

    /// <summary>
    /// Representação "Body" de um conjunto de parafusos como parafusos repetidos (IfcMappedItem). Nulo se a separação não fechar
    /// (faces divididas de forma desigual entre os parafusos); o chamador usa então o corpo inteiro.
    /// </summary>
    public int? MappedBolts(NeutralMesh mesh, IReadOnlyList<double[]> midpoints, double[] normal, double[]? xDirection, double s)
    {
        int n = midpoints.Count;
        if (n == 0) return null;
        double[] z = Vec.Normalize(normal);
        double[] x = xDirection is not null && Vec.Length(Vec.Reject(xDirection, z)) > 1e-6 ? Vec.Normalize(Vec.Reject(xDirection, z)) : Vec.Normalize(Vec.Perpendicular(z));
        double[] y = Vec.Cross(z, x);

        // Face → parafuso cujo eixo (ponto médio + normal) passa mais perto do centro do contorno externo.
        var facesOf = new List<int[][]>[n];
        for (int i = 0; i < n; i++) facesOf[i] = new List<int[][]>();
        foreach (int[][] face in mesh.Faces)
        {
            double[] c = new double[3];
            foreach (int vi in face[0]) { c[0] += mesh.Vertices[3 * vi]; c[1] += mesh.Vertices[3 * vi + 1]; c[2] += mesh.Vertices[3 * vi + 2]; }
            c = Vec.Scale(c, 1.0 / face[0].Length);
            int best = 0; double bestD = double.MaxValue;
            for (int i = 0; i < n; i++)
            {
                double d = Vec.Length(Vec.Reject(Vec.Sub(c, midpoints[i]), z));
                if (d < bestD) { bestD = d; best = i; }
            }
            facesOf[best].Add(face);
        }
        int faceCount = facesOf[0].Count;
        if (faceCount < 4 || facesOf.Any(f => f.Count != faceCount)) return null;

        int ax = _w.Add("IfcDirection", Point(x)), ay = _w.Add("IfcDirection", Point(y)), az = _w.Add("IfcDirection", Point(z));
        var items = new List<int>(n);
        for (int i = 0; i < n; i++)
        {
            // Geometria local do parafuso: vértices renumerados, coordenadas (m) no sistema (x, y, z) com origem no ponto médio.
            var local = new Dictionary<int, int>();
            var faces = new List<int[][]>(facesOf[i].Count);
            foreach (int[][] face in facesOf[i])
                faces.Add(face.Select(loop => loop.Select(vi => local.TryGetValue(vi, out int li) ? li : local[vi] = local.Count).ToArray()).ToArray());
            var coords = new double[local.Count * 3];
            foreach (var (vi, li) in local)
            {
                double[] d = Vec.Sub(new[] { mesh.Vertices[3 * vi], mesh.Vertices[3 * vi + 1], mesh.Vertices[3 * vi + 2] }, midpoints[i]);
                coords[3 * li] = Vec.Dot(d, x) * s; coords[3 * li + 1] = Vec.Dot(d, y) * s; coords[3 * li + 2] = Vec.Dot(d, z) * s;
            }
            string key = Key(coords, faces);
            if (!_maps.TryGetValue(key, out int map))
            {
                int faceSet = FaceSet(coords, faces, li => new[] { coords[3 * li], coords[3 * li + 1], coords[3 * li + 2] });
                int rep = _w.Add("IfcShapeRepresentation", Ref(_bodyCtx), Str("Body"), Str("Tessellation"), "(" + Ref(faceSet) + ")");
                if (_mapOrigin == 0)
                    _mapOrigin = _w.Add("IfcAxis2Placement3D", Ref(_w.Add("IfcCartesianPoint", Point(0, 0, 0))), Null, Null);
                _maps[key] = map = _w.Add("IfcRepresentationMap", Ref(_mapOrigin), Ref(rep));
            }
            int origin = _w.Add("IfcCartesianPoint", Point(Coord(midpoints[i][0] * s), Coord(midpoints[i][1] * s), Coord(midpoints[i][2] * s)));
            int op = _w.Add("IfcCartesianTransformationOperator3D", Ref(ax), Ref(ay), Ref(origin), Real(1), Ref(az));
            items.Add(_w.Add("IfcMappedItem", Ref(map), Ref(op)));
        }
        return _w.Add("IfcShapeRepresentation", Ref(_bodyCtx), Str("Body"), Str("MappedRepresentation"), Refs(items));
    }

    /// <summary>IfcPolygonalFaceSet fechado. Índices IFC começam em 1.</summary>
    private int FaceSet(double[] vertices, IReadOnlyList<int[][]> faces, Func<int, double[]> point)
    {
        int count = vertices.Length / 3;
        var sb = new StringBuilder("(");
        for (int i = 0; i < count; i++)
        {
            double[] p = point(i);
            if (i > 0) sb.Append(',');
            sb.Append('(').Append(Real(Coord(p[0]))).Append(',').Append(Real(Coord(p[1]))).Append(',').Append(Real(Coord(p[2]))).Append(')');
        }
        int list = _w.Add("IfcCartesianPointList3D", sb.Append(')').ToString());
        var faceIds = new List<int>(faces.Count);
        foreach (int[][] face in faces)
        {
            int[]? outer = Clean(face[0]);
            if (outer is null) continue;
            var inner = face.Skip(1).Select(Clean).Where(l => l is not null).ToList();
            faceIds.Add(inner.Count == 0
                ? _w.Add("IfcIndexedPolygonalFace", Indexes(outer))
                : _w.Add("IfcIndexedPolygonalFaceWithVoids", Indexes(outer), "(" + string.Join(",", inner.Select(l => Indexes(l!))) + ")"));
        }
        return _w.Add("IfcPolygonalFaceSet", Ref(list), Bool(true), Refs(faceIds), Null);
    }

    /// <summary>Contorno sem índices repetidos em sequência nem o primeiro repetido no fim; nulo se sobrar menos de 3.</summary>
    internal static int[]? Clean(int[] loop)
    {
        var r = new List<int>(loop.Length);
        foreach (int i in loop) if (r.Count == 0 || r[^1] != i) r.Add(i);
        while (r.Count > 1 && r[^1] == r[0]) r.RemoveAt(r.Count - 1);
        return r.Count >= 3 ? r.ToArray() : null;
    }

    private static string Indexes(int[] loop) => "(" + string.Join(",", loop.Select(i => (i + 1).ToString(CultureInfo.InvariantCulture))) + ")";

    /// <summary>Metros com 5 casas (0,01 mm): precisão do corpo lido, sem os resíduos de ponto flutuante.</summary>
    private static double Coord(double m) => Math.Round(m, 5);

    /// <summary>Chave da geometria local (0,1 mm): parafusos iguais compartilham a mesma IfcRepresentationMap.</summary>
    private static string Key(double[] coords, List<int[][]> faces)
    {
        var sb = new StringBuilder(coords.Length * 6);
        foreach (double c in coords) sb.Append(Math.Round(c * 1e4).ToString(CultureInfo.InvariantCulture)).Append(',');
        sb.Append('|');
        foreach (int[][] f in faces) { foreach (int[] l in f) { foreach (int i in l) sb.Append(i).Append(','); sb.Append(';'); } sb.Append('/'); }
        return sb.ToString();
    }
}

/// <summary>Operações vetoriais 3D.</summary>
internal static class Vec
{
    public static double[] Sub(double[] a, double[] b) => new[] { a[0] - b[0], a[1] - b[1], a[2] - b[2] };
    public static double[] Scale(double[] a, double f) => new[] { a[0] * f, a[1] * f, a[2] * f };
    public static double Dot(double[] a, double[] b) => a[0] * b[0] + a[1] * b[1] + a[2] * b[2];
    public static double Length(double[] a) => Math.Sqrt(Dot(a, a));
    public static double[] Cross(double[] a, double[] b) => new[] { a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0] };
    public static double[] Normalize(double[] a) { double l = Length(a); return l > 0 ? Scale(a, 1 / l) : new double[] { 0, 0, 1 }; }
    public static double[] Perpendicular(double[] w) => Math.Abs(w[0]) < 0.9 ? Cross(w, new double[] { 1, 0, 0 }) : Cross(w, new double[] { 0, 1, 0 });
    /// <summary>Componente de a perpendicular ao vetor unitário n.</summary>
    public static double[] Reject(double[] a, double[] n) => Sub(a, Scale(n, Dot(a, n)));
}
