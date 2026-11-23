using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Windows.Forms;
using Autodesk.AdvanceSteel.CADAccess;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Pasb.IfcExport;
using Pasb.Model;
using Pasb.Ui;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;
using AcDoc = Autodesk.AutoCAD.ApplicationServices.Document;
using AsId = Autodesk.AdvanceSteel.CADLink.Database.ObjectId;

[assembly: CommandClass(typeof(Pasb.Diagnostics.AdvanceSteel.ExportCommand))]

namespace Pasb.Diagnostics.AdvanceSteel;

/// <summary>
/// PASB_EXPORTAR_IFC: abre a janela de exportação, permite escolher o ponto base no modelo,
/// lê o modelo pelo caminho validado do diagnóstico e grava o IFC4. Não altera o modelo.
/// </summary>
public sealed class ExportCommand
{
    [CommandMethod("PASB", "PASB_EXPORTAR_IFC", CommandFlags.Modal | CommandFlags.UsePickSet)]
    public void ExportIfc()
    {
        AcDoc? doc = AcApp.DocumentManager.MdiActiveDocument;
        if (doc == null) return;
        Editor ed = doc.Editor;
        try
        {
            double unit = UnitToMeters(doc.Database.Insunits.ToString(), out bool unitKnown);
            if (!unitKnown)
                ed.WriteMessage($"\nPASB: ATENÇÃO: unidade do desenho '{doc.Database.Insunits}' não validada; assumindo milímetros.");

            ExportSettings settings = ExportSettings.Load();
            if (string.IsNullOrWhiteSpace(settings.OutputPath) || !Directory.Exists(Path.GetDirectoryName(settings.OutputPath)))
                settings.OutputPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    Path.ChangeExtension(Path.GetFileName(doc.Name), ".ifc"));
            else
                settings.OutputPath = Path.Combine(Path.GetDirectoryName(settings.OutputPath)!, Path.ChangeExtension(Path.GetFileName(doc.Name), ".ifc"));

            // Janela ↔ seleção do ponto base (a janela fecha, o usuário clica, a janela reabre)
            while (true)
            {
                using var dlg = new ExportDialog(settings, Version);
                DialogResult res = Autodesk.AutoCAD.ApplicationServices.Application.ShowModalDialog(dlg);
                if (res == DialogResult.Retry && dlg.PickRequested)
                {
                    PromptPointResult pt = ed.GetPoint(new PromptPointOptions("\nPASB: clique no ponto base (ponto de coordenada conhecida): "));
                    if (pt.Status == PromptStatus.OK)
                    {
                        Point3d w = pt.Value.TransformBy(ed.CurrentUserCoordinateSystem);   // UCS → WCS
                        settings.BaseX = w.X; settings.BaseY = w.Y; settings.BaseZ = w.Z;
                        ed.WriteMessage($"\nPASB: ponto base = ({w.X:0.###}, {w.Y:0.###}, {w.Z:0.###})");
                    }
                    continue;
                }
                if (res != DialogResult.OK) { ed.WriteMessage("\nPASB: exportação cancelada.\n"); return; }
                break;
            }
            settings.Save();

            // Objetos: modelo inteiro ou seleção
            Func<(AsId[] ids, int nonSteel)> collect;
            if (settings.Scope == ExportScope.Selection)
            {
                PromptSelectionResult sel = ed.SelectImplied();
                if (sel.Status != PromptStatus.OK || sel.Value == null)
                    sel = ed.GetSelection(new PromptSelectionOptions { MessageForAdding = "\nPASB: selecione os objetos para exportar: " });
                if (sel.Status != PromptStatus.OK || sel.Value == null) { ed.WriteMessage("\nPASB: nenhum objeto selecionado.\n"); return; }
                var cadIds = sel.Value.GetObjectIds();
                collect = () => Commands.FromCad(cadIds);
            }
            else
            {
                collect = () => { DatabaseManager.GetModelObjectIds(out AsId[] ids); return (ids ?? Array.Empty<AsId>(), 0); };
            }

            // Leitura (escrita descartada, DBMOD conferido) → modelo neutro → IFC.
            // Perfil de exportação: sem gravação física no disco a cada leitura e sem as leituras caras (docs/EXPORT-PERFORMANCE.md);
            // resumida: só as propriedades principais. Sempre com o corpo sólido real de cada peça (docs/IFC-BODY.md).
            var profile = settings.SummaryProperties ? Pasb.Diagnostics.Core.ReadProfile.ExportSummary : Pasb.Diagnostics.Core.ReadProfile.Export;
            ed.WriteMessage(settings.SummaryProperties ? "\nPASB: exportação resumida (propriedades principais)." : "\nPASB: exportação completa (todas as propriedades).");
            var total = Stopwatch.StartNew();
            var (outDir, safe) = Commands.Run(ed, "PASB_EXPORTAR_IFC", collect, profile, readBody: true);
            long readMs = total.ElapsedMilliseconds;
            if (!safe)
            {
                ed.WriteMessage("\nPASB: exportação interrompida: a verificação de integridade do modelo falhou. Feche o desenho SEM SALVAR.\n");
                return;
            }
            ed.WriteMessage("\nPASB: convertendo para o modelo neutro...");
            Extraction x = DiagnosticImporter.ImportDirectory(outDir);
            x.SourceDocument.Name = Path.GetFileName(doc.Name);
            long importMs = total.ElapsedMilliseconds - readMs;
            ed.WriteMessage("\nPASB: gerando o IFC...");
            string ifc = IfcExporter.Export(x, settings.ToExportOptions(unit), out ExportReport report);
            long ifcMs = total.ElapsedMilliseconds - readMs - importMs;
            File.WriteAllText(settings.OutputPath, ifc, new UTF8Encoding(false));
            long writeMs = total.ElapsedMilliseconds - readMs - importMs - ifcMs;
            report.TimingsMs["leitura"] = readMs;
            report.TimingsMs["conversao"] = importMs;
            report.TimingsMs["ifc"] = ifcMs;
            report.TimingsMs["gravacao"] = writeMs;
            report.TimingsMs["total"] = total.ElapsedMilliseconds;
            string reportPath = Path.ChangeExtension(settings.OutputPath, ".report.json");
            File.WriteAllText(reportPath, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));

