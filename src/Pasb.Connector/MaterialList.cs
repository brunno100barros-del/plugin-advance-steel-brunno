using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json.Nodes;
using Pasb.Model;

namespace Pasb.Connector;

/// <summary>Uma linha da lista de material (peças iguais agrupadas).</summary>
public sealed record MaterialRow(
    int Item, string Kind, string Description, string Material, double? LengthMm, int Quantity,
    double? UnitKg, double? TotalKg, double? UnitPaintM2, double? TotalPaintM2, IReadOnlyList<string> Handles);

public sealed record MaterialListResult(IReadOnlyList<MaterialRow> Rows, double TotalKg, double TotalPaintM2, double TotalBeamLengthMm,
    IReadOnlyList<string> ExcludedCompounds, IReadOnlyList<string> Notes);

/// <summary>
/// Lista de material a partir do modelo neutro (sem Advance Steel). Regras:
/// - Perfis: cada barra simples (StraightBeam etc.) pelo comprimento de corte (Beam.GetLength), peso Beam.GetWeight(1).
/// - Vigas compostas NÃO entram na soma: são formadas por barras simples já listadas (evita contar o aço duas vezes).
/// - Chapas: espessura, comprimento × largura, peso PlateBase.GetWeight.
/// - Parafusos: diâmetro × comprimento; quantidade = número de parafusos do padrão (GetMidpoints).
/// - Agrupa peças com mesma descrição, material e comprimento (0,1 mm) e mesmo peso unitário (0,01 kg).
/// </summary>
public static class MaterialList
{
    private static readonly CultureInfo Pt = CultureInfo.GetCultureInfo("pt-BR");

    public static MaterialListResult Build(Extraction x)
    {
        var parts = new List<(string kind, string desc, string mat, double? len, double? kg, double? paint, int qty, string h)>();
        var compounds = new List<string>();
        var notes = new List<string>();
        int bolt = 0;

        foreach (NeutralElement e in x.Elements)
        {
            string type = e.SourceType ?? "";
            string mat = Str(e, "Material") ?? "-";
            switch (e.Category)
            {
                case "beam" when type.StartsWith("Compound", StringComparison.Ordinal):
                    compounds.Add(e.SourceHandle ?? "?");
                    break;
                case "beam":
                {
                    string sec = Str(e, "ProfSectionName") ?? "?";
                    parts.Add(("Perfil", sec, mat, Num(e, "Beam.GetLength", raw: true), Num(e, "Beam.GetWeight(1)"), Num(e, "Beam.GetPaintArea"), 1, e.SourceHandle ?? "?"));
                    break;
                }
                case "plate":
                {
                    double? t = Num(e, "PlateBase.Thickness", raw: true), l = Num(e, "PlateBase.Length", raw: true), w = Num(e, "PlateBase.Width", raw: true);
                    string desc = "Chapa #" + Fmt(t, 2) + (l is double ll && w is double ww ? " " + Fmt(Math.Max(ll, ww), 0) + "x" + Fmt(Math.Min(ll, ww), 0) : "");
                    parts.Add(("Chapa", desc, mat, null, Num(e, "PlateBase.GetWeight"), Num(e, "PlateBase.GetPaintArea"), 1, e.SourceHandle ?? "?"));
                    break;
                }
                case "bolts":
                {
                    double? d = Num(e, "ScrewBoltPattern.ScrewDiameter", raw: true), l = Num(e, "ScrewBoltPattern.ScrewLength", raw: true);
                    int n = e.Properties.FirstOrDefault(p => p.TechnicalName == "GetMidpoints" && p.Status == PropertyStatus.Read)?.RawValue is JsonArray a ? a.Count : 0;
                    if (n == 0) { n = 1; notes.Add($"Parafuso {e.SourceHandle}: quantidade não lida; contado como 1."); }
                    string grade = Str(e, "Grade") ?? Str(e, "BoltStandard") ?? "";
                    parts.Add(("Parafuso", $"M{Fmt(d, 0)}x{Fmt(l, 0)}" + (grade.Length > 0 ? " " + grade : ""), mat, null, null, null, n, e.SourceHandle ?? "?"));
                    bolt += n;
                    break;
                }
            }
        }

        var rows = new List<MaterialRow>();
        int item = 0;
        foreach (var g in parts
                     .GroupBy(p => (p.kind, p.desc, p.mat, len: p.len is double l ? Math.Round(l, 1) : (double?)null, kg: p.kg is double k ? Math.Round(k, 2) : (double?)null))
                     .OrderBy(g => KindOrder(g.Key.kind)).ThenBy(g => g.Key.desc, StringComparer.Ordinal).ThenByDescending(g => g.Key.len ?? 0))
        {
            var list = g.ToList();
            int qty = list.Sum(p => p.qty);
            double? unitKg = list[0].kg, unitPaint = list[0].paint;
            rows.Add(new MaterialRow(++item, g.Key.kind, g.Key.desc, g.Key.mat, g.Key.len, qty,
                unitKg, unitKg is double uk ? uk * list.Count : null,
                unitPaint, list.All(p => p.paint is not null) ? list.Sum(p => p.paint!.Value) : null,
                list.Select(p => p.h).ToList()));
        }

        if (compounds.Count > 0)
            notes.Insert(0, $"{compounds.Count} viga(s) composta(s) não somada(s): são formadas pelas barras simples listadas.");
        if (x.Elements.Any(e => e.Properties.Any(p => p.TechnicalName == "SinglePartNumber" && p.RawValue?.ToString() is "indefinido" or "undefined")))
            notes.Add("Numeração (marcas de peça) não rodada: peças identificadas pelos handles.");

        return new MaterialListResult(rows,
            rows.Sum(r => r.TotalKg ?? 0), rows.Sum(r => r.TotalPaintM2 ?? 0),
            rows.Where(r => r.Kind == "Perfil").Sum(r => (r.LengthMm ?? 0) * r.Quantity), compounds, notes);
    }

