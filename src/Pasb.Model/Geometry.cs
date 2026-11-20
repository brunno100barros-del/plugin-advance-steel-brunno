using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

namespace Pasb.Model;

/// <summary>
/// Geometria analítica de um elemento (docs/NEUTRAL-MODEL.md, seção 5), extraída das propriedades lidas.
/// Coordenadas em unidades do desenho (ModelUnits) e convertidas para metros quando a unidade é conhecida.
/// </summary>
public sealed class NeutralGeometry
{
    /// <summary>axis (viga), contour (chapa), points (parafusos/chumbadores), point (solda), extents (reserva).</summary>
    public string Kind { get; set; } = "";
    public List<double[]> Points { get; } = new();
    public double[]? Normal { get; set; }
    /// <summary>Matriz 4×4 (linhas) do sistema de coordenadas do elemento, quando disponível.</summary>
    public double[][]? CoordinateSystem { get; set; }
    /// <summary>Espessura (chapa), diâmetro (parafuso) etc., em unidades do desenho.</summary>
    public Dictionary<string, double> Dimensions { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, string> Descriptors { get; } = new(StringComparer.Ordinal);
    /// <summary>Fator das unidades do desenho para metros (nulo = unidade não comprovada).</summary>
    public double? ModelUnitToMeters { get; set; }
    /// <summary>Propriedades de origem usadas, para rastreabilidade.</summary>
    public List<string> SourceKeys { get; } = new();
    public List<string> Limitations { get; } = new();
}

public static class GeometryExtractor
{
    public static NeutralGeometry? Extract(NeutralElement e, double? unitToMeters)
    {
        Dictionary<string, NeutralProperty> p = e.Properties
            .Where(x => x.Status == PropertyStatus.Read)
            .GroupBy(x => x.Key).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        NeutralGeometry? g = e.Category switch
        {
            "beam" => Beam(p),
            "plate" => Plate(p),
            "bolts" or "anchors" => Bolts(p),
            "weld" => Weld(p),
            _ => null,
        };
        g ??= Extents(p);
        if (g is null) return null;
        g.ModelUnitToMeters = unitToMeters;
        if (Matrix(p, "ActiveConstructionElement.CS") is { } cs && g.CoordinateSystem is null)
        {
            g.CoordinateSystem = cs; g.SourceKeys.Add("ActiveConstructionElement.CS");
        }
        return g;
    }

    private static NeutralGeometry? Beam(Dictionary<string, NeutralProperty> p)
    {
        if (Point(p, "Beam.GetPointAtStart") is not { } a || Point(p, "Beam.GetPointAtEnd") is not { } b) return null;
        var g = new NeutralGeometry { Kind = "axis" };
        g.Points.Add(a); g.Points.Add(b);
        g.SourceKeys.AddRange(new[] { "Beam.GetPointAtStart", "Beam.GetPointAtEnd" });
        if (Matrix(p, "Beam.PhysCSStart") is { } cs) { g.CoordinateSystem = cs; g.SourceKeys.Add("Beam.PhysCSStart"); }
        Desc(g, p, "Beam.ProfSectionType", "profileType");
        Desc(g, p, "Beam.ProfSectionName", "profileName");
        Num(g, p, "Beam.SysLength", "systemLength");
        Num(g, p, "Beam.GetLength", "physicalLength");

        // Recortes nas pontas (GetLengthOffset): comprimento físico = sistema − início − fim (Teste 3: 1500 − 2×34,5 = 1431).
        if (p.TryGetValue("Beam.GetLengthOffset", out NeutralProperty? lo) && lo.RawValue is JsonObject loj)
        {
            if (TryNum(loj["dStart"], out double ds)) g.Dimensions["offsetStart"] = ds;
            if (TryNum(loj["dEnd"], out double de)) g.Dimensions["offsetEnd"] = de;
            g.SourceKeys.Add(lo.Key);
        }

        // Seção: ProfileType serializado pelo diagnóstico 0.2.3 (propriedades do catálogo, ex.: H, B, Tw, Tf, R1, R2).
        if (p.TryGetValue("Beam.GetProfType", out NeutralProperty? pt) && pt.RawValue is JsonObject ptj && ptj["properties"] is JsonObject props)
        {
            if (props["Type"] is JsonObject t && t["name"] is JsonValue tn && tn.TryGetValue(out string? shape) && shape is not null)
                g.Descriptors["sectionShape"] = shape;
            foreach (string k in new[] { "H", "B", "Tw", "Tf", "R1", "R2", "E" })
                if (TryNum(props[k], out double d)) g.Dimensions["section." + k] = d;
            g.SourceKeys.Add(pt.Key);
        }
        // Cortes de serra (graus) e espelhamento da seção: usados para os chanfros das pontas (Teste 10).
        if (p.TryGetValue("Beam.GetSawInformation", out NeutralProperty? saw) && saw.RawValue is JsonObject sj)
        {
            foreach (string k in new[] { "flangeAngleAtStart", "flangeAngleAtEnd", "webAngleAtStart", "webAngleAtEnd" })
                if (TryNum(sj[k], out double ang)) g.Dimensions["saw." + k] = ang;
            g.SourceKeys.Add(saw.Key);
        }
        if (p.TryGetValue("Beam.IsCrossSectionMirrored", out NeutralProperty? mir) && mir.RawValue is JsonValue mv && mv.TryGetValue(out bool mirrored))
        {
            g.Descriptors["mirrored"] = mirrored ? "true" : "false";
            g.SourceKeys.Add(mir.Key);
        }
        if (!g.Dimensions.ContainsKey("section.H"))
            g.Limitations.Add("Seção sem dimensões (perfil composto ou tipo não serializado): corpo sólido indisponível.");
        return g;
    }

