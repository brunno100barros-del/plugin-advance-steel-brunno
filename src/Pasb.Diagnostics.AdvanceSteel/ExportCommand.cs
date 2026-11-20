using System;
using System.IO;
using System.Text;
using System.Text.Json;
using Autodesk.AdvanceSteel.CADAccess;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Pasb.Georeferencing;
using Pasb.IfcExport;
using Pasb.Model;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;
using AcDoc = Autodesk.AutoCAD.ApplicationServices.Document;
using AsId = Autodesk.AdvanceSteel.CADLink.Database.ObjectId;

[assembly: CommandClass(typeof(Pasb.Diagnostics.AdvanceSteel.ExportCommand))]

namespace Pasb.Diagnostics.AdvanceSteel;

/// <summary>
/// PASB_EXPORTAR_IFC (Etapa 6): lê o modelo com o diagnóstico validado, converte para o modelo neutro
/// e grava o IFC4 georreferenciado. Perguntas na linha de comando. Não altera o modelo.
/// </summary>
public sealed class ExportCommand
{
    [CommandMethod("PASB", "PASB_EXPORTAR_IFC", CommandFlags.Modal)]
    public void ExportIfc()
    {
        AcDoc? doc = AcApp.DocumentManager.MdiActiveDocument;
        if (doc == null) return;
        Editor ed = doc.Editor;
        try
        {
            string unitName = doc.Database.Insunits.ToString();
            if (!string.Equals(unitName, "Millimeters", StringComparison.OrdinalIgnoreCase))
                ed.WriteMessage($"\nPASB: ATENÇÃO: unidade do desenho '{unitName}' não validada (esperado milímetros). A geometria pode sair sem conversão.");
            const double unitToMeters = 0.001;

            // 1. Georreferenciamento
            GeoParameters? geo = AskGeoreference(ed, unitToMeters);
            if (geo == null && Cancelled) { ed.WriteMessage("\nPASB: exportação cancelada.\n"); return; }

            // 2. Arquivo de saída
            string defaultName = Path.ChangeExtension(Path.GetFileName(doc.Name), ".ifc");
            var save = new PromptSaveFileOptions("\nSalvar IFC como: ")
            {
                Filter = "IFC (*.ifc)|*.ifc",
                InitialFileName = defaultName,
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                DialogCaption = "Exportar IFC4 (PASB)",
            };
            PromptFileNameResult file = ed.GetFileNameForSave(save);
            if (file.Status != PromptStatus.OK) { ed.WriteMessage("\nPASB: exportação cancelada.\n"); return; }

            // 3. Leitura do modelo (mesmo caminho validado do PASB_DIAG_ALL: escrita descartada, DBMOD conferido)
            var (outDir, safe) = Commands.Run(ed, "PASB_EXPORTAR_IFC", () =>
            {
                DatabaseManager.GetModelObjectIds(out AsId[] ids);
                return (ids ?? Array.Empty<AsId>(), 0);
            });
            if (!safe)
            {
                ed.WriteMessage("\nPASB: exportação interrompida porque a verificação de integridade do modelo falhou. Feche o desenho SEM SALVAR.\n");
                return;
            }

            // 4. Modelo neutro + IFC
            Extraction x = DiagnosticImporter.ImportDirectory(outDir);
            x.SourceDocument.Name = Path.GetFileName(doc.Name);
            string ifc = IfcExporter.Export(x, new ExportOptions(Path.GetFileNameWithoutExtension(doc.Name), Path.GetFileName(file.StringResult), geo), out ExportReport report);
            File.WriteAllText(file.StringResult, ifc, new UTF8Encoding(false));
            string reportPath = Path.ChangeExtension(file.StringResult, ".report.json");
            File.WriteAllText(reportPath, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));

            ed.WriteMessage($"\nPASB: IFC gravado em {file.StringResult}");
            foreach (var kv in report.ExportedByIfcClass) ed.WriteMessage($"\nPASB:   {kv.Key}: {kv.Value}");
            ed.WriteMessage($"\nPASB: {report.Properties} propriedades em {report.PropertySets} Psets. Relatório: {reportPath}\n");
        }
        catch (System.Exception ex)
        {
            ed.WriteMessage($"\nPASB: PASB_EXPORTAR_IFC falhou: {ex.GetType().Name}: {ex.Message}\n");
        }
    }

    private static bool Cancelled;

    /// <summary>Pergunta o modo e os parâmetros. Retorna null para "sem georreferenciamento" (Cancelled = false) ou cancelamento (Cancelled = true).</summary>
    private static GeoParameters? AskGeoreference(Editor ed, double unitToMeters)
    {
        Cancelled = false;
        var mode = new PromptKeywordOptions("\nGeorreferenciamento [Sirgas/Local/Nenhum] <Sirgas>: ") { AllowNone = true };
        mode.Keywords.Add("Sirgas"); mode.Keywords.Add("Local"); mode.Keywords.Add("Nenhum");
        PromptResult m = ed.GetKeywords(mode);
        if (m.Status == PromptStatus.Cancel) { Cancelled = true; return null; }
        string kw = m.Status == PromptStatus.None || string.IsNullOrEmpty(m.StringResult) ? "Sirgas" : m.StringResult;
        if (kw == "Nenhum") return null;

        while (true)
        {
            PromptPointResult pt = ed.GetPoint(new PromptPointOptions("\nClique no ponto de controle (ponto conhecido) no modelo: "));
            if (pt.Status != PromptStatus.OK) { Cancelled = true; return null; }
            Point3d w = pt.Value.TransformBy(ed.CurrentUserCoordinateSystem);   // UCS → WCS
            var model = new Pasb.Georeferencing.Point3(w.X, w.Y, w.Z);

            GeoParameters p;
            if (kw == "Sirgas")
            {
                int? zone = AskInt(ed, "\nZona UTM (ex.: 22): ", 22); if (zone is null) { Cancelled = true; return null; }
                var hemi = new PromptKeywordOptions("\nHemisfério [Sul/Norte] <Sul>: ") { AllowNone = true };
                hemi.Keywords.Add("Sul"); hemi.Keywords.Add("Norte");
                PromptResult h = ed.GetKeywords(hemi);
                if (h.Status == PromptStatus.Cancel) { Cancelled = true; return null; }
                Hemisphere hs = h.StringResult == "Norte" ? Hemisphere.North : Hemisphere.South;
                double? e = AskDouble(ed, "\nCoordenada E (Leste) do ponto, em metros: "); if (e is null) { Cancelled = true; return null; }
                double? n = AskDouble(ed, "\nCoordenada N (Norte) do ponto, em metros: "); if (n is null) { Cancelled = true; return null; }
                double? hh = AskDouble(ed, "\nCota (altitude) do ponto, em metros <0>: ", 0); if (hh is null) { Cancelled = true; return null; }
                double? rot = AskRotation(ed); if (rot is null) { Cancelled = true; return null; }
                p = new GeoParameters(GeoMode.SirgasUtm, model, new Pasb.Georeferencing.Point3(e.Value, n.Value, hh.Value), rot.Value, unitToMeters, zone, hs);
            }
            else
            {
                PromptResult name = ed.GetString(new PromptStringOptions("\nNome do sistema de coordenadas da obra: ") { AllowSpaces = true });
                if (name.Status != PromptStatus.OK) { Cancelled = true; return null; }
                double? x = AskDouble(ed, "\nCoordenada X do ponto no sistema da obra, em metros: "); if (x is null) { Cancelled = true; return null; }
                double? y = AskDouble(ed, "\nCoordenada Y do ponto no sistema da obra, em metros: "); if (y is null) { Cancelled = true; return null; }
                double? z = AskDouble(ed, "\nCoordenada Z do ponto, em metros <0>: ", 0); if (z is null) { Cancelled = true; return null; }
                double? rot = AskRotation(ed); if (rot is null) { Cancelled = true; return null; }
                p = new GeoParameters(GeoMode.LocalXyz, model, new Pasb.Georeferencing.Point3(x.Value, y.Value, z.Value), rot.Value, unitToMeters,
                    LocalCrsName: name.StringResult);
            }

            var errors = Georeference.Validate(p);
            if (errors.Count == 0)
            {
                ed.WriteMessage($"\nPASB: ponto de controle ({w.X:0.###}, {w.Y:0.###}, {w.Z:0.###}) → ({p.TargetPoint.X:0.###}, {p.TargetPoint.Y:0.###}, {p.TargetPoint.Z:0.###}) m, rotação {p.RotationDegrees:0.####}°.");
                return p;
            }
            foreach (string err in errors) ed.WriteMessage("\nPASB: " + err);
            ed.WriteMessage("\nPASB: corrija os dados e informe novamente.");
        }
    }

    /// <summary>Ângulo: rotação anti-horária do X do modelo até o Leste, ou azimute horário do +Y do modelo a partir do Norte.</summary>
    private static double? AskRotation(Editor ed)
    {
        var o = new PromptKeywordOptions("\nComo informar o ângulo [Rotacao/Azimute] <Rotacao>: ") { AllowNone = true };
        o.Keywords.Add("Rotacao"); o.Keywords.Add("Azimute");
        PromptResult r = ed.GetKeywords(o);
        if (r.Status == PromptStatus.Cancel) return null;
        if (r.StringResult == "Azimute")
        {
            double? az = AskDouble(ed, "\nAzimute do eixo +Y do modelo (graus, horário a partir do Norte) <0>: ", 0);
            return az is null ? null : GeoParameters.RotationFromModelNorthAzimuth(az.Value);
        }
        return AskDouble(ed, "\nRotação do eixo X do modelo até o Leste (graus, anti-horário) <0>: ", 0);
    }

    private static double? AskDouble(Editor ed, string msg, double? def = null)
    {
        var o = new PromptDoubleOptions(msg) { AllowNone = def.HasValue };
        if (def.HasValue) { o.DefaultValue = def.Value; o.UseDefaultValue = true; }
        PromptDoubleResult r = ed.GetDouble(o);
        if (r.Status == PromptStatus.OK) return r.Value;
        if (r.Status == PromptStatus.None && def.HasValue) return def.Value;
        return null;
    }

    private static int? AskInt(Editor ed, string msg, int def)
    {
        var o = new PromptIntegerOptions(msg) { AllowNone = true, DefaultValue = def, UseDefaultValue = true, LowerLimit = 1, UpperLimit = 60 };
        PromptIntegerResult r = ed.GetInteger(o);
        if (r.Status == PromptStatus.OK) return r.Value;
        if (r.Status == PromptStatus.None) return def;
        return null;
    }
}
