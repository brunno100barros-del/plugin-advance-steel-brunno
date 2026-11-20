using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Pasb.Georeferencing;
using Pasb.IfcExport;
using Pasb.Model;
using Pasb.Ui;

namespace Pasb.Ui.Tests;

internal static class Program
{
    private static int _failed, _passed;

    [STAThread]
    private static int Main()
    {
        Run("números com vírgula ou ponto", () =>
        {
            True(ExportSettings.TryParseNumber("7400000,25", out double a) && a == 7400000.25, "vírgula");
            True(ExportSettings.TryParseNumber("500000.5", out double b) && b == 500000.5, "ponto");
            True(!ExportSettings.TryParseNumber("abc", out _), "inválido");
        });
        Run("configuração padrão: sem georreferência, todos os objetos", () =>
        {
            var s = new ExportSettings { OutputPath = @"C:\x\modelo.ifc" };
            True(s.Validate().Count == 0, string.Join("; ", s.Validate()));
            True(s.ToGeoParameters(0.001) is null, "sem geo");
            True(s.ToExportOptions(0.001).IncludedGroups!.Count == ObjectGroups.All.Length, "todos os grupos");
        });
        Run("validação: sem objetos, arquivo inválido e UTM implausível", () =>
        {
            var s = new ExportSettings { OutputPath = "saida.txt", Geo = GeoChoice.Sirgas, TargetX = 10, TargetY = 10 };
            foreach (var k in s.Groups.Keys.ToList()) s.Groups[k] = false;
            var e = s.Validate();
            True(e.Any(x => x.Contains("tipo de objeto")) && e.Any(x => x.Contains(".ifc")) && e.Any(x => x.Contains("Easting")), string.Join("; ", e));
        });
        Run("SIRGAS: parâmetros e azimuto convertido em rotação", () =>
        {
            var s = new ExportSettings
            {
                Geo = GeoChoice.Sirgas, UtmZone = 23, Hemisphere = Hemisphere.South, BaseX = 1000, BaseY = 2000,
                TargetX = 500000, TargetY = 7400000, TargetZ = 800, AngleMode = AngleInput.Azimuth, AngleDegrees = 30,
                OutputPath = @"C:\x\a.ifc",
            };
            GeoParameters g = s.ToGeoParameters(0.001)!;
            True(g.Mode == GeoMode.SirgasUtm && g.UtmZone == 23 && g.RotationDegrees == -30 && g.ModelPoint.X == 1000, "parâmetros");
            True(s.Validate().Count == 0, string.Join("; ", s.Validate()));
        });
        Run("coordenadas reais em milímetros viram metros", () =>
        {
            var s = new ExportSettings
            {
                Geo = GeoChoice.Local, LocalCrsName = "Obra", TargetUnit = TargetUnit.Millimeters,
                TargetX = 12500, TargetY = -3000, TargetZ = 800, OutputPath = @"C:\x\mm.ifc",
            };
            GeoParameters g = s.ToGeoParameters(0.001)!;
            True(Math.Abs(g.TargetPoint.X - 12.5) < 1e-12 && Math.Abs(g.TargetPoint.Y + 3) < 1e-12 && Math.Abs(g.TargetPoint.Z - 0.8) < 1e-12, "conversão");
            // UTM digitado em mm: 500 000 m = 500 000 000 mm deve continuar plausível.
            var u = new ExportSettings
            {
                Geo = GeoChoice.Sirgas, UtmZone = 22, TargetUnit = TargetUnit.Millimeters,
                TargetX = 500000000, TargetY = 7400000000, OutputPath = @"C:\x\mm.ifc",
            };
            True(u.Validate().Count == 0, string.Join("; ", u.Validate()));
            True(new ExportSettings().TargetUnit == TargetUnit.Meters, "padrão em metros");
        });
        Run("janela: unidade das coordenadas reais é lida e devolvida", () =>
        {
            var s = new ExportSettings { Geo = GeoChoice.Local, LocalCrsName = "L", TargetUnit = TargetUnit.Millimeters, TargetX = 1234.5, OutputPath = @"C:\x\u.ifc" };
            using var dlg = new ExportDialog(s);
            True(dlg.Collect(true) && dlg.Settings.TargetUnit == TargetUnit.Millimeters && dlg.Settings.TargetX == 1234.5, "milímetros preservado");
            ComboBox unit = (ComboBox)typeof(ExportDialog).GetField("_unit", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(dlg)!;
            unit.SelectedIndex = 0;
            True(dlg.Collect(true) && dlg.Settings.TargetUnit == TargetUnit.Meters, "troca para metros");
        });
        Run("modo local exige nome do sistema", () =>
        {
            var s = new ExportSettings { Geo = GeoChoice.Local, LocalCrsName = "", OutputPath = @"C:\x\a.ifc" };
            True(s.Validate().Any(x => x.Contains("nome")), "nome obrigatório");
        });
        Run("salvar e carregar configuração (ida e volta)", () =>
        {
            string p = Path.Combine(Path.GetTempPath(), "pasb-settings-" + Guid.NewGuid().ToString("N") + ".json");
            try
            {
                var s = new ExportSettings { ProjectName = "Obra Teste", Geo = GeoChoice.Local, LocalCrsName = "Eixo A", TargetX = 12.5, OutputPath = @"C:\x\b.ifc" };
                s.Groups[ObjectGroups.Welds] = false;
                s.Save(p);
                ExportSettings r = ExportSettings.Load(p);
                True(r.ProjectName == "Obra Teste" && r.Geo == GeoChoice.Local && r.LocalCrsName == "Eixo A" && r.TargetX == 12.5, "campos");
                True(!r.Groups[ObjectGroups.Welds] && r.Groups[ObjectGroups.Beams], "grupos");
            }
            finally { File.Delete(p); }
        });
        Run("arquivo de configuração corrompido volta ao padrão", () =>
        {
            string p = Path.Combine(Path.GetTempPath(), "pasb-bad-" + Guid.NewGuid().ToString("N") + ".json");
            File.WriteAllText(p, "{ isto não é json");
            try { True(ExportSettings.Load(p).Geo == GeoChoice.None, "padrão"); } finally { File.Delete(p); }
        });
        Run("janela: abre os valores e devolve o que foi lido", () =>
        {
            var s = new ExportSettings
            {
                ProjectName = "P1", Geo = GeoChoice.Sirgas, UtmZone = 24, TargetX = 500000, TargetY = 7400000.5,
                AngleDegrees = 12.25, OutputPath = @"C:\x\c.ifc",
            };
            s.Groups[ObjectGroups.Joints] = false;
            using var dlg = new ExportDialog(s, "0.4.0");
            True(dlg.Collect(validate: true), string.Join("; ", dlg.LastErrors));
            True(dlg.Settings.ProjectName == "P1" && dlg.Settings.UtmZone == 24 && dlg.Settings.TargetY == 7400000.5 && dlg.Settings.AngleDegrees == 12.25, "valores");
            True(!dlg.Settings.Groups[ObjectGroups.Joints], "grupo desmarcado preservado");
            // Grupos de opções independentes (bug visto na imagem da janela: Rotação desmarcava SIRGAS).
            True(dlg.Settings.Geo == GeoChoice.Sirgas && dlg.Settings.Format == "IFC4" && dlg.Settings.Scope == ExportScope.Model
                 && dlg.Settings.AngleMode == AngleInput.Rotation, "grupos de opção independentes");
            var s2 = new ExportSettings { Geo = GeoChoice.Local, LocalCrsName = "L", Scope = ExportScope.Selection, AngleMode = AngleInput.Azimuth, OutputPath = @"C:\x\e.ifc" };
            using var dlg2 = new ExportDialog(s2);
            True(dlg2.Collect(true) && dlg2.Settings.Geo == GeoChoice.Local && dlg2.Settings.Scope == ExportScope.Selection && dlg2.Settings.AngleMode == AngleInput.Azimuth,
                "combinação local + seleção + azimute");
        });
        Run("janela: valor digitado inválido gera erro", () =>
        {
            var s = new ExportSettings { Geo = GeoChoice.Local, LocalCrsName = "X", OutputPath = @"C:\x\c.ifc" };
            using var dlg = new ExportDialog(s);
            TextBox tx = (TextBox)typeof(ExportDialog).GetField("_tx", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(dlg)!;
            tx.Text = "abc";
            True(!dlg.Collect(validate: true) && dlg.LastErrors.Any(e => e.Contains("X / E")), "erro de número");
        });
        Run("exportador respeita o filtro de objetos", () =>
        {
            const string run = """{"insunits":"Millimeters"}""";
            const string plate = """{"handle":"C7","clrType":"A.Plate","inheritance":["Plate","PlateBase","AtomicElement","FilerObject"],"properties":[]}""";
            const string weld = """{"handle":"D5","clrType":"A.WeldLine","inheritance":["WeldLine","WeldPattern","AtomicElement","FilerObject"],"properties":[]}""";
            Extraction x = DiagnosticImporter.Import(run, new[] { plate, weld });
            var s = new ExportSettings { OutputPath = @"C:\x\d.ifc" };
            s.Groups[ObjectGroups.Welds] = false;
            string ifc = IfcExporter.Export(x, s.ToExportOptions(0.001), out ExportReport r);
            True(ifc.Contains("=IFCPLATE(") && !ifc.Contains("=IFCFASTENER("), "solda filtrada");
            True(r.SkippedByCategory["filtrado:weld"] == 1, "relatório");
            True(ifc.Contains("IFCSITE(") && ifc.Contains("'Site'"), "nome do site");
        });

        // Opcional: imagem da janela para conferência visual (PASB_SCREENSHOT=caminho.png).
        if (Environment.GetEnvironmentVariable("PASB_SCREENSHOT") is { Length: > 0 } shot)
        {
            var s = new ExportSettings { Geo = GeoChoice.Sirgas, UtmZone = 22, TargetX = 500000, TargetY = 7400000, TargetZ = 800, BaseX = 1000, BaseY = 2000, OutputPath = @"C:\Users\...\Documents\Portal-copia.ifc" };
            using var dlg = new ExportDialog(s, "0.4.0");
            dlg.StartPosition = FormStartPosition.Manual; dlg.Location = new System.Drawing.Point(-3000, -3000); dlg.ShowInTaskbar = false;
            dlg.Show(); Application.DoEvents();
            using var bmp = new System.Drawing.Bitmap(dlg.Width, dlg.Height);
            dlg.DrawToBitmap(bmp, new System.Drawing.Rectangle(0, 0, dlg.Width, dlg.Height));
            bmp.Save(shot, System.Drawing.Imaging.ImageFormat.Png);
            dlg.Close();
            Console.WriteLine("imagem: " + shot);
        }

        Console.WriteLine($"{_passed} passaram, {_failed} falharam");
        return _failed == 0 ? 0 : 1;
    }

    private static void Run(string name, Action t)
    {
        try { t(); _passed++; Console.WriteLine("ok    " + name); }
        catch (Exception ex) { _failed++; Console.WriteLine("FALHA " + name + ": " + ex.Message); }
    }

    private static void True(bool c, string what) { if (!c) throw new Exception(what); }
}
