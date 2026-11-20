using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json.Nodes;
using Pasb.Model;

namespace Pasb.Connector;

public enum ItemCategory { Plate, Beam, Bolt, Anchor }

/// <summary>Campo do Advance Steel onde a marca é gravada.</summary>
public enum ItemTarget
{
    /// <summary>Numeração preliminar nativa (PreliminaryPartPrefix + PreliminaryPartNumber).</summary>
    Preliminary,
    /// <summary>AtomicElement.ItemNumber.</summary>
    ItemNumber,
    /// <summary>AtomicElement.Note.</summary>
    Note,
    UserAttribute1, UserAttribute2, UserAttribute3, UserAttribute4, UserAttribute5,
    UserAttribute6, UserAttribute7, UserAttribute8, UserAttribute9, UserAttribute10,
}

public enum ItemSortBy { Name, Quantity }

public enum FilterField { Type, Material, Note, Role, Denotation, Assembly, UserAttribute1, UserAttribute2, UserAttribute3, UserAttribute4, UserAttribute5, UserAttribute6, UserAttribute7, UserAttribute8, UserAttribute9, UserAttribute10 }

public enum FilterOp { Contains, NotContains, Equals, NotEquals, StartsWith, IsEmpty, IsNotEmpty }

/// <summary>Um filtro: só são numeradas as peças que passam em TODOS os filtros.</summary>
public sealed record ItemFilter(FilterField Field, FilterOp Op, string Value)
{
    public override string ToString() => $"{ItemNumbering.FieldName(Field)} {ItemNumbering.OpName(Op)}" + (Op is FilterOp.IsEmpty or FilterOp.IsNotEmpty ? "" : $" \"{Value}\"");
}

public sealed class ItemOptions
{
    /// <summary>Categorias na ordem de numeração (a ordem da lista define a sequência das marcas).</summary>
    public List<ItemCategory> Order { get; set; } = new() { ItemCategory.Plate, ItemCategory.Beam, ItemCategory.Bolt, ItemCategory.Anchor };
    public HashSet<ItemCategory> Enabled { get; set; } = new() { ItemCategory.Plate, ItemCategory.Beam, ItemCategory.Bolt };
    /// <summary>true: vigas curvas/poligonais e chapas dobradas entram no mesmo item das retas/planas com o mesmo perfil/espessura.</summary>
    public bool GroupBent { get; set; } = true;
    public ItemTarget Target { get; set; } = ItemTarget.Preliminary;
    public string Prefix { get; set; } = "";
    public int Start { get; set; } = 1;
    public bool Descending { get; set; }
    public ItemSortBy SortBy { get; set; } = ItemSortBy.Name;
    public List<ItemFilter> Filters { get; set; } = new();
}

/// <summary>Uma linha da prévia: uma marca para todas as peças iguais.</summary>
public sealed record ItemRow(string Mark, string Number, string Type, ItemCategory Category, IReadOnlyList<string> Handles)
{
    public int Quantity => Handles.Count;
}

public sealed record ItemExcluded(string Handle, string Type, ItemCategory Category, string Reason);

