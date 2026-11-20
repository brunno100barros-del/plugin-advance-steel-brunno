using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Pasb.Georeferencing;
using Pasb.IfcExport;

namespace Pasb.Ui;

public enum ExportScope { Model, Selection }
public enum GeoChoice { None, Sirgas, Local }
public enum AngleInput { Rotation, Azimuth }

/// <summary>Tudo o que a janela de exportação coleta. Serializável para lembrar a última configuração.</summary>
public sealed class ExportSettings
{
    public string Format { get; set; } = "IFC4";
    public ExportScope Scope { get; set; } = ExportScope.Model;
    public Dictionary<string, bool> Groups { get; set; } = ObjectGroups.All.ToDictionary(g => g, _ => true);

    public string ProjectName { get; set; } = "Projeto Advance Steel";
    public string SiteName { get; set; } = "Site";
    public string BuildingName { get; set; } = "Building";

    public GeoChoice Geo { get; set; } = GeoChoice.None;
    public int UtmZone { get; set; } = 22;
    public Hemisphere Hemisphere { get; set; } = Hemisphere.South;
    public string LocalCrsName { get; set; } = "Sistema local da obra";

    /// <summary>Ponto base no modelo, em unidades do desenho (mm), coordenadas globais (WCS).</summary>
    public double BaseX { get; set; }
    public double BaseY { get; set; }
    public double BaseZ { get; set; }

    /// <summary>Coordenadas de destino do ponto base, em metros.</summary>
    public double TargetX { get; set; }
    public double TargetY { get; set; }
    public double TargetZ { get; set; }

    public AngleInput AngleMode { get; set; } = AngleInput.Rotation;
    public double AngleDegrees { get; set; }

    public string OutputPath { get; set; } = "";

    /// <summary>Erros que impedem a exportação (lista vazia = pode exportar).</summary>
    public List<string> Validate(double unitToMeters = 0.001)
    {
        var errors = new List<string>();
        if (Format != "IFC4") errors.Add("Somente IFC4 está disponível nesta versão.");
        if (!Groups.Values.Any(v => v)) errors.Add("Selecione ao menos um tipo de objeto para exportar.");
        if (string.IsNullOrWhiteSpace(OutputPath)) errors.Add("Informe o arquivo de saída.");
        else if (!OutputPath.EndsWith(".ifc", StringComparison.OrdinalIgnoreCase)) errors.Add("O arquivo de saída deve terminar em .ifc.");
        if (ToGeoParameters(unitToMeters) is { } geo) errors.AddRange(Georeference.Validate(geo));
        return errors;
    }

    public GeoParameters? ToGeoParameters(double unitToMeters)
    {
        if (Geo == GeoChoice.None) return null;
        double rotation = AngleMode == AngleInput.Azimuth ? GeoParameters.RotationFromModelNorthAzimuth(AngleDegrees) : AngleDegrees;
        var model = new Point3(BaseX, BaseY, BaseZ);
        var target = new Point3(TargetX, TargetY, TargetZ);
        return Geo == GeoChoice.Sirgas
            ? new GeoParameters(GeoMode.SirgasUtm, model, target, rotation, unitToMeters, UtmZone, Hemisphere)
            : new GeoParameters(GeoMode.LocalXyz, model, target, rotation, unitToMeters, LocalCrsName: LocalCrsName);
    }

    public ExportOptions ToExportOptions(double unitToMeters) =>
        new(ProjectName, Path.GetFileName(OutputPath), ToGeoParameters(unitToMeters))
        {
            SiteName = SiteName,
            BuildingName = BuildingName,
            IncludedGroups = Groups.Where(kv => kv.Value).Select(kv => kv.Key).ToHashSet(StringComparer.Ordinal),
        };

    // ------------------------------------------------------------------ persistência

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true, Converters = { new JsonStringEnumConverter() } };

    public static string DefaultPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PASB", "export-settings.json");

    /// <summary>Carrega a última configuração; qualquer problema volta ao padrão (nunca lança).</summary>
    public static ExportSettings Load(string? path = null)
    {
        try
        {
            string p = path ?? DefaultPath;
            if (!File.Exists(p)) return new ExportSettings();
            ExportSettings s = JsonSerializer.Deserialize<ExportSettings>(File.ReadAllText(p), Json) ?? new ExportSettings();
            foreach (string g in ObjectGroups.All) s.Groups.TryAdd(g, true);   // grupos novos entram marcados
            return s;
        }
        catch (Exception) { return new ExportSettings(); }
    }

    public void Save(string? path = null)
    {
        try
        {
            string p = path ?? DefaultPath;
            Directory.CreateDirectory(Path.GetDirectoryName(p)!);
            File.WriteAllText(p, JsonSerializer.Serialize(this, Json), new UTF8Encoding(false));
        }
        catch (Exception) { /* preferência do usuário: falhar ao salvar não impede a exportação */ }
    }

    /// <summary>Aceita vírgula ou ponto como separador decimal.</summary>
    public static bool TryParseNumber(string? text, out double value) =>
        double.TryParse((text ?? "").Trim().Replace(" ", "").Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out value)
        && double.IsFinite(value);

    public static string FormatNumber(double v, int decimals = 4) =>
        v.ToString("0." + new string('#', decimals), CultureInfo.GetCultureInfo("pt-BR"));
}
