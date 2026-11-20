using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using Pasb.Model;

namespace Pasb.Connector;

/// <summary>Tipos de tabela da lista de material.</summary>
public enum MaterialTableKind
{
    /// <summary>Lista detalhada: peças iguais agrupadas por perfil/chapa/parafuso e comprimento.</summary>
    Detailed,
    /// <summary>Resumo de perfis: um só item por perfil e material, com comprimento total.</summary>
    ProfileSummary,
    /// <summary>Parafusos por diâmetro × comprimento, classe e norma.</summary>
    Bolts,
    /// <summary>Chumbadores por diâmetro × comprimento e classe.</summary>
    Anchors,
    /// <summary>Chapas agrupadas por espessura e material.</summary>
    Plates,
}

public sealed record TableColumn(string Header, double WidthFactor, bool Numeric);

/// <summary>Tabela pronta para exibir (valores já formatados em pt-BR).</summary>
public sealed record MaterialTable(MaterialTableKind Kind, string Title, IReadOnlyList<TableColumn> Columns,
    IReadOnlyList<string[]> Rows, string[]? Total, IReadOnlyList<string> Notes)
{
    public bool IsEmpty => Rows.Count == 0;
}

/// <summary>
/// Monta as tabelas da lista de material a partir do modelo neutro (sem Advance Steel).
/// Vigas compostas nunca são somadas: são formadas pelas barras simples já contadas.
/// </summary>
public static class MaterialTables
{
    public static string KindName(MaterialTableKind k) => k switch
    {
        MaterialTableKind.Detailed => "Lista detalhada (por comprimento)",
        MaterialTableKind.ProfileSummary => "Resumo de perfis",
        MaterialTableKind.Bolts => "Parafusos",
        MaterialTableKind.Anchors => "Chumbadores",
        MaterialTableKind.Plates => "Chapas (por espessura)",
        _ => k.ToString(),
    };

