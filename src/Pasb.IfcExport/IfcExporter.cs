using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Pasb.Georeferencing;
using Pasb.Model;
using static Pasb.IfcExport.StepWriter;

namespace Pasb.IfcExport;

/// <summary>Relatório de exportação (IFC-EXPORTER.md, "Validação de saída").</summary>
public sealed class ExportReport
{
    public Dictionary<string, int> ExportedByIfcClass { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, int> SkippedByCategory { get; } = new(StringComparer.Ordinal);
    public int PropertySets { get; set; }
    public int Properties { get; set; }
    public int PropertiesWithoutValue { get; set; }
    public int GroupsFromJoints { get; set; }
    public List<string> Limitations { get; } = new();
    public List<string> Warnings { get; } = new();
    public JsonObject? Georeference { get; set; }
    public string GlobalIdPolicy { get; set; } = "";
}

public sealed record ExportOptions(string ProjectName = "Projeto Advance Steel", string FileName = "modelo.ifc", GeoParameters? Geo = null);

/// <summary>
/// Exportador IFC4 a partir do modelo neutro. Regras:
/// - Geometria em metros; posições absolutas no sistema do modelo; georreferenciamento por IfcMapConversion.
/// - Todas as propriedades vão para Psets "AS_&lt;ClasseDeclaradora&gt;" com o nome técnico original; nada é descartado.
/// - Nenhuma geometria é inventada: sem dados suficientes, o elemento sai sem corpo e a limitação vai para o relatório.
/// </summary>
public static class IfcExporter
{
    public static string Export(Extraction x, ExportOptions opt, out ExportReport report)
    {
        var r = new ExportReport();
        report = r;
        var w = new StepWriter();
        double? k = x.Elements.Select(e => e.Geometry?.ModelUnitToMeters).FirstOrDefault(v => v.HasValue);
        if (k is null) r.Warnings.Add("Unidade do desenho não comprovada: geometria não exportada.");

        // ---------- Contexto, unidades, projeto
        int origin = w.Add("IfcCartesianPoint", Point(0, 0, 0));
        int zAxis = w.Add("IfcDirection", Point(0, 0, 1));
        int xAxis = w.Add("IfcDirection", Point(1, 0, 0));
        int wcs = w.Add("IfcAxis2Placement3D", Ref(origin), Ref(zAxis), Ref(xAxis));
        int ctx = w.Add("IfcGeometricRepresentationContext", Null, Str("Model"), "3", Real(1e-5), Ref(wcs), Null);
        int body = w.Add("IfcGeometricRepresentationSubContext", Str("Body"), Str("Model"), Derived, Derived, Derived, Derived, Ref(ctx), Null, Enum("MODEL_VIEW"), Null);
        int axisCtx = w.Add("IfcGeometricRepresentationSubContext", Str("Axis"), Str("Model"), Derived, Derived, Derived, Derived, Ref(ctx), Null, Enum("GRAPH_VIEW"), Null);
        int metre = w.Add("IfcSIUnit", Derived, Enum("LENGTHUNIT"), Null, Enum("METRE"));
        int area = w.Add("IfcSIUnit", Derived, Enum("AREAUNIT"), Null, Enum("SQUARE_METRE"));
        int volume = w.Add("IfcSIUnit", Derived, Enum("VOLUMEUNIT"), Null, Enum("CUBIC_METRE"));
        int kg = w.Add("IfcSIUnit", Derived, Enum("MASSUNIT"), Enum("KILO"), Enum("GRAM"));
        int rad = w.Add("IfcSIUnit", Derived, Enum("PLANEANGLEUNIT"), Null, Enum("RADIAN"));
        int units = w.Add("IfcUnitAssignment", Refs(new[] { metre, area, volume, kg, rad }));
        int project = w.Add("IfcProject", Str(Gid("project")), Null, Str(opt.ProjectName), Null, Null, Null, Null, "(" + Ref(ctx) + ")", Ref(units));

        // ---------- Georreferenciamento (IfcProjectedCRS + IfcMapConversion)
        if (opt.Geo is { } geo)
        {
            List<string> errors = Georeferencing.Georeference.Validate(geo);
            if (errors.Count > 0) throw new ArgumentException("Parâmetros de georreferenciamento inválidos: " + string.Join("; ", errors));
            ProjectedCrs crs = Georeferencing.Georeference.ToIfcProjectedCrs(geo);
            MapConversion mc = Georeferencing.Georeference.ToIfcMapConversion(geo);
            int crsId = w.Add("IfcProjectedCRS", Str(crs.Name), Str(crs.Description), Str(crs.GeodeticDatum), Null, Str(crs.MapProjection), Str(crs.MapZone), Ref(metre));
            w.Add("IfcMapConversion", Ref(ctx), Ref(crsId), Real(mc.Eastings), Real(mc.Northings), Real(mc.OrthogonalHeight),
                Real(mc.XAxisAbscissa), Real(mc.XAxisOrdinate), Real(mc.Scale));
            r.Georeference = new JsonObject
            {
                ["mode"] = geo.Mode.ToString(), ["crs"] = crs.Name, ["zone"] = crs.MapZone,
                ["eastings"] = mc.Eastings, ["northings"] = mc.Northings, ["orthogonalHeight"] = mc.OrthogonalHeight,
                ["rotationDegrees"] = geo.RotationDegrees, ["xAxisAbscissa"] = mc.XAxisAbscissa, ["xAxisOrdinate"] = mc.XAxisOrdinate,
            };
        }
        else r.Warnings.Add("Sem parâmetros de georreferenciamento: IFC em coordenadas do modelo.");

        // ---------- Estrutura espacial
        int sitePl = w.Add("IfcLocalPlacement", Null, Ref(wcs));
        int site = w.Add("IfcSite", Str(Gid("site")), Null, Str("Site"), Null, Null, Ref(sitePl), Null, Null, Enum("ELEMENT"), Null, Null, Null, Null, Null);
        int bldPl = w.Add("IfcLocalPlacement", Ref(sitePl), Ref(wcs));
        int building = w.Add("IfcBuilding", Str(Gid("building")), Null, Str("Building"), Null, Null, Ref(bldPl), Null, Null, Enum("ELEMENT"), Null, Null, Null);
        int stPl = w.Add("IfcLocalPlacement", Ref(bldPl), Ref(wcs));
        int storey = w.Add("IfcBuildingStorey", Str(Gid("storey")), Null, Str("Estrutura"), Null, Null, Ref(stPl), Null, Null, Enum("ELEMENT"), Real(0));
        w.Add("IfcRelAggregates", Str(Gid("agg-project")), Null, Null, Null, Ref(project), "(" + Ref(site) + ")");
        w.Add("IfcRelAggregates", Str(Gid("agg-site")), Null, Null, Null, Ref(site), "(" + Ref(building) + ")");
        w.Add("IfcRelAggregates", Str(Gid("agg-building")), Null, Null, Null, Ref(building), "(" + Ref(storey) + ")");
        int elemPl = w.Add("IfcLocalPlacement", Ref(stPl), Ref(wcs));

        // ---------- Elementos
        var contained = new List<int>();
        var byHandle = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var groups = new List<(NeutralElement e, int id)>();
        r.GlobalIdPolicy = "GlobalId derivado do GUID do Advance Steel (GetUniqueId) quando existe; senão, determinístico a partir do handle. " +
                           "A persistência do GetUniqueId entre sessões ainda não foi comprovada.";

        foreach (NeutralElement e in x.Elements)
        {
            string gid = ElementGid(e);
            string name = NameOf(e);
            int id;
            switch (e.Category)
            {
                case "beam":
                    id = w.Add("IfcBeam", Str(gid), Null, Str(name), Null, Str(e.SourceType), Ref(elemPl), Shape(w, e, k, body, axisCtx, r), Str(e.SourceHandle), Enum("BEAM"));
                    break;
                case "plate":
                    id = w.Add("IfcPlate", Str(gid), Null, Str(name), Null, Str(e.SourceType), Ref(elemPl), Shape(w, e, k, body, axisCtx, r), Str(e.SourceHandle), Enum("SHEET"));
                    break;
                case "bolts":
                case "anchors":
                {
                    double? d = Dim(e, "diameter", k), l = Dim(e, "length", k);
                    id = w.Add("IfcMechanicalFastener", Str(gid), Null, Str(name), Null, Str(e.SourceType), Ref(elemPl), Shape(w, e, k, body, axisCtx, r), Str(e.SourceHandle),
                        d is null ? Null : Real(d.Value), l is null ? Null : Real(l.Value), Enum(e.Category == "anchors" ? "ANCHORBOLT" : "BOLT"));
                    break;
                }
                case "weld":
                    id = w.Add("IfcFastener", Str(gid), Null, Str(name), Null, Str(e.SourceType), Ref(elemPl), Null, Str(e.SourceHandle), Enum("WELD"));
                    Note(r, "Soldas exportadas sem corpo (só propriedades e posição no relatório).");
                    break;
                case "joint":
                case "connection":
                case "joint_parameters":
                    id = w.Add("IfcGroup", Str(gid), Null, Str(name), Str("Advance Steel " + e.Category), Str(e.SourceType));
                    groups.Add((e, id));
                    r.GroupsFromJoints++;
                    break;
                case "internal":
                    Count(r.SkippedByCategory, e.Category);
                    continue;
                default:
                    id = w.Add("IfcBuildingElementProxy", Str(gid), Null, Str(name), Null, Str(e.SourceType), Ref(elemPl), Null, Str(e.SourceHandle), Enum("NOTDEFINED"));
                    Note(r, $"Categoria '{e.Category}' exportada como IfcBuildingElementProxy, sem corpo.");
                    break;
            }
            if (e.SourceHandle is not null) byHandle[e.SourceHandle] = id;
            if (e.Category is not ("joint" or "connection" or "joint_parameters")) contained.Add(id);
            Count(r.ExportedByIfcClass, IfcClassOf(e.Category));
            Psets(w, e, id, gid, metre, kg, area, volume, r);
        }

        if (contained.Count > 0)
            w.Add("IfcRelContainedInSpatialStructure", Str(Gid("contained")), Null, Null, Null, Refs(contained), Ref(storey));

        // Ligações: membros = todos os handles referenciados pelas relações do elemento que foram exportados.
        foreach (var (e, gidRef) in groups)
        {
            var members = e.Relationships.Select(rel => byHandle.TryGetValue(rel.TargetHandle, out int m) ? m : 0)
                .Where(m => m != 0 && m != gidRef).Distinct().ToList();
            if (members.Count > 0)
                w.Add("IfcRelAssignsToGroup", Str(Gid("grp-" + e.SourceHandle)), Null, Null, Null, Refs(members), Null, Ref(gidRef));
        }

        foreach (NeutralElement e in x.Elements)
            foreach (string lim in e.Geometry?.Limitations ?? new List<string>())
                Note(r, $"{e.Category}: {lim}");
        foreach (string wmsg in x.Info.Warnings) r.Warnings.Add(wmsg);

        return w.Build(opt.FileName, x.Info.StartedUtc ?? DateTime.UtcNow.ToString("s"));
    }

