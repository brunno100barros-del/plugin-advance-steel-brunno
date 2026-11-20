using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json.Nodes;
using Autodesk.AdvanceSteel.CADAccess;
using Autodesk.AdvanceSteel.DocumentManagement;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using Pasb.Diagnostics.Core;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;
using AcDoc = Autodesk.AutoCAD.ApplicationServices.Document;
using AcId = Autodesk.AutoCAD.DatabaseServices.ObjectId;
using AsId = Autodesk.AdvanceSteel.CADLink.Database.ObjectId;

namespace Pasb.Diagnostics.AdvanceSteel;

/// <summary>
/// Comandos do diagnóstico (docs/DIAGNOSTIC-DESIGN.md, seções 3 e 4). Todos somente leitura.
/// </summary>
public sealed class Commands
{
    private const string Group = "PASB";

    [CommandMethod(Group, "PASB_DIAG_VERSION", CommandFlags.Modal)]
    public void Version()
    {
        Guard("PASB_DIAG_VERSION", ed =>
            ed.WriteMessage($"\nPASB Diagnostics {DiagnosticVersion} (somente leitura).\n"));
    }

    [CommandMethod(Group, "PASB_DIAG_SEL", CommandFlags.Modal | CommandFlags.UsePickSet)]
    public void DiagnoseSelection()
    {
        Guard("PASB_DIAG_SEL", ed =>
        {
            PromptSelectionResult sel = ed.SelectImplied();
            if (sel.Status != PromptStatus.OK || sel.Value == null)
                sel = ed.GetSelection(new PromptSelectionOptions { MessageForAdding = "\nSelecione os objetos do Advance Steel para diagnosticar: " });
            if (sel.Status != PromptStatus.OK || sel.Value == null)
            {
                ed.WriteMessage("\nPASB: nenhum objeto selecionado.\n");
                return;
            }
            Run(ed, "PASB_DIAG_SEL", () => FromCad(sel.Value.GetObjectIds()));
        });
    }

    [CommandMethod(Group, "PASB_DIAG_ALL", CommandFlags.Modal)]
    public void DiagnoseAll()
    {
        Guard("PASB_DIAG_ALL", ed => Run(ed, "PASB_DIAG_ALL", () =>
        {
            // Sem filtro de categoria: todos os objetos do Advance Steel no modelo.
            DatabaseManager.GetModelObjectIds(out AsId[] ids);
            return (ids ?? Array.Empty<AsId>(), 0);
        }));
    }

    private static string DiagnosticVersion =>
        typeof(Commands).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? typeof(Commands).Assembly.GetName().Version?.ToString() ?? "?";

    /// <summary>Isola qualquer falha: mensagem na linha de comando, sem derrubar o Advance Steel (seção 9.2).</summary>
    private static void Guard(string command, Action<Editor> body)
    {
        AcDoc? doc = AcApp.DocumentManager.MdiActiveDocument;
        if (doc == null) return;
        try
        {
            body(doc.Editor);
        }
        catch (System.Exception ex)
        {
            doc.Editor.WriteMessage($"\nPASB: {command} falhou: {ex.GetType().Name}: {ex.Message}\n");
        }
    }

    /// <summary>Converte a seleção do AutoCAD em objetos do Advance Steel; entidades comuns são contadas e ignoradas.</summary>
    private static (AsId[] ids, int nonSteel) FromCad(AcId[] cadIds)
    {
        var result = new List<AsId>();
        int nonSteel = 0;
        foreach (AcId cadId in cadIds)
        {
            AsId? filerId = null;
            try { filerId = DatabaseManager.GetFilerObjectId(new AsId(cadId.OldIdPtr)); } catch (System.Exception) { }
            if (filerId == null || filerId.IsNull()) nonSteel++;
            else result.Add(filerId);
        }
        return (result.ToArray(), nonSteel);
    }

