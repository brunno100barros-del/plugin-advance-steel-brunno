using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Pasb.Bridge;
using Pasb.Georeferencing;
using Pasb.IfcExport;
using Pasb.Model;
using Pasb.Ui;

namespace Pasb.Connector;

/// <summary>
/// O que as ferramentas do Conector IA fazem depois que o plugin já leu o modelo (saída do diagnóstico).
/// Nada aqui acessa o Advance Steel; por isso é testado fora dele.
/// </summary>
public static class ConnectorLogic
{
    /// <summary>Propriedades curtas mostradas na lista de objetos, quando existirem (nomes técnicos do Advance Steel).</summary>
    private static readonly string[] Descriptors =
        { "ProfName", "ProfSectionName", "ProfSectionType", "SectionName", "MaterialName", "Material", "Role", "Thickness", "Length", "Weight", "Name" };

    public static JsonObject Summarize(Extraction x, string diagnosticDir, bool safe, int maxObjects, string? category)
    {
        maxObjects = Math.Clamp(maxObjects, 0, 5000);
        var byClass = new SortedDictionary<string, int>(StringComparer.Ordinal);
        var byCategory = new SortedDictionary<string, int>(StringComparer.Ordinal);
        foreach (NeutralElement e in x.Elements)
        {
            string cls = ShortType(e.SourceType);
            byClass[cls] = byClass.GetValueOrDefault(cls) + 1;
            byCategory[e.Category] = byCategory.GetValueOrDefault(e.Category) + 1;
        }

        IEnumerable<NeutralElement> filtered = x.Elements;
        if (!string.IsNullOrWhiteSpace(category)) filtered = filtered.Where(e => string.Equals(e.Category, category.Trim(), StringComparison.OrdinalIgnoreCase));
        List<NeutralElement> list = filtered.ToList();

        var objects = new JsonArray();
        foreach (NeutralElement e in list.Take(maxObjects))
        {
            var o = new JsonObject { ["handle"] = e.SourceHandle, ["class"] = ShortType(e.SourceType), ["category"] = e.Category };
            foreach (string d in Descriptors)
            {
                NeutralProperty? p = e.Properties.FirstOrDefault(p => p.TechnicalName == d && p.Status == PropertyStatus.Read && p.Argument is null);
                if (p?.RawValue is JsonValue v) o[d] = v.DeepClone();
            }
            if (e.Status != ElementStatus.Valid) o["status"] = e.Status.ToString();
            objects.Add(o);
        }

        return new JsonObject
        {
            ["document"] = x.SourceDocument.Name,
            ["drawingUnit"] = x.SourceDocument.DeclaredLengthUnit,
            ["modelUnchanged"] = safe,
            ["totalObjects"] = x.Elements.Count,
            ["countsByClass"] = ToJson(byClass),
            ["countsByCategory"] = ToJson(byCategory),
            ["listedObjects"] = objects.Count,
            ["matchingObjects"] = list.Count,
            ["truncated"] = list.Count > objects.Count,
            ["objects"] = objects,
            ["diagnosticFolder"] = diagnosticDir,
        };
    }

    /// <summary>Todas as propriedades de um objeto já lido. Lança <see cref="BridgeException"/> se o handle não estiver na leitura.</summary>
    public static JsonObject DescribeObject(Extraction x, string handle, bool safe)
    {
        string h = NormalizeHandle(handle);
        NeutralElement e = x.Elements.FirstOrDefault(el => string.Equals(el.SourceHandle, h, StringComparison.OrdinalIgnoreCase))
            ?? throw new BridgeException($"Objeto com handle {h} não encontrado no modelo.");
        var props = new JsonArray();
        foreach (NeutralProperty p in e.Properties)
        {
            var o = new JsonObject { ["key"] = p.Key, ["status"] = p.Status.ToString(), ["dataType"] = p.DataType };
            if (p.RawValue != null) o["value"] = p.RawValue.DeepClone();
            if (p.Unit != null) o["unit"] = p.Unit;
            if (p.NormalizedValue is double nv) { o["normalized"] = nv; o["normalizedUnit"] = p.NormalizedUnit; }
            if (p.Error != null) o["error"] = p.Error;
            props.Add(o);
        }
        return new JsonObject
        {
            ["handle"] = e.SourceHandle,
            ["class"] = e.SourceType,
            ["category"] = e.Category,
            ["inheritance"] = new JsonArray(e.Inheritance.Select(i => (JsonNode)JsonValue.Create(i)!).ToArray()),
            ["status"] = e.Status.ToString(),
            ["validationMessages"] = new JsonArray(e.ValidationMessages.Select(i => (JsonNode)JsonValue.Create(i)!).ToArray()),
            ["relationships"] = new JsonArray(e.Relationships.Select(r => (JsonNode)new JsonObject { ["kind"] = r.Kind, ["target"] = r.TargetHandle }).ToArray()),
            ["geometry"] = e.Geometry == null ? null : JsonSerializer.SerializeToNode(e.Geometry, NeutralModelJson.Options),
            ["propertyCount"] = props.Count,
            ["properties"] = props,
            ["modelUnchanged"] = safe,
        };
    }