    // ------------------------------------------------------------------ geometria

    private static string Shape(StepWriter w, NeutralElement e, double? k, int bodyCtx, int axisCtx, ExportReport r)
    {
        NeutralGeometry? g = e.Geometry;
        if (g is null || k is null) return Null;
        double s = k.Value;
        double[] P(double[] p) => new[] { p[0] * s, p[1] * s, p[2] * s };
        int Pt(double[] p) => w.Add("IfcCartesianPoint", Point(P(p)));

        switch (g.Kind)
        {
            case "axis" when g.Points.Count >= 2:
            {
                int pl = w.Add("IfcPolyline", Refs(g.Points.Select(Pt)));
                int rep = w.Add("IfcShapeRepresentation", Ref(axisCtx), Str("Axis"), Str("Curve3D"), "(" + Ref(pl) + ")");
                return Ref(w.Add("IfcProductDefinitionShape", Null, Null, "(" + Ref(rep) + ")"));
            }
            case "contour" when g.Points.Count >= 3 && g.Normal is { } n && g.Dimensions.TryGetValue("thickness", out double t):
            {
                double[] p0 = g.Points[0];
                double[] u = Normalize(Sub(g.Points[1], p0));
                double[] wv = Normalize(n);
                double[] v = Cross(wv, u);
                var pts2d = g.Points.Select(p => { double[] d = Sub(p, p0); return w.Add("IfcCartesianPoint", Point(Dot(d, u) * s, Dot(d, v) * s)); }).ToList();
                pts2d.Add(pts2d[0]);
                int pl = w.Add("IfcPolyline", Refs(pts2d));
                int prof = w.Add("IfcArbitraryClosedProfileDef", Enum("AREA"), Null, Ref(pl));
                int pos = w.Add("IfcAxis2Placement3D", Ref(Pt(p0)), Ref(w.Add("IfcDirection", Point(wv))), Ref(w.Add("IfcDirection", Point(u))));
                int dir = w.Add("IfcDirection", Point(0, 0, 1));
                int solid = w.Add("IfcExtrudedAreaSolid", Ref(prof), Ref(pos), Ref(dir), Real(t * s));
                Note(r, "plate: extrusão no sentido da normal; o deslocamento por 'Portioning' ainda não é aplicado.");
                int rep = w.Add("IfcShapeRepresentation", Ref(bodyCtx), Str("Body"), Str("SweptSolid"), "(" + Ref(solid) + ")");
                return Ref(w.Add("IfcProductDefinitionShape", Null, Null, "(" + Ref(rep) + ")"));
            }
            case "points" when g.Points.Count > 0 && g.Normal is { } n
                               && g.Dimensions.TryGetValue("diameter", out double dia) && g.Dimensions.TryGetValue("length", out double len):
            {
                double[] wv = Normalize(n);
                double[] u = Normalize(Perpendicular(wv));
                int c2 = w.Add("IfcAxis2Placement2D", Ref(w.Add("IfcCartesianPoint", Point(0, 0))), Null);
                int circle = w.Add("IfcCircleProfileDef", Enum("AREA"), Null, Ref(c2), Real(dia * s / 2));
                int dirZ = w.Add("IfcDirection", Point(0, 0, 1));
                int axisDir = w.Add("IfcDirection", Point(wv));
                int refDir = w.Add("IfcDirection", Point(u));
                var solids = new List<int>();
                foreach (double[] m in g.Points)
                {
                    double[] start = Sub(m, Scale(wv, len / 2));
                    int pos = w.Add("IfcAxis2Placement3D", Ref(Pt(start)), Ref(axisDir), Ref(refDir));
                    solids.Add(w.Add("IfcExtrudedAreaSolid", Ref(circle), Ref(pos), Ref(dirZ), Real(len * s)));
                }
                Note(r, "bolts: corpo simplificado como cilindro (Ø nominal × comprimento), centrado no ponto médio; cabeça e porca não modeladas.");
                int rep = w.Add("IfcShapeRepresentation", Ref(bodyCtx), Str("Body"), Str("SweptSolid"), Refs(solids));
                return Ref(w.Add("IfcProductDefinitionShape", Null, Null, "(" + Ref(rep) + ")"));
            }
            default:
                return Null;
        }
    }