    private static NeutralGeometry? Plate(Dictionary<string, NeutralProperty> p)
    {
        if (!p.TryGetValue("PlateBase.GetBaseContourPolygon(0)", out NeutralProperty? c) || c.RawValue is not JsonArray arr) return null;
        var g = new NeutralGeometry { Kind = "contour" };
        foreach (JsonNode? v in arr) if (ToVector(v) is { } pt) g.Points.Add(pt);
        g.SourceKeys.Add(c.Key);
        if (Vector(p, "PlateBase.PlateNormal") is { } n) { g.Normal = n; g.SourceKeys.Add("PlateBase.PlateNormal"); }
        Num(g, p, "PlateBase.Thickness", "thickness");
        Num(g, p, "PlateBase.Portioning", "portioning");
        g.Limitations.Add("Contorno externo apenas (índice 0); furos e recortes internos não incluídos.");
        return g;
    }

    private static NeutralGeometry? Bolts(Dictionary<string, NeutralProperty> p)
    {
        if (!p.TryGetValue("BoltPattern.GetMidpoints", out NeutralProperty? m) || m.RawValue is not JsonArray arr) return null;
        var g = new NeutralGeometry { Kind = "points" };
        foreach (JsonNode? v in arr) if (ToVector(v) is { } pt) g.Points.Add(pt);
        g.SourceKeys.Add(m.Key);
        if (Vector(p, "BoltPattern.Normal") is { } n) { g.Normal = n; g.SourceKeys.Add("BoltPattern.Normal"); }
        Num(g, p, "ScrewBoltPattern.ScrewDiameter", "diameter");
        Num(g, p, "ScrewBoltPattern.ScrewLength", "length");
        Num(g, p, "ScrewBoltPattern.BoltHeadDiameter", "headDiameter");
        Num(g, p, "ScrewBoltPattern.BoltHeadHeight", "headHeight");
        Num(g, p, "ScrewBoltPattern.NutDiameter", "nutDiameter");
        Num(g, p, "ScrewBoltPattern.NutHeight", "nutHeight");
        return g;
    }

    private static NeutralGeometry? Weld(Dictionary<string, NeutralProperty> p)
    {
        if (Point(p, "WeldPattern.GetCenterPoint") is not { } c) return null;
        var g = new NeutralGeometry { Kind = "point" };
        g.Points.Add(c); g.SourceKeys.Add("WeldPattern.GetCenterPoint");
        Num(g, p, "WeldPattern.Length", "length");
        Num(g, p, "WeldPattern.Thickness", "thickness");
        g.Limitations.Add("Trajetória do cordão não extraída; só o ponto central.");
        return g;
    }

    private static NeutralGeometry? Extents(Dictionary<string, NeutralProperty> p)
    {
        if (!p.TryGetValue("ConstructionElement.GeomExtents", out NeutralProperty? x) || x.RawValue is not JsonObject o) return null;
        if (ToVector(o["min"]) is not { } min || ToVector(o["max"]) is not { } max) return null;
        var g = new NeutralGeometry { Kind = "extents" };
        g.Points.Add(min); g.Points.Add(max); g.SourceKeys.Add(x.Key);
        g.Limitations.Add("Caixa envolvente alinhada aos eixos globais (aproximação).");
        return g;
    }

    private static void Num(NeutralGeometry g, Dictionary<string, NeutralProperty> p, string key, string name)
    {
        if (p.TryGetValue(key, out NeutralProperty? v) && v.RawValue is JsonValue jv && jv.TryGetValue(out double d))
        { g.Dimensions[name] = d; g.SourceKeys.Add(key); }
    }

    private static void Desc(NeutralGeometry g, Dictionary<string, NeutralProperty> p, string key, string name)
    {
        if (p.TryGetValue(key, out NeutralProperty? v) && v.RawValue is JsonValue jv && jv.TryGetValue(out string? s) && s is not null)
        { g.Descriptors[name] = s; g.SourceKeys.Add(key); }
    }

    private static double[]? Point(Dictionary<string, NeutralProperty> p, string key) =>
        p.TryGetValue(key, out NeutralProperty? v) ? ToVector(v.RawValue) : null;

    private static double[]? Vector(Dictionary<string, NeutralProperty> p, string key) => Point(p, key);

    private static double[][]? Matrix(Dictionary<string, NeutralProperty> p, string key)
    {
        if (!p.TryGetValue(key, out NeutralProperty? v) || v.RawValue is not JsonArray rows || rows.Count != 4) return null;
        var m = new double[4][];
        for (int i = 0; i < 4; i++)
        {
            if (rows[i] is not JsonArray r || r.Count != 4) return null;
            m[i] = new double[4];
            for (int j = 0; j < 4; j++) if (!TryNum(r[j], out m[i][j])) return null;
        }
        return m;
    }

    internal static double[]? ToVector(JsonNode? node)
    {
        if (node is not JsonArray a || a.Count is < 2 or > 3) return null;
        var v = new double[3];
        for (int i = 0; i < a.Count; i++) if (!TryNum(a[i], out v[i])) return null;
        return v;
    }

    private static bool TryNum(JsonNode? n, out double d)
    {
        d = 0;
        return n is JsonValue v && v.TryGetValue(out d) && double.IsFinite(d);
    }
}