    private static void Run(Editor ed, string command, Func<(AsId[] ids, int nonSteel)> collect)
    {
        AcDoc doc = AcApp.DocumentManager.MdiActiveDocument;
        DateTime startedLocal = DateTime.Now;
        var clock = Stopwatch.StartNew();
        object? dbmodBefore = SysVar("DBMOD");

        var run = new JsonObject
        {
            ["tool"] = "PASB Diagnostics",
            ["toolVersion"] = DiagnosticVersion,
            ["command"] = command,
            ["startedUtc"] = startedLocal.ToUniversalTime().ToString("O"),
            ["advanceSteelVersion"] = AdvanceSteelVersion(),
            ["savedToDisk"] = doc.IsNamedDrawing,
            ["insunits"] = doc.Database.Insunits.ToString(),
            ["dbmodBefore"] = dbmodBefore?.ToString(),
        };

        string outDir = DiagnosticOutput.DefaultDirectory(startedLocal);
        int openErrors = 0, objectErrors = 0;
        string discard = "não chamado";

        using (var output = new DiagnosticOutput(outDir))
        {
            using (doc.LockDocument())
            using (var access = new DocumentAccess(null, true))   // somente leitura; nunca há Commit()
            {
                try
                {
                    var (ids, nonSteel) = collect();
                    run["requestedObjects"] = ids.Length;
                    run["nonAdvanceSteelEntitiesIgnored"] = nonSteel;
                    ed.WriteMessage($"\nPASB: lendo {ids.Length} objeto(s)...");

                    for (int i = 0; i < ids.Length; i++)
                    {
                        FilerObject? o;
                        try { o = DatabaseManager.Open(ids[i]); }
                        catch (System.Exception ex) { openErrors++; output.WriteOpenFailure(SafeHandle(ids[i]), ex); continue; }
                        if (o == null) { openErrors++; output.WriteOpenFailure(SafeHandle(ids[i]), new InvalidOperationException("DatabaseManager.Open retornou null.")); continue; }

                        try
                        {
                            var (header, records, objectType) = ObjectReader.Read(o);
                            output.WriteObject(header, objectType, records);
                        }
                        catch (System.Exception ex)
                        {
                            objectErrors++;
                            output.WriteOpenFailure(SafeHandle(ids[i]), ex);
                        }

                        if ((i + 1) % 200 == 0) ed.WriteMessage($"\nPASB: {i + 1}/{ids.Length}...");
                    }
                }
                finally
                {
                    // Terceira barreira: descarta qualquer alteração acidental (seção 2).
                    try { access.DiscardAllChanges(); discard = "ok"; }
                    catch (System.Exception ex) { discard = "falhou: " + ex.GetType().Name + ": " + ex.Message; }
                }
            }

            object? dbmodAfter = SysVar("DBMOD");
            bool changed = !Equals(dbmodBefore, dbmodAfter);
            run["dbmodAfter"] = dbmodAfter?.ToString();
            run["dbmodChanged"] = changed;
            run["discardAllChanges"] = discard;
            run["openErrors"] = openErrors;
            run["objectErrors"] = objectErrors;
            run["durationMs"] = clock.ElapsedMilliseconds;
            output.Finish(run);

            ed.WriteMessage($"\nPASB: {output.ObjectsWritten} objeto(s) gravado(s), {openErrors} falha(s) ao abrir, {objectErrors} falha(s) de leitura.");
            ed.WriteMessage($"\nPASB: saída em {outDir}");
            if (changed)
                ed.WriteMessage($"\nPASB: ATENÇÃO: DBMOD mudou de {dbmodBefore} para {dbmodAfter}. Feche o desenho SEM SALVAR e informe o desenvolvedor.");
            ed.WriteMessage("\n");
        }
    }

    private static object? SysVar(string name)
    {
        try { return AcApp.GetSystemVariable(name); } catch (System.Exception) { return null; }
    }

    private static string AdvanceSteelVersion()
    {
        try
        {
            return Autodesk.AdvanceSteel.Utils.VersionInformation.GetMajorVersion() + "." +
                   Autodesk.AdvanceSteel.Utils.VersionInformation.GetMinorVersion();
        }
        catch (System.Exception ex) { return "erro: " + ex.Message; }
    }

    private static string? SafeHandle(AsId id)
    {
        try { return id.Handle; } catch (System.Exception) { return null; }
    }
}
