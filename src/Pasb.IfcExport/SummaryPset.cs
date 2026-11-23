using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Pasb.Model;
using static Pasb.IfcExport.StepWriter;

namespace Pasb.IfcExport;

/// <summary>
/// Pset "PASB_Resumo" da exportação resumida: poucas propriedades, com nome em português e unidade SI (m, m², kg, kg/m).
/// Uma propriedade só entra quando o valor existe e a unidade é comprovada (nada de valor em unidade errada).
/// </summary>
internal static class SummaryPset
{
    public const string Name = "PASB_Resumo";

    private enum Kind { Label, Length, Area, Mass, MassPerLength, Count, Real }

    private sealed record Field(string Label, Kind Kind, params string[] Keys);

    private static readonly Field PartMark = new("Marca da peça", Kind.Label, "AtomicElement.GetSinglePartPositionNumber");
    private static readonly Field AssemblyMark = new("Marca do conjunto", Kind.Label, "AtomicElement.GetMainPartPositionNumber");
    private static readonly Field Steel = new("Tipo de aço", Kind.Label, "AtomicElement.Material");

    private static readonly Dictionary<string, Field[]> ByCategory = new(StringComparer.Ordinal)
    {
        ["beam"] = new[]
        {
            PartMark, AssemblyMark, Steel,
            new Field("Perfil", Kind.Label, "Beam.ProfSectionName"),
            new Field("Tipo de perfil", Kind.Label, "Beam.ProfSectionType"),
            new Field("Comprimento", Kind.Length, "Beam.GetLength"),
            new Field("Peso por metro", Kind.MassPerLength, "Beam.GetWeightPerMeter"),
            new Field("Peso total", Kind.Mass, "Beam.GetWeight(1)"),
            new Field("Área de pintura", Kind.Area, "Beam.GetPaintArea"),
        },
        ["plate"] = new[]
        {
            PartMark, AssemblyMark, Steel,
            new Field("Espessura", Kind.Length, "PlateBase.Thickness"),
            new Field("Comprimento", Kind.Length, "PlateBase.GetPhysLengthAndWidth/dLength"),
            new Field("Largura", Kind.Length, "PlateBase.GetPhysLengthAndWidth/dWidth"),
            new Field("Peso total", Kind.Mass, "PlateBase.GetWeight"),
            new Field("Área de pintura", Kind.Area, "PlateBase.GetPaintArea"),
        },
        ["bolts"] = new[]
        {
            new Field("Norma", Kind.Label, "ScrewBoltPattern.Standard"),
            new Field("Classe", Kind.Label, "ScrewBoltPattern.Grade"),
            new Field("Conjunto", Kind.Label, "ScrewBoltPattern.BoltAssembly"),
            new Field("Diâmetro", Kind.Length, "ScrewBoltPattern.ScrewDiameter"),
            new Field("Comprimento", Kind.Length, "ScrewBoltPattern.ScrewLength"),
            new Field("Quantidade", Kind.Count, "BoltPattern.NumberOfScrews"),
            new Field("Peso total", Kind.Mass, "ScrewBoltPattern.GetWeight"),
        },
        ["anchors"] = new[]
        {
            new Field("Norma", Kind.Label, "ScrewBoltPattern.Standard"),
            new Field("Classe", Kind.Label, "ScrewBoltPattern.Grade"),
            new Field("Tipo", Kind.Label, "AnchorPattern.AnchorType"),
            new Field("Diâmetro", Kind.Length, "ScrewBoltPattern.ScrewDiameter"),
            new Field("Comprimento", Kind.Length, "ScrewBoltPattern.ScrewLength"),
            new Field("Quantidade", Kind.Count, "AnchorPattern.NumberOfScrews", "BoltPattern.NumberOfScrews"),
            new Field("Peso total", Kind.Mass, "AnchorPattern.GetWeight"),
        },
        ["weld"] = new[]
        {
            new Field("Tipo", Kind.Label, "WeldPattern.GetWeldType(kUpper)"),
            new Field("Espessura", Kind.Length, "WeldPattern.Thickness"),
            new Field("Comprimento", Kind.Length, "WeldPattern.Length"),
        },
        ["special_part"] = new[]
        {
            PartMark, AssemblyMark, Steel,
            new Field("Nome", Kind.Label, "SpecialPart.BlockName"),
            // Unidade de SpecialPart.Weight/PaintArea não comprovada nos testes: valor bruto do Advance Steel.
            new Field("Peso (valor do Advance Steel)", Kind.Real, "SpecialPart.Weight"),
            new Field("Área de pintura (valor do Advance Steel)", Kind.Real, "SpecialPart.PaintArea"),
        },
    };