            var summary = new StringBuilder();
            summary.AppendLine("IFC gravado em:").AppendLine(settings.OutputPath).AppendLine();
            foreach (var kv in report.ExportedByIfcClass) summary.AppendLine($"{kv.Key}: {kv.Value}");
            foreach (var kv in report.SkippedByCategory) summary.AppendLine($"Não exportado ({kv.Key}): {kv.Value}");
            summary.AppendLine().AppendLine($"{report.Properties} propriedades em {report.PropertySets} Psets.");
            if (report.Georeference != null) summary.AppendLine($"Georreferência: {report.Georeference["crs"]}");
            summary.AppendLine($"Tempo: {Commands.Clock(TimeSpan.FromMilliseconds(total.ElapsedMilliseconds))} " +
                $"(leitura {Commands.Clock(TimeSpan.FromMilliseconds(readMs))}, conversão {Commands.Clock(TimeSpan.FromMilliseconds(importMs))}, " +
                $"IFC {Commands.Clock(TimeSpan.FromMilliseconds(ifcMs + writeMs))}).");
            ed.WriteMessage("\nPASB: " + summary.ToString().Replace(Environment.NewLine, "\nPASB: ") + "\n");
            MessageBox.Show(summary.ToString(), "PASB — Exportação concluída", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (System.Exception ex)
        {
            ed.WriteMessage($"\nPASB: PASB_EXPORTAR_IFC falhou: {ex.GetType().Name}: {ex.Message}\n");
        }
    }

    private static string Version =>
        typeof(ExportCommand).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion?.Split('+')[0] ?? "";

    /// <summary>INSUNITS → metros. Só milímetros foi validado nos testes; os demais são conversões diretas.</summary>
    internal static double UnitToMeters(string insunits, out bool validated)
    {
        validated = string.Equals(insunits, "Millimeters", StringComparison.OrdinalIgnoreCase);
        return insunits switch
        {
            "Millimeters" => 0.001, "Centimeters" => 0.01, "Meters" => 1.0, "Inches" => 0.0254, "Feet" => 0.3048,
            _ => 0.001,
        };
    }
}
