using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Pasb.Georeferencing;
using Pasb.IfcExport;
using Pasb.Model;

// Uso:
//   ToIfc <pasta-do-diagnostico> <saida.ifc> [opções de georreferenciamento]
// Modo A (SIRGAS 2000 / UTM):  --utm 22S --model X,Y,Z --target E,N,H --rot GRAUS
// Modo B (X, Y, Z próprio):    --local "Nome do sistema" --model X,Y,Z --target X,Y,Z --rot GRAUS
//   --model em unidades do desenho; --target em metros; --rot anti-horário a partir do X do modelo (ver GEOREFERENCING.md).
//   --azimuth GRAUS pode substituir --rot (azimute horário do +Y do modelo a partir do Norte).
if (args.Length < 2 || !Directory.Exists(args[0]))
{
    Console.Error.WriteLine("Uso: ToIfc <pasta-do-diagnostico> <saida.ifc> [--utm 22S | --local NOME] --model X,Y,Z --target E,N,H [--rot G | --azimuth G]");
    return 1;
}

string? Opt(string name) { int i = Array.IndexOf(args, name); return i >= 0 && i + 1 < args.Length ? args[i + 1] : null; }
Point3 P3(string s) { double[] v = s.Split(',').Select(t => double.Parse(t, CultureInfo.InvariantCulture)).ToArray(); return new Point3(v[0], v[1], v.Length > 2 ? v[2] : 0); }

Extraction x = DiagnosticImporter.ImportDirectory(args[0]);
double unit = x.Elements.Select(e => e.Geometry?.ModelUnitToMeters).FirstOrDefault(v => v.HasValue) ?? 0.001;

GeoParameters? geo = null;
string? utm = Opt("--utm"), local = Opt("--local");
if (utm is not null || local is not null)
{
    double rot = Opt("--rot") is { } r ? double.Parse(r, CultureInfo.InvariantCulture)
               : Opt("--azimuth") is { } a ? GeoParameters.RotationFromModelNorthAzimuth(double.Parse(a, CultureInfo.InvariantCulture)) : 0;
    Point3 model = P3(Opt("--model") ?? "0,0,0"), target = P3(Opt("--target") ?? throw new ArgumentException("--target obrigatório"));
    geo = utm is not null
        ? new GeoParameters(GeoMode.SirgasUtm, model, target, rot, unit, int.Parse(utm[..^1], CultureInfo.InvariantCulture),
            char.ToUpperInvariant(utm[^1]) == 'S' ? Hemisphere.South : Hemisphere.North)
        : new GeoParameters(GeoMode.LocalXyz, model, target, rot, unit, LocalCrsName: local);
}

string ifc = IfcExporter.Export(x, new ExportOptions(FileName: Path.GetFileName(args[1]), Geo: geo), out ExportReport report);
File.WriteAllText(args[1], ifc, new UTF8Encoding(false));
string reportPath = Path.ChangeExtension(args[1], ".report.json");
File.WriteAllText(reportPath, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));

Console.WriteLine($"IFC: {args[1]} ({ifc.Length:N0} caracteres)");
foreach (var kv in report.ExportedByIfcClass) Console.WriteLine($"  {kv.Key,-24} {kv.Value}");
foreach (var kv in report.SkippedByCategory) Console.WriteLine($"  ignorado: {kv.Key} {kv.Value}");
Console.WriteLine($"Psets: {report.PropertySets} | propriedades: {report.Properties} | sem valor: {report.PropertiesWithoutValue}");
Console.WriteLine("Relatório: " + reportPath);
return 0;
