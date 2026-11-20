using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Pasb.IfcImport.Model;
using Xbim.Common;
using Xbim.Common.Geometry;
using Xbim.Common.XbimExtensions;
using Xbim.Ifc;
using Xbim.Ifc4.Interfaces;
using Xbim.ModelGeometry.Scene;

namespace Pasb.IfcImport;

/// <summary>
/// Pasb.IfcImport.exe — leitura de IFC com a xBIM, fora do AutoCAD.
///   scan    &lt;arquivo.ifc&gt; &lt;saida.json&gt;      tipos, níveis e layers (rápido, sem geometria)
///   convert &lt;pedido.json&gt; &lt;saida.pasbmesh&gt;  malhas trianguladas dos elementos escolhidos
/// Código de saída 0 = ok; 1 = erro (mensagem em stderr). Linhas "PROGRESSO n" em stdout.
/// </summary>
internal static class Program
{
    private static int Main(string[] args)
    {
        Console.OutputEncoding = new UTF8Encoding(false);
        try
        {
            if (args.Length == 3 && args[0] == "scan") { File.WriteAllText(args[2], ScanJson.Serialize(Reader.Scan(args[1])), new UTF8Encoding(false)); return 0; }
            if (args.Length == 3 && args[0] == "convert")
            {
                var req = JsonSerializer.Deserialize<ConvertRequest>(File.ReadAllText(args[1]), ScanJson.Options) ?? throw new InvalidDataException("Pedido vazio.");
                MeshPackage p = Reader.Convert(req, pct => Console.WriteLine("PROGRESSO " + pct));
                using var fs = File.Create(args[2]);
                p.Write(fs);
                return 0;
            }
            if (args.Length == 1 && args[0] == "--version") { Console.WriteLine("Pasb.IfcImport " + typeof(Program).Assembly.GetName().Version); return 0; }
            Console.Error.WriteLine("Uso: Pasb.IfcImport scan <arquivo.ifc> <saida.json> | convert <pedido.json> <saida.pasbmesh>");
            return 2;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.GetType().Name + ": " + ex.Message);
            return 1;
        }
    }
}

internal static class Reader
{
    private static readonly HashSet<string> Excluded = new(StringComparer.Ordinal) { "IfcOpeningElement", "IfcSpace" };

    public static IfcScan Scan(string file)
    {
        using IfcStore model = IfcStore.Open(file);
        var scan = new IfcScan
        {
            File = file,
            Schema = model.SchemaVersion.ToString(),
            ProjectName = model.Instances.FirstOrDefault<IIfcProject>()?.Name,
            UnitsPerMetre = model.ModelFactors.OneMetre,
        };
        var products = model.Instances.OfType<IIfcProduct>().Where(p => p.Representation != null && p is not IIfcSpatialStructureElement || p is IIfcSpace).ToList();
        foreach (var g in products.GroupBy(p => p.ExpressType.Name).OrderBy(g => g.Key, StringComparer.Ordinal))
            scan.Types.Add(new ScanType { Name = g.Key, Count = g.Count() });

        var counts = products.GroupBy(StoreyKey).ToDictionary(g => g.Key, g => g.Count());
        foreach (IIfcBuildingStorey s in model.Instances.OfType<IIfcBuildingStorey>().OrderBy(s => (double?)s.Elevation ?? double.MaxValue))
            scan.Storeys.Add(new ScanStorey
            {
                Key = s.GlobalId.ToString(), Name = (string?)s.Name ?? "(nível sem nome)",
                Elevation = s.Elevation.HasValue ? (double)s.Elevation.Value : null,
                Count = counts.GetValueOrDefault(s.GlobalId.ToString()),
            });
        if (counts.TryGetValue(MeshInstance.NoStorey, out int loose))
            scan.Storeys.Add(new ScanStorey { Key = MeshInstance.NoStorey, Name = "Sem nível", Count = loose });

        scan.Layers = model.Instances.OfType<IIfcPresentationLayerAssignment>().Select(l => (string)l.Name).Where(n => !string.IsNullOrWhiteSpace(n)).Distinct().OrderBy(n => n).ToList();
        if (scan.Storeys.Count == 0) scan.Warnings.Add("O IFC não tem níveis (IfcBuildingStorey).");
        return scan;
    }