    /// <summary>Grava o Pset e o liga ao elemento. Retorna a quantidade de propriedades.</summary>
    public static int Write(StepWriter w, NeutralElement e, int elementId, string gid, double? k, int metre, int kg, int area)
    {
        var byKey = e.Properties.Where(p => p.Status == PropertyStatus.Read)
            .GroupBy(p => p.Key).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        var props = new List<int>
        {
            w.Add("IfcPropertySingleValue", Str("Handle"), Null, "IFCIDENTIFIER(" + Str(e.SourceHandle ?? "") + ")", Null),
        };
        if (!ByCategory.ContainsKey(e.Category))
            props.Add(w.Add("IfcPropertySingleValue", Str("Tipo"), Null, "IFCLABEL(" + Str(e.SourceType ?? e.Category) + ")", Null));
        foreach (Field f in ByCategory.GetValueOrDefault(e.Category) ?? Array.Empty<Field>())
            if (Value(f, byKey, k, metre, kg, area) is var (value, unit))
                props.Add(w.Add("IfcPropertySingleValue", Str(f.Label), Null, value, unit is int u ? Ref(u) : Null));

        int pset = w.Add("IfcPropertySet", Str(IfcGuid.Compress(IfcGuid.FromName(gid + "/" + Name))), Null, Str(Name), Null, Refs(props));
        w.Add("IfcRelDefinesByProperties", Str(IfcGuid.Compress(IfcGuid.FromName(gid + "/rel/" + Name))), Null, Null, Null, "(" + Ref(elementId) + ")", Ref(pset));
        return props.Count;
    }

    private static (string value, int? unit)? Value(Field f, Dictionary<string, NeutralProperty> byKey, double? k, int metre, int kg, int area)
    {
        foreach (string key in f.Keys)
        {
            // "Chave/campo": campo de um objeto (ex.: GetPhysLengthAndWidth → dLength), em unidades do desenho.
            string[] parts = key.Split('/');
            if (!byKey.TryGetValue(parts[0], out NeutralProperty? p)) continue;
            switch (f.Kind)
            {
                case Kind.Label when Text(p.RawValue) is { Length: > 0 } t:
                    return ("IFCLABEL(" + Str(t) + ")", null);
                case Kind.Count when p.RawValue is JsonValue cv && cv.TryGetValue(out long n):
                    return ("IFCCOUNTMEASURE(" + Int(n) + ".)", null);
                case Kind.Real when p.RawValue is JsonValue rv && rv.TryGetValue(out double d) && double.IsFinite(d):
                    return ("IFCREAL(" + Real(d) + ")", null);
                case Kind.Length when parts.Length == 2:
                    if (k is double kk && p.RawValue is JsonObject o && o[parts[1]] is JsonValue ov && ov.TryGetValue(out double od) && double.IsFinite(od))
                        return ("IFCLENGTHMEASURE(" + Real(Math.Round(od * kk, 6)) + ")", metre);
                    break;
                case Kind.Length when p.NormalizedUnit == "m" && p.NormalizedValue is double m:
                    return ("IFCLENGTHMEASURE(" + Real(Math.Round(m, 6)) + ")", metre);
                case Kind.Area when p.NormalizedUnit == "m2" && p.NormalizedValue is double a:
                    return ("IFCAREAMEASURE(" + Real(Math.Round(a, 6)) + ")", area);
                case Kind.Mass when p.NormalizedUnit == "kg" && p.NormalizedValue is double kgv:
                    return ("IFCMASSMEASURE(" + Real(Math.Round(kgv, 6)) + ")", kg);
                case Kind.MassPerLength when p.NormalizedUnit == "kg/m" && p.NormalizedValue is double kpm:
                    // Unidade derivada kg/m não declarada no projeto: IfcMassPerLengthMeasure usa a unidade SI padrão (kg/m).
                    return ("IFCMASSPERLENGTHMEASURE(" + Real(Math.Round(kpm, 6)) + ")", null);
            }
        }
        return null;
    }

    /// <summary>Texto de um valor: texto simples, número, ou nome de enumeração ({"name":..,"value":..}).</summary>
    private static string? Text(JsonNode? n) => n switch
    {
        JsonValue v when v.TryGetValue(out string? s) => s,
        JsonValue v when v.GetValueKind() is JsonValueKind.Number => v.ToJsonString(),
        JsonObject o when o["name"] is JsonValue nm && nm.TryGetValue(out string? s) => s,
        _ => null,
    };
}