    /// <summary>Handle hexadecimal em maiúsculas; aceita "0x2f1" ou "2f1".</summary>
    public static string NormalizeHandle(string? handle)
    {
        string h = (handle ?? "").Trim();
        if (h.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) h = h[2..];
        if (h.Length == 0 || h.Length > 16 || !h.All(Uri.IsHexDigit)) throw new BridgeException("Handle inválido: use o valor hexadecimal, por exemplo \"2F1\".");
        return h.ToUpperInvariant();
    }

    /// <summary>
    /// Aplica os parâmetros da ferramenta pasb_export_ifc sobre a última configuração da janela.
    /// Lança <see cref="BridgeException"/> com todos os erros encontrados.
    /// </summary>
    public static ExportSettings ApplyExportParameters(ExportSettings baseline, JsonObject p, double drawingUnitToMeters, out bool overwrite)
    {
        var known = new HashSet<string>(StringComparer.Ordinal)
        {
            "output_path", "overwrite", "project_name", "site_name", "building_name", "object_groups", "georeference", "utm_zone",
            "hemisphere", "local_crs_name", "base_point_model", "target_point", "target_unit", "angle_mode", "angle_degrees", "apply_to_placement",
        };
        var errors = p.Select(kv => kv.Key).Where(k => !known.Contains(k)).Select(k => $"Parâmetro desconhecido: {k}.").ToList();

        // Cópia: a configuração da janela não é alterada pelo conector.
        ExportSettings s = JsonSerializer.Deserialize<ExportSettings>(JsonSerializer.Serialize(baseline))!;
        s.Scope = ExportScope.Model;
        overwrite = false;
        try
        {
            s.OutputPath = Str(p, "output_path") ?? "";
            if (s.OutputPath.Length > 0 && !Path.IsPathFullyQualified(s.OutputPath)) errors.Add("output_path deve ser um caminho completo (ex.: C:\\Obras\\modelo.ifc).");
            overwrite = p["overwrite"]?.GetValue<bool>() ?? false;
            if (Str(p, "project_name") is { } pn) s.ProjectName = pn;
            if (Str(p, "site_name") is { } sn) s.SiteName = sn;
            if (Str(p, "building_name") is { } bn) s.BuildingName = bn;
            if (p["object_groups"] is JsonArray groups)
            {
                var wanted = groups.Select(g => g?.GetValue<string>() ?? "").ToList();
                foreach (string bad in wanted.Where(g => !ObjectGroups.All.Contains(g))) errors.Add($"Tipo de objeto desconhecido: {bad}.");
                foreach (string g in ObjectGroups.All) s.Groups[g] = wanted.Contains(g);
            }
            switch (Str(p, "georeference"))
            {
                case null: break;
                case "none": s.Geo = GeoChoice.None; break;
                case "sirgas": s.Geo = GeoChoice.Sirgas; break;
                case "local": s.Geo = GeoChoice.Local; break;
                default: errors.Add("georeference deve ser none, sirgas ou local."); break;
            }
            if (p["utm_zone"] is { } z) s.UtmZone = z.GetValue<int>();
            switch (Str(p, "hemisphere"))
            {
                case null: break;
                case "S": s.Hemisphere = Hemisphere.South; break;
                case "N": s.Hemisphere = Hemisphere.North; break;
                default: errors.Add("hemisphere deve ser S ou N."); break;
            }
            if (Str(p, "local_crs_name") is { } crs) s.LocalCrsName = crs;
            if (Vec(p, "base_point_model", errors) is { } b) (s.BaseX, s.BaseY, s.BaseZ) = (b[0], b[1], b[2]);
            if (Vec(p, "target_point", errors) is { } t) (s.TargetX, s.TargetY, s.TargetZ) = (t[0], t[1], t[2]);
            switch (Str(p, "target_unit"))
            {
                case null: break;
                case "m": s.TargetUnit = TargetUnit.Meters; break;
                case "mm": s.TargetUnit = TargetUnit.Millimeters; break;
                default: errors.Add("target_unit deve ser m ou mm."); break;
            }
            switch (Str(p, "angle_mode"))
            {
                case null: break;
                case "rotation": s.AngleMode = AngleInput.Rotation; break;
                case "azimuth": s.AngleMode = AngleInput.Azimuth; break;
                default: errors.Add("angle_mode deve ser rotation ou azimuth."); break;
            }
            if (p["apply_to_placement"] is { } ap) s.GeoInPlacement = ap.GetValue<bool>();
            if (p["angle_degrees"] is { } a)
            {
                s.AngleDegrees = a.GetValue<double>();
                if (!double.IsFinite(s.AngleDegrees)) errors.Add("angle_degrees inválido.");
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or FormatException)
        {
            errors.Add("Algum parâmetro tem o tipo errado (texto, número ou verdadeiro/falso).");
        }

        if (errors.Count == 0) errors.AddRange(s.Validate(drawingUnitToMeters));
        if (errors.Count == 0 && File.Exists(s.OutputPath) && !overwrite)
            errors.Add("O arquivo já existe. Use overwrite=true para sobrescrever.");
        if (errors.Count == 0 && !Directory.Exists(Path.GetDirectoryName(s.OutputPath)))
            errors.Add("A pasta do arquivo de saída não existe.");
        if (errors.Count > 0) throw new BridgeException(string.Join(" ", errors));
        return s;
    }

    /// <summary>Gera e grava o IFC e o relatório; devolve o resumo para o assistente.</summary>
    public static JsonObject Export(Extraction x, ExportSettings s, double drawingUnitToMeters, string diagnosticDir)
    {
        string ifc = IfcExporter.Export(x, s.ToExportOptions(drawingUnitToMeters), out ExportReport report);
        File.WriteAllText(s.OutputPath, ifc, new UTF8Encoding(false));
        string reportPath = Path.ChangeExtension(s.OutputPath, ".report.json");
        File.WriteAllText(reportPath, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));
        return new JsonObject
        {
            ["ifcPath"] = s.OutputPath,
            ["reportPath"] = reportPath,
            ["bytes"] = new FileInfo(s.OutputPath).Length,
            ["exportedByIfcClass"] = ToJson(report.ExportedByIfcClass),
            ["skippedByCategory"] = ToJson(report.SkippedByCategory),
            ["propertySets"] = report.PropertySets,
            ["properties"] = report.Properties,
            ["georeference"] = report.Georeference?.DeepClone(),
            ["warnings"] = new JsonArray(report.Warnings.Select(w => (JsonNode)JsonValue.Create(w)!).ToArray()),
            ["limitations"] = new JsonArray(report.Limitations.Select(w => (JsonNode)JsonValue.Create(w)!).ToArray()),
            ["diagnosticFolder"] = diagnosticDir,
            ["modelUnchanged"] = true,
        };
    }

    private static string ShortType(string? t)
    {
        if (string.IsNullOrEmpty(t)) return "?";
        int i = t.LastIndexOf('.');
        return i >= 0 ? t[(i + 1)..] : t;
    }

    private static JsonObject ToJson(IEnumerable<KeyValuePair<string, int>> d)
    {
        var o = new JsonObject();
        foreach (var kv in d) o[kv.Key] = kv.Value;
        return o;
    }

    private static string? Str(JsonObject p, string name) => p[name] is { } n ? n.GetValue<string>().Trim() : null;

    private static double[]? Vec(JsonObject p, string name, List<string> errors)
    {
        if (p[name] is not { } n) return null;
        if (n is not JsonArray a || a.Count != 3) { errors.Add($"{name} deve ter 3 números [x, y, z]."); return null; }
        double[] v = a.Select(e => e!.GetValue<double>()).ToArray();
        if (!v.All(double.IsFinite)) { errors.Add($"{name} tem número inválido."); return null; }
        return v;
    }
}