    public static MeshPackage Convert(ConvertRequest req, Action<int> progress)
    {
        using IfcStore model = IfcStore.Open(req.IfcFile);
        progress(10);
        var pkg = new MeshPackage();
        var types = new HashSet<string>(req.Types, StringComparer.Ordinal);
        var storeys = new HashSet<string>(req.Storeys, StringComparer.Ordinal);
        double k = 1.0 / model.ModelFactors.OneMetre;   // unidades do IFC → metros

        var ctx = new Xbim3DModelContext(model);
        ctx.CreateContext(null, true);
        progress(60);

        var geomIds = new Dictionary<int, int>();
        var instances = ctx.ShapeInstances().Where(s => s.RepresentationType != XbimGeometryRepresentationType.OpeningsAndAdditionsExcluded).ToList();
        int done = 0, failed = 0;
        foreach (XbimShapeInstance si in instances)
        {
            if (++done % 200 == 0) progress(60 + 35 * done / Math.Max(1, instances.Count));
            if (model.Instances[si.IfcProductLabel] is not IIfcProduct p) continue;
            string type = p.ExpressType.Name;
            if (type == "IfcOpeningElement" && !req.IncludeOpenings) continue;
            if (type == "IfcSpace" && !req.IncludeSpaces) continue;
            if (types.Count > 0 && !types.Contains(type)) continue;
            string storey = StoreyKey(p);
            if (storeys.Count > 0 && !storeys.Contains(storey)) continue;

            if (!geomIds.TryGetValue(si.ShapeGeometryLabel, out int gid))
            {
                try
                {
                    IXbimShapeGeometryData g = ctx.ShapeGeometry(si.ShapeGeometryLabel);
                    XbimShapeTriangulation tri = new BinaryReader(new MemoryStream(((IXbimShapeGeometryData)g).ShapeData)).ReadShapeTriangulation();
                    var mg = new MeshGeometry { Id = pkg.Geometries.Count + 1, ReferenceCount = g.ReferenceCount };
                    mg.Vertices = tri.Vertices.SelectMany(v => new[] { v.X * k, v.Y * k, v.Z * k }).ToArray();
                    mg.Triangles = tri.Faces.SelectMany(f => f.Indices).ToArray();
                    if (mg.TriangleCount == 0) { failed++; continue; }
                    pkg.Geometries.Add(mg);
                    gid = mg.Id;
                    geomIds[si.ShapeGeometryLabel] = gid;
                }
                catch (Exception ex) { failed++; pkg.Warnings.Add($"{type} {p.GlobalId}: geometria não lida ({ex.Message})."); continue; }
            }

            XbimMatrix3D m = si.Transformation;
            var inst = new MeshInstance
            {
                GeometryId = gid,
                Transform = new[] { m.M11, m.M12, m.M13, m.M14, m.M21, m.M22, m.M23, m.M24, m.M31, m.M32, m.M33, m.M34, m.OffsetX * k, m.OffsetY * k, m.OffsetZ * k, m.M44 },
                IfcType = type,
                Name = p.Name,
                GlobalId = p.GlobalId.ToString(),
                StoreyKey = storey,
                IfcLayer = p.Representation?.Representations.SelectMany(r => r.LayerAssignments).Select(l => (string)l.Name).FirstOrDefault(n => !string.IsNullOrWhiteSpace(n)),
            };
            (inst.Color, inst.Transparency) = StyleColor(model, si.StyleLabel);
            pkg.Instances.Add(inst);
        }
        if (failed > 0) pkg.Warnings.Insert(0, $"{failed} geometria(s) sem triângulos ou com erro de leitura.");
        progress(100);
        return pkg;
    }

    /// <summary>Nível do elemento: contenção espacial, agregação (peças de um conjunto), hospedeiro da abertura ou espaço.</summary>
    internal static string StoreyKey(IIfcProduct p)
    {
        IIfcObjectDefinition? cur = p;
        for (int guard = 0; cur != null && guard < 32; guard++)
        {
            if (cur is IIfcBuildingStorey st) return st.GlobalId.ToString();
            IIfcObjectDefinition? next = null;
            // No IFC4 qualquer produto pode estar contido num elemento espacial (inclusive IfcSpace).
            if (cur is IIfcProduct prod) next = prod.IsContainedIn;
            if (next == null && cur is IIfcFeatureElementSubtraction op) next = op.VoidsElements?.RelatingBuildingElement;
            next ??= cur.Decomposes.Select(r => r.RelatingObject).FirstOrDefault();
            cur = next;
        }
        return MeshInstance.NoStorey;
    }

    private static (byte[]? rgb, double transparency) StyleColor(IModel model, int styleLabel)
    {
        if (styleLabel <= 0) return (null, 0);
        if (model.Instances[styleLabel] is not IIfcSurfaceStyle ss) return (null, 0);
        IIfcSurfaceStyleShading? sh = ss.Styles.OfType<IIfcSurfaceStyleShading>().FirstOrDefault();
        if (sh?.SurfaceColour is not { } c) return (null, 0);
        static byte B(double v) => (byte)Math.Clamp(Math.Round(v * 255), 0, 255);
        double t = sh.Transparency is { } tr ? (double)tr : 0;
        return (new[] { B(c.Red), B(c.Green), B(c.Blue) }, t);
    }
}