    private static double? Dim(NeutralElement e, string name, double? k) =>
        k is not null && e.Geometry?.Dimensions.TryGetValue(name, out double d) == true ? d * k.Value : null;

    private static double[] Sub(double[] a, double[] b) => new[] { a[0] - b[0], a[1] - b[1], a[2] - b[2] };
    private static double[] Scale(double[] a, double f) => new[] { a[0] * f, a[1] * f, a[2] * f };
    private static double Dot(double[] a, double[] b) => a[0] * b[0] + a[1] * b[1] + a[2] * b[2];
    private static double[] Cross(double[] a, double[] b) => new[] { a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0] };
    private static double[] Normalize(double[] a) { double l = Math.Sqrt(Dot(a, a)); return l > 0 ? Scale(a, 1 / l) : new double[] { 0, 0, 1 }; }
    private static double[] Perpendicular(double[] w) => Math.Abs(w[0]) < 0.9 ? Cross(w, new double[] { 1, 0, 0 }) : Cross(w, new double[] { 0, 1, 0 });

    // ------------------------------------------------------------------ propriedades

    private static void Psets(StepWriter w, NeutralElement e, int elementId, string gid, int metre, int kg, int area, int volume, ExportReport r)
    {
        // Identificação de origem também vira propriedade (rastreabilidade).
        var groupsByClass = e.Properties.GroupBy(p => p.DeclaringType).OrderBy(g => g.Key, StringComparer.Ordinal);
        var psetIds = new List<int>();
        var origin = new List<int>
        {
            w.Add("IfcPropertySingleValue", Str("SourceHandle"), Null, Typed("IfcIdentifier", e.SourceHandle), Null),
            w.Add("IfcPropertySingleValue", Str("SourceType"), Null, Typed("IfcIdentifier", e.SourceType), Null),
            w.Add("IfcPropertySingleValue", Str("SourceUniqueId"), Null, Typed("IfcIdentifier", e.SourceUniqueId), Null),
            w.Add("IfcPropertySingleValue", Str("Category"), Null, Typed("IfcLabel", e.Category), Null),
            w.Add("IfcPropertySingleValue", Str("NeutralStatus"), Null, Typed("IfcLabel", e.Status.ToString()), Null),
        };
        psetIds.Add(w.Add("IfcPropertySet", Str(IfcGuid.Compress(IfcGuid.FromName(gid + "/AS_Origin"))), Null, Str("AS_Origin"), Null, Refs(origin)));
        r.Properties += origin.Count;

        foreach (var g in groupsByClass)
        {
            var props = new List<int>();
            foreach (NeutralProperty p in g)
            {
                string pname = p.TechnicalName + (p.Argument is null ? "" : "(" + p.Argument + ")");
                string desc = p.Status + (p.Unit is null ? "" : "; unidade: " + p.Unit) + (p.Error is null ? "" : "; " + p.Error);
                string value = ValueOf(p);
                if (value == Null) r.PropertiesWithoutValue++;
                props.Add(w.Add("IfcPropertySingleValue", Str(pname), Str(desc), value, Null));
                if (p.NormalizedValue is double nv && double.IsFinite(nv) && UnitRef(p.NormalizedUnit, metre, kg, area, volume) is int u)
                    props.Add(w.Add("IfcPropertySingleValue", Str(pname + " [SI]"), Str("Normalizado a partir de " + p.Unit), Typed("IfcReal", nv), Ref(u)));
            }
            r.Properties += props.Count;
            string psetName = "AS_" + g.Key;
            psetIds.Add(w.Add("IfcPropertySet", Str(IfcGuid.Compress(IfcGuid.FromName(gid + "/" + psetName))), Null, Str(psetName), Null, Refs(props)));
        }
        r.PropertySets += psetIds.Count;
        foreach (int ps in psetIds)
            w.Add("IfcRelDefinesByProperties", Str(IfcGuid.Compress(IfcGuid.FromName(gid + "/rel/" + ps))), Null, Null, Null, "(" + Ref(elementId) + ")", Ref(ps));
    }

