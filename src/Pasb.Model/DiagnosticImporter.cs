using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;

namespace Pasb.Model;

/// <summary>
/// Converte a saída do diagnóstico (run.json + objects.jsonl) no modelo neutro.
/// Regras: nenhuma propriedade é descartada; valores brutos preservados; unidade só quando comprovada.
/// </summary>
public static class DiagnosticImporter
{
    public static Extraction ImportDirectory(string runDirectory)
    {
        string runPath = Path.Combine(runDirectory, "run.json");
        string objectsPath = Path.Combine(runDirectory, "objects.jsonl");
        string? runText = File.Exists(runPath) ? File.ReadAllText(runPath) : null;
        return Import(runText, File.ReadLines(objectsPath));
    }

    public static Extraction Import(string? runJson, IEnumerable<string> objectLines)
    {
        var x = new Extraction();
        JsonObject? run = runJson is null ? null : JsonNode.Parse(runJson) as JsonObject;
        x.RawRun = run;
        if (run is null) x.Info.Warnings.Add("run.json ausente: execução possivelmente interrompida.");
        x.SourceDocument.DeclaredLengthUnit = Str(run, "insunits");
        x.SourceDocument.ApplicationVersion = Str(run, "advanceSteelVersion");
        x.Info.Tool = Str(run, "tool");
        x.Info.ToolVersion = Str(run, "toolVersion");
        x.Info.Command = Str(run, "command");
        x.Info.StartedUtc = Str(run, "startedUtc");
        x.Info.TransactionMode = Str(run, "transactionMode");
        if (x.Info.TransactionMode == "write-discarded")
            x.Info.Warnings.Add("Leitura com escrita descartada: valores criados sob demanda (ex.: IDs) podem não existir no modelo salvo.");

        bool millimeters = string.Equals(x.SourceDocument.DeclaredLengthUnit, "Millimeters", StringComparison.OrdinalIgnoreCase);
        if (!millimeters)
            x.Info.Warnings.Add($"Unidade do desenho '{x.SourceDocument.DeclaredLengthUnit ?? "?"}' não comprovada: comprimentos sem normalização.");

        int n = 0;
        foreach (string line in objectLines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var o = (JsonObject)JsonNode.Parse(line)!;
            var e = new NeutralElement { InternalId = "E" + (++n).ToString("D6", CultureInfo.InvariantCulture), SourceHandle = Str(o, "handle") };

            if (o["openError"] is JsonObject err)
            {
                e.Status = ElementStatus.Unsupported;
                e.Category = "internal";
                e.SourceType = Str(o["identification"] as JsonObject, "autocadClass");
                e.Identification = o["identification"]?.DeepClone();
                e.ValidationMessages.Add("Não aberto pela API: " + Str(err, "message"));
                x.Elements.Add(e);
                continue;
            }

            e.SourceType = Str(o, "clrType")?.Split('.').Last();
            e.SourceUniqueId = Str(o, "uniqueId");
            if (o["inheritance"] is JsonArray inh) e.Inheritance.AddRange(inh.Select(v => v!.GetValue<string>()));
            e.Category = Categories.Of(e.Inheritance);

            foreach (JsonObject p in (o["properties"] as JsonArray ?? new JsonArray()).OfType<JsonObject>())
            {
                var prop = new NeutralProperty
                {
                    TechnicalName = Str(p, "technicalName") ?? "",
                    DeclaringType = Str(p, "declaringType") ?? "",
                    Argument = Str(p, "argument"),
                    Source = Str(p, "source") ?? "",
                    DataType = Str(p, "dataType") ?? "",
                    Status = Enum.TryParse(Str(p, "status"), out PropertyStatus s) ? s : PropertyStatus.ReadError,
                    RawValue = p["rawValue"]?.DeepClone(),
                    Error = (p["error"] as JsonObject) is { } er ? Str(er, "type") + ": " + Str(er, "message") : null,
                };
                Units.Apply(prop, millimeters);
                e.Properties.Add(prop);
                CollectRelationships(prop, e.Relationships);
                AddInventory(x, e.Category, prop.Key);
            }

            e.Geometry = GeometryExtractor.Extract(e, millimeters ? 1e-3 : null);
            if (e.Geometry is null) e.ValidationMessages.Add("Sem geometria extraível das propriedades lidas.");

            int errors = e.Properties.Count(p => p.Status == PropertyStatus.ReadError);
            int pending = e.Properties.Count(p => p.Status is PropertyStatus.Unavailable or PropertyStatus.ComplexNotSerialized);
            if (errors > 0) { e.Status = ElementStatus.Partial; e.ValidationMessages.Add($"{errors} leitura(s) com erro."); }
            if (pending > 0) { e.Status = ElementStatus.Partial; e.ValidationMessages.Add($"{pending} propriedade(s) indisponível(is) ou não serializada(s)."); }
            x.Elements.Add(e);
        }
        return x;
    }