    public static MaterialTable Build(Extraction x, MaterialTableKind kind, string docName) => kind switch
    {
        MaterialTableKind.Detailed => Detailed(x, docName),
        MaterialTableKind.ProfileSummary => ProfileSummary(x, docName),
        MaterialTableKind.Bolts => Fasteners(x, docName, anchors: false),
        MaterialTableKind.Anchors => Fasteners(x, docName, anchors: true),
        MaterialTableKind.Plates => Plates(x, docName),
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    // ------------------------------------------------------------------ 1. detalhada (a tabela original)

    private static MaterialTable Detailed(Extraction x, string doc)
    {
        MaterialListResult m = MaterialList.Build(x);
        var cols = new[]
        {
            C("Item", 5), C("Tipo", 8), C("Descrição", 14), C("Material", 10), N("Compr. (mm)", 10), N("Qtde", 6),
            N("Peso unit. (kg)", 11), N("Peso total (kg)", 11), N("Pintura (m²)", 10),
        };
        var rows = m.Rows.Select(r => new[]
        {
            r.Item.ToString(), r.Kind, r.Description, r.Material, F(r.LengthMm, 1), r.Quantity.ToString(),
            F(r.UnitKg, 2), F(r.TotalKg, 2), F(r.TotalPaintM2, 3),
        }).ToList();
        string[] total = { "TOTAL", "", "", "", F(m.TotalBeamLengthMm, 1), m.Rows.Sum(r => r.Quantity).ToString(), "", F(m.TotalKg, 2), F(m.TotalPaintM2, 3) };
        return new MaterialTable(MaterialTableKind.Detailed, "LISTA DE MATERIAL — " + doc, cols, rows, rows.Count > 0 ? total : null, m.Notes);
    }

    // ------------------------------------------------------------------ 2. resumo de perfis

    private static MaterialTable ProfileSummary(Extraction x, string doc)
    {
        var bars = x.Elements.Where(e => e.Category == "beam" && !(e.SourceType ?? "").StartsWith("Compound", StringComparison.Ordinal)).ToList();
        var groups = bars
            .GroupBy(e => (sec: Str(e, "ProfSectionName") ?? "?", mat: Str(e, "Material") ?? "-"))
            .OrderBy(g => g.Key.sec, StringComparer.Ordinal).ThenBy(g => g.Key.mat, StringComparer.Ordinal).ToList();
        var cols = new[]
        {
            C("Item", 5), C("Perfil", 12), C("Material", 10), N("Qtde barras", 8), N("Compr. total (m)", 11),
            N("Peso/m (kg/m)", 11), N("Peso total (kg)", 11), N("Pintura (m²)", 10),
        };
        var rows = new List<string[]>();
        double tLen = 0, tKg = 0, tPaint = 0; int tQty = 0, i = 0;
        foreach (var g in groups)
        {
            double len = g.Sum(e => Num(e, "Beam.GetLength", raw: true) ?? 0) / 1000.0;
            double kg = g.Sum(e => Num(e, "Beam.GetWeight(1)") ?? 0);
            double paint = g.Sum(e => Num(e, "Beam.GetPaintArea") ?? 0);
            double? kgm = g.Select(e => Num(e, "Beam.GetWeightPerMeter")).FirstOrDefault(v => v is not null);
            rows.Add(new[] { (++i).ToString(), g.Key.sec, g.Key.mat, g.Count().ToString(), F(len, 3), F(kgm, 2), F(kg, 2), F(paint, 3) });
            tLen += len; tKg += kg; tPaint += paint; tQty += g.Count();
        }
        var notes = new List<string>();
        int compounds = x.Elements.Count(e => e.Category == "beam" && (e.SourceType ?? "").StartsWith("Compound", StringComparison.Ordinal));
        if (compounds > 0) notes.Add($"{compounds} viga(s) composta(s) não somada(s): são formadas pelas barras simples listadas.");
        string[] total = { "TOTAL", "", "", tQty.ToString(), F(tLen, 3), "", F(tKg, 2), F(tPaint, 3) };
        return new MaterialTable(MaterialTableKind.ProfileSummary, "RESUMO DE PERFIS — " + doc, cols, rows, rows.Count > 0 ? total : null, notes);
    }

    // ------------------------------------------------------------------ 3/4. parafusos e chumbadores

    private static MaterialTable Fasteners(Extraction x, string doc, bool anchors)
    {
        string cat = anchors ? "anchors" : "bolts";
        var items = x.Elements.Where(e => e.Category == cat).Select(e =>
        {
            double? d = Num(e, "ScrewBoltPattern.ScrewDiameter", raw: true) ?? Num(e, "AnchorPattern.MaxTopDiameter", raw: true);
            double? l = anchors
                ? Num(e, "AnchorPattern.Length", raw: true) ?? Num(e, "ScrewBoltPattern.ScrewLength", raw: true)
                : Num(e, "ScrewBoltPattern.ScrewLength", raw: true);
            int qty = Count(e);
            string grade = Str(e, "Grade") ?? Str(e, "Material") ?? "-";
            string std = Str(e, "Standard") ?? (anchors ? Str(e, "AnchorPartName") : null) ?? "-";
            double? kg = anchors ? Num(e, "AnchorPattern.GetWeight") : Num(e, "ScrewBoltPattern.GetWeight");
            return (desc: $"M{F(d, 0)} x {F(l, 0)}", grade, std, qty, kg, d: d ?? 0, l: l ?? 0);
        }).ToList();

        var groups = items.GroupBy(i => (i.desc, i.grade, i.std)).OrderBy(g => g.First().d).ThenBy(g => g.First().l).ToList();
        var cols = new[]
        {
            C("Item", 5), C(anchors ? "Chumbador (Ø x compr.)" : "Parafuso (Ø x compr.)", 14), C("Classe", 8), C(anchors ? "Tipo / norma" : "Norma", 12),
            N("Qtde", 7), N("Peso total (kg)", 11),
        };
        var rows = new List<string[]>();
        int i = 0, tQty = 0; double tKg = 0; bool allKg = true;
        foreach (var g in groups)
        {
            int qty = g.Sum(v => v.qty);
            double? kg = g.All(v => v.kg is not null) ? g.Sum(v => v.kg!.Value) : null;
            allKg &= kg is not null;
            rows.Add(new[] { (++i).ToString(), g.Key.desc, g.Key.grade, g.Key.std, qty.ToString(), F(kg, 3) });
            tQty += qty; tKg += kg ?? 0;
        }
        var notes = new List<string>();
        if (anchors && rows.Count > 0) notes.Add("Tabela de chumbadores ainda não validada em modelo real (leitura pelas propriedades da API).");
        if (!allKg) notes.Add("Algum peso não foi lido; o total soma só os pesos disponíveis.");
        string[] total = { "TOTAL", "", "", "", tQty.ToString(), F(tKg, 3) };
        string title = (anchors ? "CHUMBADORES — " : "PARAFUSOS — ") + doc;
        return new MaterialTable(anchors ? MaterialTableKind.Anchors : MaterialTableKind.Bolts, title, cols, rows, rows.Count > 0 ? total : null, notes);
    }

    private static int Count(NeutralElement e)
    {
        if (Num(e, "BoltPattern.NumberOfScrews", raw: true) is double n && n > 0) return (int)n;
        if (e.Properties.FirstOrDefault(p => p.TechnicalName == "GetMidpoints" && p.Status == PropertyStatus.Read)?.RawValue is JsonArray a && a.Count > 0) return a.Count;
        return 1;
    }

    // ------------------------------------------------------------------ 5. chapas por espessura

    private static MaterialTable Plates(Extraction x, string doc)
    {
        var plates = x.Elements.Where(e => e.Category == "plate").ToList();
        var groups = plates
            .GroupBy(e => (t: Math.Round(Num(e, "PlateBase.Thickness", raw: true) ?? 0, 2), mat: Str(e, "Material") ?? "-"))
            .OrderBy(g => g.Key.t).ThenBy(g => g.Key.mat, StringComparer.Ordinal).ToList();
        var cols = new[]
        {
            C("Item", 5), N("Espessura (mm)", 10), C("Material", 10), N("Qtde", 6), N("Área (m²)", 10), N("Peso total (kg)", 11), N("Pintura (m²)", 10),
        };
        var rows = new List<string[]>();
        int i = 0, tQty = 0; double tArea = 0, tKg = 0, tPaint = 0;
        foreach (var g in groups)
        {
            double area = g.Sum(e => Num(e, "PlateBase.GetArea") ?? 0);
            double kg = g.Sum(e => Num(e, "PlateBase.GetWeight") ?? 0);
            double paint = g.Sum(e => Num(e, "PlateBase.GetPaintArea") ?? 0);
            rows.Add(new[] { (++i).ToString(), F(g.Key.t, 2), g.Key.mat, g.Count().ToString(), F(area, 3), F(kg, 2), F(paint, 3) });
            tQty += g.Count(); tArea += area; tKg += kg; tPaint += paint;
        }
        string[] total = { "TOTAL", "", "", tQty.ToString(), F(tArea, 3), F(tKg, 2), F(tPaint, 3) };
        var notes = new List<string> { "Área = uma face da chapa (GetArea); pintura = todas as faces (GetPaintArea)." };
        return new MaterialTable(MaterialTableKind.Plates, "CHAPAS — " + doc, cols, rows, rows.Count > 0 ? total : null, rows.Count > 0 ? notes : new List<string>());
    }

    // ------------------------------------------------------------------ JSON (Conector IA)

    public static JsonObject ToJson(MaterialTable t) => new()
    {
        ["kind"] = t.Kind.ToString(),
        ["title"] = t.Title,
        ["columns"] = new JsonArray(t.Columns.Select(c => (JsonNode)JsonValue.Create(c.Header)!).ToArray()),
        ["rows"] = new JsonArray(t.Rows.Select(r => (JsonNode)new JsonArray(r.Select(v => (JsonNode)JsonValue.Create(v)!).ToArray())).ToArray()),
        ["total"] = t.Total == null ? null : new JsonArray(t.Total.Select(v => (JsonNode)JsonValue.Create(v)!).ToArray()),
        ["notes"] = new JsonArray(t.Notes.Select(n => (JsonNode)JsonValue.Create(n)!).ToArray()),
    };

    /// <summary>Lê os nomes vindos do conector (detailed, profile_summary, bolts, anchors, plates).</summary>
    public static bool TryParseKind(string? s, out MaterialTableKind k)
    {
        k = default;
        switch ((s ?? "").Trim().ToLowerInvariant())
        {
            case "detailed": k = MaterialTableKind.Detailed; return true;
            case "profile_summary": k = MaterialTableKind.ProfileSummary; return true;
            case "bolts": k = MaterialTableKind.Bolts; return true;
            case "anchors": k = MaterialTableKind.Anchors; return true;
            case "plates": k = MaterialTableKind.Plates; return true;
            default: return false;
        }
    }

    // ------------------------------------------------------------------ utilitários

    private static TableColumn C(string h, double w) => new(h, w, false);
    private static TableColumn N(string h, double w) => new(h, w, true);
    private static string F(double? v, int d) => MaterialList.Fmt(v, d);

    private static string? Str(NeutralElement e, string name)
    {
        NeutralProperty? p = e.Properties.FirstOrDefault(p => p.TechnicalName == name && p.Argument is null && p.Status == PropertyStatus.Read);
        return p?.RawValue is JsonValue v && v.TryGetValue(out string? s) && !string.IsNullOrWhiteSpace(s) ? s : null;
    }

    private static double? Num(NeutralElement e, string key, bool raw = false)
    {
        NeutralProperty? p = e.Properties.FirstOrDefault(p => p.Key == key && p.Status == PropertyStatus.Read);
        if (p == null) return null;
        if (!raw && p.NormalizedValue is double n) return n;
        return p.RawValue is JsonValue v && v.TryGetValue(out double d) ? d : null;
    }
}