    private static int? UnitRef(string? unit, int metre, int kg, int area, int volume) => unit switch
    {
        "m" => metre, "kg" => kg, "m2" => area, "m3" => volume, _ => null,
    };

    /// <summary>Valor IFC tipado, sem perda: texto, número, booleano; estruturas como JSON em IfcText.</summary>
    private static string ValueOf(NeutralProperty p)
    {
        if (p.Status is PropertyStatus.Null or PropertyStatus.Unavailable or PropertyStatus.ReadError || p.RawValue is null) return Null;
        switch (p.RawValue)
        {
            case JsonValue v when v.TryGetValue(out bool b): return "IFCBOOLEAN(" + Bool(b) + ")";
            case JsonValue v when v.GetValueKind() == JsonValueKind.Number && v.TryGetValue(out long l) && !v.ToJsonString().Contains('.'):
                return "IFCINTEGER(" + Int(l) + ")";
            case JsonValue v when v.TryGetValue(out double d) && double.IsFinite(d): return Typed("IfcReal", d);
            case JsonValue v when v.TryGetValue(out string? s): return Typed("IfcText", s);
            case JsonObject o when o.Count == 2 && o["name"] is JsonValue nm && o["value"] is JsonValue:
                return Typed("IfcLabel", nm.ToString() + " (" + o["value"]!.ToJsonString() + ")");
            default: return Typed("IfcText", p.RawValue.ToJsonString());
        }
    }