    /// <summary>Qualquer valor com "handle" vira relação, com o nome técnico da propriedade como tipo.</summary>
    private static void CollectRelationships(NeutralProperty p, List<Relationship> rels)
    {
        if (p.Status != PropertyStatus.Read) return;
        void Visit(JsonNode? node)
        {
            switch (node)
            {
                case JsonObject obj when obj["handle"] is JsonValue hv && hv.TryGetValue(out string? h) && h is not null:
                    rels.Add(new Relationship(p.Key, h)); break;
                case JsonArray arr:
                    foreach (JsonNode? item in arr) Visit(item); break;
            }
        }
        Visit(p.RawValue);
    }

    private static void AddInventory(Extraction x, string category, string key)
    {
        if (!x.PropertyInventory.TryGetValue(category, out SortedSet<string>? set))
            x.PropertyInventory[category] = set = new SortedSet<string>(StringComparer.Ordinal);
        set.Add(key);
    }

    private static string? Str(JsonObject? o, string key) =>
        o?[key] is JsonValue v && v.TryGetValue(out string? s) ? s : null;
}

/// <summary>Categoria a partir da cadeia de herança (docs/NEUTRAL-MODEL.md, seção 7). Desconhecidas são preservadas.</summary>
public static class Categories
{
    private static readonly (string type, string category)[] Rules =
    {
        ("Wall", "concrete"), ("Slab", "concrete"), ("FootingIsolated", "concrete"), ("ConcreteBeam", "concrete"), ("ConcreteBentBeam", "concrete"),
        ("Beam", "beam"), ("PlateBase", "plate"), ("AnchorPattern", "anchors"), ("BoltPattern", "bolts"),
        ("WeldPattern", "weld"), ("SpecialPart", "special_part"), ("Connector", "connector"),
        ("FeatureObject", "feature"), ("Connection", "connection"), ("PassiveConstructionObject", "joint"),
        ("Grid", "grid"), ("UserDefinedPoint", "user_point"), ("RuleParameterSet", "joint_parameters"),
    };

    public static string Of(IList<string> inheritance)
    {
        foreach (var (type, category) in Rules)
            if (inheritance.Contains(type)) return category;
        return inheritance.Count > 0 ? "other:" + inheritance[0] : "unknown";
    }
}

/// <summary>
/// Unidades comprovadas nos testes (docs/DIAGNOSTIC-TEST-LOG.md, Testes 3 e 8). Normalização para m, m², m³ e kg.
/// Propriedades fora da tabela ficam sem unidade: o valor bruto é mantido.
/// </summary>
public static class Units
{
    private static readonly HashSet<string> LengthKeys = new(StringComparer.Ordinal)
    {
        "Beam.SysLength", "Beam.GetLength", "Beam.ShrinkValue",
        "PlateBase.Thickness", "PlateBase.Length", "PlateBase.Width", "PlateBase.Radius",
        "ScrewBoltPattern.ScrewDiameter", "ScrewBoltPattern.ScrewLength", "ScrewBoltPattern.HoleTolerance",
        "ScrewBoltPattern.NutDiameter", "ScrewBoltPattern.NutHeight", "ScrewBoltPattern.BoltHeadDiameter",
        "ScrewBoltPattern.BoltHeadHeight", "ScrewBoltPattern.BindingLength",
        "WeldPattern.Length", "WeldPattern.Thickness",
    };

    private static readonly Dictionary<string, (string unit, string normUnit, double factor)> Fixed = new(StringComparer.Ordinal)
    {
        ["Beam.GetWeight(1)"] = ("g", "kg", 1e-3),
        ["PlateBase.GetWeight"] = ("g", "kg", 1e-3),
        ["ScrewBoltPattern.GetWeight"] = ("g", "kg", 1e-3),
        ["Beam.GetWeightPerMeter"] = ("kg/m", "kg/m", 1),
        ["AtomicElement.Volume"] = ("mm3", "m3", 1e-9),
        ["PlateBase.GetArea"] = ("mm2", "m2", 1e-6),
        ["Beam.GetPaintArea"] = ("mm2", "m2", 1e-6),
        ["AtomicElement.SpecificGravity"] = ("kg/mm3", "kg/m3", 1e9),
    };

    public static void Apply(NeutralProperty p, bool drawingInMillimeters)
    {
        if (p.Status != PropertyStatus.Read || p.RawValue is not JsonValue v || !v.TryGetValue(out double d)) return;
        string key = p.Key;
        if (Fixed.TryGetValue(key, out var f))
        {
            p.Unit = f.unit; p.NormalizedUnit = f.normUnit; p.NormalizedValue = d * f.factor;
        }
        else if (drawingInMillimeters && LengthKeys.Contains(p.DeclaringType + "." + p.TechnicalName))
        {
            p.Unit = "mm"; p.NormalizedUnit = "m"; p.NormalizedValue = d * 1e-3;
        }
    }
}