    public static JsonObject ToJson(MaterialListResult m) => new()
    {
        ["rows"] = new JsonArray(m.Rows.Select(r => (JsonNode)new JsonObject
        {
            ["item"] = r.Item, ["kind"] = r.Kind, ["description"] = r.Description, ["material"] = r.Material,
            ["lengthMm"] = r.LengthMm, ["quantity"] = r.Quantity, ["unitKg"] = R(r.UnitKg, 3), ["totalKg"] = R(r.TotalKg, 3),
            ["unitPaintM2"] = R(r.UnitPaintM2, 4), ["totalPaintM2"] = R(r.TotalPaintM2, 4),
            ["handles"] = new JsonArray(r.Handles.Select(h => (JsonNode)JsonValue.Create(h)!).ToArray()),
        }).ToArray()),
        ["totalKg"] = Math.Round(m.TotalKg, 3),
        ["totalPaintM2"] = Math.Round(m.TotalPaintM2, 4),
        ["totalBeamLengthMm"] = Math.Round(m.TotalBeamLengthMm, 1),
        ["excludedCompoundBeams"] = new JsonArray(m.ExcludedCompounds.Select(h => (JsonNode)JsonValue.Create(h)!).ToArray()),
        ["notes"] = new JsonArray(m.Notes.Select(n => (JsonNode)JsonValue.Create(n)!).ToArray()),
    };

    /// <summary>Texto formatado em pt-BR (vírgula decimal); vazio para nulo.</summary>
    public static string Fmt(double? v, int decimals) => v is double d ? d.ToString("N" + decimals, Pt) : "";

    private static double? R(double? v, int d) => v is double x ? Math.Round(x, d) : null;

    private static int KindOrder(string k) => k switch { "Perfil" => 0, "Chapa" => 1, "Parafuso" => 2, _ => 3 };

    private static string? Str(NeutralElement e, string name)
    {
        NeutralProperty? p = e.Properties.FirstOrDefault(p => p.TechnicalName == name && p.Argument is null && p.Status == PropertyStatus.Read);
        return p?.RawValue is JsonValue v && v.TryGetValue(out string? s) && !string.IsNullOrWhiteSpace(s) ? s : null;
    }

    /// <summary>Valor numérico por chave "Tipo.Nome(arg)": normalizado (kg, m²) ou bruto (mm) quando <paramref name="raw"/>.</summary>
    private static double? Num(NeutralElement e, string key, bool raw = false)
    {
        NeutralProperty? p = e.Properties.FirstOrDefault(p => p.Key == key && p.Status == PropertyStatus.Read);
        if (p == null) return null;
        if (!raw && p.NormalizedValue is double n) return n;
        return p.RawValue is JsonValue v && v.TryGetValue(out double d) ? d : null;
    }
}
