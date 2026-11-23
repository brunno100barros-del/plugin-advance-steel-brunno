using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using Autodesk.AdvanceSteel.CADAccess;
using Autodesk.AdvanceSteel.ConstructionTypes;
using Autodesk.AdvanceSteel.DocumentManagement;
using Autodesk.AdvanceSteel.Modelling;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using Pasb.Connector;
using Pasb.Model;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;
using AcDoc = Autodesk.AutoCAD.ApplicationServices.Document;
using AsId = Autodesk.AdvanceSteel.CADLink.Database.ObjectId;

[assembly: CommandClass(typeof(Pasb.Diagnostics.AdvanceSteel.NumberingCommand))]

namespace Pasb.Diagnostics.AdvanceSteel;

/// <summary>
/// PASB_NUMERAR_ITENS (docs/NUMBERING.md): numeração itemizada — peças iguais recebem a mesma marca.
/// 1) lê o modelo pelo caminho validado (transação descartada, DBMOD conferido); 2) janela com prévia e filtros;
/// 3) confirmação; 4) grava SÓ o campo escolhido das peças numeradas, numa transação com Commit, e salva um CSV de auditoria
/// (valor antigo → novo) em Documentos\PASB-Numeracao. Ctrl+Z desfaz.
/// </summary>
public sealed class NumberingCommand
{
    [CommandMethod("PASB", "PASB_NUMERAR_ITENS", CommandFlags.Modal)]
    public void Number()
    {
        AcDoc? doc = AcApp.DocumentManager.MdiActiveDocument;
        if (doc == null) return;
        Editor ed = doc.Editor;
        try
        {
            // 1) Leitura sem alterar o modelo
            var (dir, safe) = Commands.Run(ed, "PASB_NUMERAR_ITENS", () =>
            {
                DatabaseManager.GetModelObjectIds(out AsId[] ids);
                return (ids ?? Array.Empty<AsId>(), 0);
            }, Pasb.Diagnostics.Core.ReadProfile.Export);
            if (!safe) { ed.WriteMessage("\nPASB: numeração cancelada: a verificação da leitura falhou. Feche o desenho SEM SALVAR.\n"); return; }
            Extraction x = DiagnosticImporter.ImportDirectory(dir);

            // 2) Janela
            ItemOptions options = NumberingSettings.Load();
            ItemResult result;
            using (var dlg = new NumberingDialog(x, options, Version))
            {
                if (Autodesk.AutoCAD.ApplicationServices.Application.ShowModalDialog(dlg) != DialogResult.OK) { ed.WriteMessage("\nPASB: numeração cancelada.\n"); return; }
                result = dlg.Result;
            }
            NumberingSettings.Save(options);

            // 3) Confirmação
            string field = ItemNumbering.TargetName(options.Target);
            string msg = $"Gravar {result.Rows.Count} marca(s) em {result.Parts} peça(s), no campo:\n\n    {field}\n\n" +
                         $"{result.Excluded.Count} peça(s) ficam de fora e não são alteradas.\n" +
                         "O valor anterior de cada peça vai para um registro CSV em Documentos\\PASB-Numeracao.\n" +
                         "Ctrl+Z desfaz.\n\nContinuar?";
            if (MessageBox.Show(msg, "PASB — Confirmar numeração", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
            { ed.WriteMessage("\nPASB: numeração cancelada; nada foi gravado.\n"); return; }

            // 4) Gravação
            var assign = result.Assignments(options.Prefix);
            var log = new StringBuilder("handle;tipo;campo;valor_anterior;valor_novo;resultado\n");
            int ok = 0, failed = 0;
            var typeByHandle = result.Rows.SelectMany(r => r.Handles.Select(h => (h, r.Type))).ToDictionary(t => t.h, t => t.Type, StringComparer.OrdinalIgnoreCase);
            using (doc.LockDocument())
            using (var access = new DocumentAccess(null, false))
            {
                DatabaseManager.GetModelObjectIds(out AsId[] ids);
                foreach (AsId id in ids ?? Array.Empty<AsId>())
                {
                    string? h = SafeHandle(id);
                    if (h == null || !assign.TryGetValue(h, out var a)) continue;
                    string before = "", after = "";
                    try
                    {
                        if (DatabaseManager.Open(id) is not AtomicElement ae) throw new InvalidOperationException("objeto não é uma peça");
                        (before, after) = Write(ae, options.Target, a.mark, a.prefix, a.number);
                        ok++;
                        log.Append(Csv(h, typeByHandle.GetValueOrDefault(h, ""), field, before, after, "ok"));
                    }
                    catch (System.Exception ex)
                    {
                        failed++;
                        log.Append(Csv(h, typeByHandle.GetValueOrDefault(h, ""), field, before, a.mark, "falhou: " + ex.Message));
                    }
                }
                access.Commit();
            }

            string logPath = SaveLog(log.ToString(), doc.Name);
            ed.WriteMessage($"\nPASB: numeração concluída: {ok} peça(s) gravada(s) em \"{field}\"" + (failed > 0 ? $", {failed} falha(s)" : "") + ".");
            ed.WriteMessage($"\nPASB: registro (valor anterior → novo): {logPath}");
            ed.WriteMessage("\nPASB: Ctrl+Z desfaz a numeração.\n");
            if (failed > 0)
                MessageBox.Show($"{failed} peça(s) não puderam ser numeradas. Veja o registro:\n{logPath}", "PASB — Numeração", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        catch (System.Exception ex)
        {
            ed.WriteMessage($"\nPASB: PASB_NUMERAR_ITENS falhou: {ex.GetType().Name}: {ex.Message}\n");
        }
    }

    /// <summary>Grava o campo escolhido e devolve (valor anterior, valor novo).</summary>
    private static (string before, string after) Write(AtomicElement ae, ItemTarget target, string mark, string prefix, string number)
    {
        switch (target)
        {
            case ItemTarget.Preliminary:
            {
                string before = (ae.PreliminaryPartPrefix ?? "") + (ae.PreliminaryPartNumber ?? "");
                ae.PreliminaryPartPrefix = prefix;
                ae.PreliminaryPartNumber = number;
                return (before, prefix + number);
            }
            case ItemTarget.ItemNumber:
            {
                string before = ae.ItemNumber ?? "";
                ae.ItemNumber = mark;
                return (before, mark);
            }
            case ItemTarget.Note:
            {
                string before = ae.Note ?? "";
                ae.Note = mark;
                return (before, mark);
            }
            default:
            {
                int idx = (int)target - (int)ItemTarget.UserAttribute1;   // 0..9
                string before = ae.GetUserAttribute(idx) ?? "";
                ae.SetUserAttribute(idx, mark);
                return (before, mark);
            }
        }
    }

    private static string Csv(params string[] v) => string.Join(";", v.Select(s => "\"" + (s ?? "").Replace("\"", "\"\"") + "\"")) + "\n";

    private static string SaveLog(string content, string docName)
    {
        string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "PASB-Numeracao");
        Directory.CreateDirectory(dir);
        string name = Path.GetFileNameWithoutExtension(docName);
        string path = Path.Combine(dir, $"{DateTime.Now:yyyyMMdd-HHmmss}-{string.Concat(name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c))}.csv");
        File.WriteAllText(path, content, new UTF8Encoding(true));   // BOM: o Excel abre os acentos corretamente
        return path;
    }

    private static string? SafeHandle(AsId id)
    {
        try { return id.Handle; } catch (System.Exception) { return null; }
    }

    private static string Version =>
        typeof(NumberingCommand).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion?.Split('+')[0] ?? "";
}