    private static string Typed(string ifcType, string? s) => s is null ? Null : ifcType.ToUpperInvariant() + "(" + Str(s) + ")";
    private static string Typed(string ifcType, double d) => ifcType.ToUpperInvariant() + "(" + Real(d) + ")";

    // ------------------------------------------------------------------ utilidades

    private static string IfcClassOf(string category) => category switch
    {
        "beam" => "IfcBeam", "plate" => "IfcPlate", "bolts" or "anchors" => "IfcMechanicalFastener", "weld" => "IfcFastener",
        "joint" or "connection" or "joint_parameters" => "IfcGroup", _ => "IfcBuildingElementProxy",
    };

    private static string ElementGid(NeutralElement e) =>
        IfcGuid.Compress(Guid.TryParse(e.SourceUniqueId, out Guid g) ? g : IfcGuid.FromName("AS:" + e.SourceHandle + ":" + e.InternalId));

    private static string Gid(string name) => IfcGuid.Compress(IfcGuid.FromName("PASB:" + name));

    private static string NameOf(NeutralElement e)
    {
        string? Prop(string key) => e.Properties.FirstOrDefault(p => p.Key == key && p.Status == PropertyStatus.Read)?.RawValue?.ToString();
        string? n = Prop("AtomicElement.Denotation") ?? Prop("Beam.ProfSectionName");
        return string.IsNullOrWhiteSpace(n) ? $"{e.SourceType} {e.SourceHandle}" : $"{n} {e.SourceHandle}";
    }

    private static void Count(Dictionary<string, int> d, string key) => d[key] = d.GetValueOrDefault(key) + 1;

    private static void Note(ExportReport r, string msg) { if (!r.Limitations.Contains(msg)) r.Limitations.Add(msg); }
}