public sealed record ItemResult(IReadOnlyList<ItemRow> Rows, IReadOnlyList<ItemExcluded> Excluded)
{
    public int Parts => Rows.Sum(r => r.Quantity);
    /// <summary>Handle → (marca completa, prefixo, número).</summary>
    public Dictionary<string, (string mark, string prefix, string number)> Assignments(string prefix) =>
        Rows.SelectMany(r => r.Handles.Select(h => (h, r))).ToDictionary(x => x.h, x => (x.r.Mark, prefix, x.r.Number), StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// Numeração itemizada (como no Revit): peças iguais recebem a mesma marca — chapas por espessura, vigas por perfil,
/// parafusos e chumbadores por diâmetro × comprimento. Calcula só a prévia; quem grava é o comando no Advance Steel.
/// </summary>
public static class ItemNumbering
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static string CategoryName(ItemCategory c) => c switch
    {
        ItemCategory.Plate => "Chapa", ItemCategory.Beam => "Viga", ItemCategory.Bolt => "Parafuso", ItemCategory.Anchor => "Chumbador", _ => c.ToString(),
    };

    public static string TargetName(ItemTarget t) => t switch
    {
        ItemTarget.Preliminary => "Numeração preliminar (nativo)",
        ItemTarget.ItemNumber => "Número do item (ItemNumber)",
        ItemTarget.Note => "Nota",
        _ => "Atributo do usuário " + ((int)t - (int)ItemTarget.UserAttribute1 + 1),
    };

    public static string FieldName(FilterField f) => f switch
    {
        FilterField.Type => "Perfil / tipo", FilterField.Material => "Material", FilterField.Note => "Nota", FilterField.Role => "Papel (role)",
        FilterField.Denotation => "Denotação", FilterField.Assembly => "Conjunto", _ => "Atributo do usuário " + ((int)f - (int)FilterField.UserAttribute1 + 1),
    };

    public static string OpName(FilterOp o) => o switch
    {
        FilterOp.Contains => "contém", FilterOp.NotContains => "não contém", FilterOp.Equals => "igual a", FilterOp.NotEquals => "diferente de",
        FilterOp.StartsWith => "começa com", FilterOp.IsEmpty => "está vazio", FilterOp.IsNotEmpty => "não está vazio", _ => o.ToString(),
    };

    /// <summary>Categoria e tipo (chave do item) de um elemento; null se não for chapa, viga, parafuso ou chumbador.</summary>
    public static (ItemCategory cat, string type, bool bent)? Classify(NeutralElement e)
    {
        string cls = e.SourceType ?? "";
        switch (e.Category)
        {
            case "plate":
            {
                double? t = Num(e, "PlateBase.Thickness");
                string type = t is double v ? Trim(v) : "?";
                return (ItemCategory.Plate, type, cls.Contains("Folded", StringComparison.Ordinal));
            }
            case "beam":
            {
                string sec = Str(e, "ProfSectionName") ?? Str(e, "ProfName") ?? "?";
                if (cls.StartsWith("Compound", StringComparison.Ordinal) && string.Equals(sec, "Default", StringComparison.OrdinalIgnoreCase))
                    sec = Str(e, "CompoundClassName") is { } cc ? cc.Replace("Compound", "") + " (composto)" : sec;
                bool bent = cls.Contains("Bent", StringComparison.Ordinal) || cls.Contains("Poly", StringComparison.Ordinal) || cls.Contains("Unfolded", StringComparison.Ordinal);
                return (ItemCategory.Beam, sec, bent);
            }
            case "bolts":
            case "anchors":
            {
                double? d = Num(e, "ScrewBoltPattern.ScrewDiameter") ?? Num(e, "AnchorPattern.MaxTopDiameter");
                double? l = e.Category == "anchors" ? Num(e, "AnchorPattern.Length") ?? Num(e, "ScrewBoltPattern.ScrewLength") : Num(e, "ScrewBoltPattern.ScrewLength");
                string grade = Str(e, "Grade") ?? "";
                string type = $"Ø{(d is double dd ? Trim(dd) : "?")} x {(l is double ll ? Trim(ll) : "?")}" + (grade.Length > 0 ? " " + grade : "");
                return (e.Category == "anchors" ? ItemCategory.Anchor : ItemCategory.Bolt, type, false);
            }
            default:
                return null;
        }
    }

    public static ItemResult Compute(Extraction x, ItemOptions o)
    {
        var excluded = new List<ItemExcluded>();
        var groups = new Dictionary<(ItemCategory cat, string type), List<string>>();
        foreach (NeutralElement e in x.Elements)
        {
            if (Classify(e) is not { } c) continue;
            string handle = e.SourceHandle ?? "?";
            if (!o.Enabled.Contains(c.cat)) { excluded.Add(new(handle, c.type, c.cat, "categoria desmarcada")); continue; }
            ItemFilter? failed = o.Filters.FirstOrDefault(f => !Passes(e, c.type, f));
            if (failed != null) { excluded.Add(new(handle, c.type, c.cat, "filtro: " + failed)); continue; }
            string type = c.bent && !o.GroupBent ? c.type + (c.cat == ItemCategory.Plate ? " (dobrada)" : " (curva)") : c.type;
            if (!groups.TryGetValue((c.cat, type), out var list)) groups[(c.cat, type)] = list = new List<string>();
            list.Add(handle);
        }

        var rows = new List<ItemRow>();
        int n = o.Start;
        foreach (ItemCategory cat in o.Order.Where(o.Enabled.Contains))
        {
            IEnumerable<KeyValuePair<(ItemCategory cat, string type), List<string>>> g = groups.Where(kv => kv.Key.cat == cat);
            g = o.SortBy == ItemSortBy.Quantity
                ? (o.Descending ? g.OrderByDescending(kv => kv.Value.Count).ThenBy(kv => kv.Key.type, NaturalComparer.Instance)
                                : g.OrderBy(kv => kv.Value.Count).ThenBy(kv => kv.Key.type, NaturalComparer.Instance))
                : (o.Descending ? g.OrderByDescending(kv => kv.Key.type, NaturalComparer.Instance) : g.OrderBy(kv => kv.Key.type, NaturalComparer.Instance));
            foreach (var kv in g)
            {
                string number = n.ToString(Inv);
                rows.Add(new ItemRow(o.Prefix + number, number, kv.Key.type, cat, kv.Value.OrderBy(h => h, StringComparer.OrdinalIgnoreCase).ToList()));
                n++;
            }
        }
        return new ItemResult(rows, excluded);
    }

    // ------------------------------------------------------------------ filtros

    public static string FieldValue(NeutralElement e, string type, FilterField f) => f switch
    {
        FilterField.Type => type,
        FilterField.Material => Str(e, "Material") ?? "",
        FilterField.Note => Str(e, "Note") ?? "",
        FilterField.Role => Str(e, "Role") ?? "",
        FilterField.Denotation => Str(e, "Denotation") ?? "",
        FilterField.Assembly => Str(e, "Assembly") ?? "",
        _ => UserAttribute(e, (int)f - (int)FilterField.UserAttribute1),
    };

    private static bool Passes(NeutralElement e, string type, ItemFilter f)
    {
        string v = FieldValue(e, type, f.Field).Trim();
        string w = (f.Value ?? "").Trim();
        return f.Op switch
        {
            FilterOp.Contains => v.Contains(w, StringComparison.OrdinalIgnoreCase),
            FilterOp.NotContains => !v.Contains(w, StringComparison.OrdinalIgnoreCase),
            FilterOp.Equals => string.Equals(v, w, StringComparison.OrdinalIgnoreCase),
            FilterOp.NotEquals => !string.Equals(v, w, StringComparison.OrdinalIgnoreCase),
            FilterOp.StartsWith => v.StartsWith(w, StringComparison.OrdinalIgnoreCase),
            FilterOp.IsEmpty => v.Length == 0 || v == "-",
            FilterOp.IsNotEmpty => v.Length > 0 && v != "-",
            _ => true,
        };
    }

    private static string UserAttribute(NeutralElement e, int idx)
    {
        NeutralProperty? p = e.Properties.FirstOrDefault(p => p.TechnicalName == "GetUserAttribute" && p.Argument == idx.ToString(Inv) && p.Status == PropertyStatus.Read);
        return p?.RawValue is JsonValue jv && jv.TryGetValue(out string? s) ? s ?? "" : "";
    }

    // ------------------------------------------------------------------ utilitários

    /// <summary>Número sem zeros à direita, com ponto decimal (como o Advance Steel mostra: 4.75, 12, 38.1).</summary>
    private static string Trim(double v) => Math.Round(v, 3).ToString("0.###", Inv);

    private static string? Str(NeutralElement e, string name)
    {
        NeutralProperty? p = e.Properties.FirstOrDefault(p => p.TechnicalName == name && p.Argument is null && p.Status == PropertyStatus.Read);
        return p?.RawValue is JsonValue v && v.TryGetValue(out string? s) && !string.IsNullOrWhiteSpace(s) ? s : null;
    }

    private static double? Num(NeutralElement e, string key)
    {
        NeutralProperty? p = e.Properties.FirstOrDefault(p => p.Key == key && p.Status == PropertyStatus.Read);
        return p?.RawValue is JsonValue v && v.TryGetValue(out double d) ? d : null;
    }
}

/// <summary>Ordem "natural": 4.75 &lt; 12.5, "U100" &lt; "U200", "L50x5" &lt; "L100x10".</summary>
public sealed class NaturalComparer : IComparer<string>
{
    public static readonly NaturalComparer Instance = new();

    public int Compare(string? a, string? b)
    {
        a ??= ""; b ??= "";
        int i = 0, j = 0;
        while (i < a.Length && j < b.Length)
        {
            if (char.IsDigit(a[i]) && char.IsDigit(b[j]))
            {
                int si = i, sj = j;
                while (i < a.Length && (char.IsDigit(a[i]) || a[i] == '.' || a[i] == ',')) i++;
                while (j < b.Length && (char.IsDigit(b[j]) || b[j] == '.' || b[j] == ',')) j++;
                double.TryParse(a[si..i].Replace(',', '.').TrimEnd('.'), NumberStyles.Float, CultureInfo.InvariantCulture, out double x);
                double.TryParse(b[sj..j].Replace(',', '.').TrimEnd('.'), NumberStyles.Float, CultureInfo.InvariantCulture, out double y);
                int c = x.CompareTo(y);
                if (c != 0) return c;
            }
            else
            {
                int c = char.ToUpperInvariant(a[i]).CompareTo(char.ToUpperInvariant(b[j]));
                if (c != 0) return c;
                i++; j++;
            }
        }
        return (a.Length - i).CompareTo(b.Length - j);
    }
}
