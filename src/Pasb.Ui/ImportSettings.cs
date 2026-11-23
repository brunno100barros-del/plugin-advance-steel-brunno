using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Pasb.Ui;

public enum ImportGeometry { Solid, Mesh }
public enum ImportLayerMode { ByIfcType, ByIfcLayer, ByStorey, Single }
public enum ImportColorMode { Original, ByStorey, Single }

/// <summary>Opções da importação IFC (lembradas em %APPDATA%\PASB\import-settings.json).</summary>
public sealed class ImportSettings
{
    public string LastFolder { get; set; } = "";
    public ImportGeometry Geometry { get; set; } = ImportGeometry.Solid;
    public bool FailuresAsMesh { get; set; } = true;
    public bool Blocks { get; set; } = true;
    public ImportLayerMode LayerMode { get; set; } = ImportLayerMode.ByStorey;
    public string SingleLayer { get; set; } = "IFC_Import";
    public ImportColorMode ColorMode { get; set; } = ImportColorMode.ByStorey;
    public short SingleColor { get; set; } = 7;
    public bool IncludeOpenings { get; set; }
    public bool IncludeSpaces { get; set; }
    /// <summary>Cor (índice do AutoCAD) escolhida para cada nível, pelo nome do nível (vale entre arquivos).</summary>
    public Dictionary<string, short> StoreyColors { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true, Converters = { new JsonStringEnumConverter() } };

    public static string DefaultPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PASB", "import-settings.json");

    public static ImportSettings Load(string? path = null)
    {
        try
        {
            string p = path ?? DefaultPath;
            if (!File.Exists(p)) return new ImportSettings();
            ImportSettings s = JsonSerializer.Deserialize<ImportSettings>(File.ReadAllText(p), Json) ?? new ImportSettings();
            s.StoreyColors = new Dictionary<string, short>(s.StoreyColors ?? new(), StringComparer.OrdinalIgnoreCase);
            return s;
        }
        catch (Exception) { return new ImportSettings(); }
    }

    public void Save(string? path = null)
    {
        try
        {
            string p = path ?? DefaultPath;
            Directory.CreateDirectory(Path.GetDirectoryName(p)!);
            File.WriteAllText(p, JsonSerializer.Serialize(this, Json), new UTF8Encoding(false));
        }
        catch (Exception) { }
    }
}

/// <summary>Cores do AutoCAD (ACI) para exibir amostras na janela. O plugin pode trocar por valores exatos (<see cref="Resolver"/>).</summary>
public static class Aci
{
    public static Func<short, Color>? Resolver { get; set; }

    private static readonly Color[] Basic =
    {
        Color.Black, Color.FromArgb(255, 0, 0), Color.FromArgb(255, 255, 0), Color.FromArgb(0, 255, 0), Color.FromArgb(0, 255, 255),
        Color.FromArgb(0, 0, 255), Color.FromArgb(255, 0, 255), Color.FromArgb(255, 255, 255), Color.FromArgb(128, 128, 128), Color.FromArgb(192, 192, 192),
    };

    /// <summary>RGB aproximado do índice ACI (1–255), pela regra da paleta do AutoCAD.</summary>
    public static Color ToColor(short index)
    {
        if (Resolver != null) { try { return Resolver(index); } catch (Exception) { } }
        if (index >= 1 && index <= 9) return Basic[index];
        if (index >= 250 && index <= 255) { int g = 51 + (index - 250) * 41; g = Math.Min(255, index == 250 ? 51 : g); return Color.FromArgb(g, g, g); }
        if (index < 10 || index > 249) return Color.White;
        int hueStep = index / 10 - 1;            // 0..23 → 0..345°
        int v = index % 10;                      // 0,2,4,6,8 cheios; ímpares misturados com branco
        double hue = hueStep * 15.0;
        double value = v switch { 0 or 1 => 1.0, 2 or 3 => 0.8, 4 or 5 => 0.6, 6 or 7 => 0.5, _ => 0.3 };
        double sat = v % 2 == 0 ? 1.0 : 0.5;
        return FromHsv(hue, sat, value);
    }

    private static Color FromHsv(double h, double s, double v)
    {
        double c = v * s, x = c * (1 - Math.Abs(h / 60 % 2 - 1)), m = v - c;
        (double r, double g, double b) = h switch
        {
            < 60 => (c, x, 0.0), < 120 => (x, c, 0.0), < 180 => (0.0, c, x), < 240 => (0.0, x, c), < 300 => (x, 0.0, c), _ => (c, 0.0, x),
        };
        return Color.FromArgb((int)Math.Round((r + m) * 255), (int)Math.Round((g + m) * 255), (int)Math.Round((b + m) * 255));
    }
}
