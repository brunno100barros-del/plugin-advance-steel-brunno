using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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

    /// <summary>true: transação com escrita permitida e DiscardAllChanges() no final; false: somente leitura.</summary>
    internal static readonly bool WriteWithDiscard = true;

    internal static readonly string TransactionMode = WriteWithDiscard ? "write-discarded" : "read-only";

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
    internal static (AsId[] ids, int nonSteel) FromCad(AcId[] cadIds)
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

    /// <summary>Lê os objetos e grava a saída do diagnóstico. Retorna a pasta de saída e se o modelo ficou intacto.</summary>
    /// <param name="profile">
    /// Diagnostic: trace gravado no disco físico antes de cada leitura. Export: trace só por objeto, sem esperar o disco,
    /// e sem as leituras de ApprovedMethods.OmittedInExport (docs/EXPORT-PERFORMANCE.md).
    /// </param>
    internal static (string outDir, bool safe) Run(Editor ed, string command, Func<(AsId[] ids, int nonSteel)> collect,
        ReadProfile profile = ReadProfile.Diagnostic)
    {
        bool diagnostic = profile == ReadProfile.Diagnostic;
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
            // Valores criados sob demanda durante a leitura (por exemplo, IDs) podem não existir no modelo salvo.
            ["transactionMode"] = TransactionMode,
            ["readProfile"] = diagnostic ? "diagnostic" : "export",
        };
        if (!diagnostic)
            run["omittedReads"] = new JsonArray(ApprovedMethods.OmittedInExport.Select(n => (JsonNode)JsonValue.Create(n)!).ToArray());

        string outDir = DiagnosticOutput.DefaultDirectory(startedLocal);
        int openErrors = 0, objectErrors = 0;
        string discard = "não chamado";

        using (var output = new DiagnosticOutput(outDir, flushEachObject: diagnostic))
        using (var trace = new TraceLog(output.TracePath, durable: diagnostic))
        {
            trace.Write($"inicio {command} versao {DiagnosticVersion} DBMOD={dbmodBefore}");
            using (doc.LockDocument())
            // Modo "gravação com descarte" (decisão do desenvolvedor após os Testes 1-5, docs/DIAGNOSTIC-TEST-LOG.md):
            // algumas leituras gravam valores internamente e derrubam o AutoCAD numa transação somente leitura.
            // A transação permite escrita, mas NUNCA há Commit(): DiscardAllChanges() desfaz tudo no finally.
            // Mesma técnica do modo dry-run do AdvancedSteelMCP (Infrastructure.cs:234-244).
            using (var access = new DocumentAccess(null, !WriteWithDiscard))
            {
                trace.Write($"DocumentAccess aberto (modo {TransactionMode})");
                try
                {
                    trace.Write("coletando ids");
                    var (ids, nonSteel) = collect();
                    trace.Write($"ids: {ids.Length}, ignorados: {nonSteel}");
                    run["requestedObjects"] = ids.Length;
                    run["nonAdvanceSteelEntitiesIgnored"] = nonSteel;
                    ed.WriteMessage($"\nPASB: lendo {ids.Length} objeto(s)...");
                    Dictionary<string, List<string>>? typeMap = null;   // montado só se algum objeto não abrir

                    for (int i = 0; i < ids.Length; i++)
                    {
                        FilerObject? o;
                        trace.Write($"[{i + 1}/{ids.Length}] Open handle={SafeHandle(ids[i])}");
                        try { o = DatabaseManager.Open(ids[i]); }
                        catch (System.Exception ex)
                        {
                            openErrors++;
                            output.WriteOpenFailure(SafeHandle(ids[i]), ex, Identify(ids[i], ref typeMap, trace));
                            continue;
                        }
                        if (o == null)
                        {
                            openErrors++;
                            output.WriteOpenFailure(SafeHandle(ids[i]), new InvalidOperationException("DatabaseManager.Open retornou null."),
                                Identify(ids[i], ref typeMap, trace));
                            continue;
                        }

                        try
                        {
                            string h = SafeHandle(ids[i]) ?? "?";
                            trace.Write($"[{i + 1}/{ids.Length}] tipo={o.GetType().Name}");
                            Action<string>? perRead = diagnostic ? step => trace.Write($"  {h} {step}") : null;
                            var (header, records, objectType) = ObjectReader.Read(o, perRead, profile);
                            output.WriteObject(header, objectType, records);
                            trace.Write($"[{i + 1}/{ids.Length}] gravado ({records.Count} leituras)");
                        }
                        catch (System.Exception ex)
                        {
                            objectErrors++;
                            output.WriteOpenFailure(SafeHandle(ids[i]), ex);
                        }

                        if ((i + 1) % 200 == 0) Progress(ed, i + 1, ids.Length, clock.Elapsed);
                    }
                }
                finally
                {
                    // Descarta tudo o que as leituras gravaram internamente. Nunca há Commit().
                    trace.Write("DiscardAllChanges");
                    try { access.DiscardAllChanges(); discard = "ok"; }
                    catch (System.Exception ex) { discard = "falhou: " + ex.GetType().Name + ": " + ex.Message; }
                }
            }

            trace.Write($"DocumentAccess liberado; discard={discard}");
            object? dbmodAfter = SysVar("DBMOD");
            trace.Write($"fim DBMOD={dbmodAfter}");
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
            if (discard != "ok")
                ed.WriteMessage($"\nPASB: ATENÇÃO: DiscardAllChanges {discard}. Feche o desenho SEM SALVAR e informe o desenvolvedor.");
            ed.WriteMessage("\n");
            return (outDir, !changed && discard == "ok");
        }
    }

    /// <summary>
    /// Identifica um objeto que DatabaseManager.Open não abriu, sem abri-lo:
    /// (1) classe da entidade AutoCAD correspondente (ObjectId.ObjectClass não abre o objeto);
    /// (2) tipo Advance Steel, cruzando o handle com GetModelObjectIds filtrado por cada eObjectType.
    /// </summary>
    private static JsonObject Identify(AsId id, ref Dictionary<string, List<string>>? typeMap, TraceLog trace)
    {
        var j = new JsonObject();
        string? handle = SafeHandle(id);
        trace.Write($"identificando handle={handle} sem abrir");
        try
        {
            var cadId = new AcId(id.AsOldId());
            j["autocadClass"] = cadId.ObjectClass?.Name;
            j["autocadDxfName"] = cadId.ObjectClass?.DxfName;
            j["autocadIsErased"] = cadId.IsErased;
        }
        catch (System.Exception ex) { j["autocadClassError"] = ex.GetType().Name + ": " + ex.Message; }

        try
        {
            typeMap ??= BuildTypeMap(trace);
            j["advanceSteelTypes"] = handle != null && typeMap.TryGetValue(handle, out List<string>? types)
                ? new JsonArray(types.Select(t => (JsonNode)JsonValue.Create(t)!).ToArray())
                : new JsonArray();
        }
        catch (System.Exception ex) { j["advanceSteelTypesError"] = ex.GetType().Name + ": " + ex.Message; }
        return j;
    }

    /// <summary>handle → tipos eObjectType cujo filtro exato (AppendAcceptedClass) retorna o objeto. Só consultas.</summary>
    private static Dictionary<string, List<string>> BuildTypeMap(TraceLog trace)
    {
        trace.Write("montando mapa de tipos por ClassTypeFilter");
        var map = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (FilerObject.eObjectType t in Enum.GetValues(typeof(FilerObject.eObjectType)).Cast<FilerObject.eObjectType>().Distinct())
        {
            AsId[]? ids = null;
            try
            {
                var filter = new ClassTypeFilter();
                filter.RejectAllFirst();
                filter.AppendAcceptedClass(t);
                DatabaseManager.GetModelObjectIds(out ids, filter);
            }
            catch (System.Exception) { continue; }
            foreach (AsId id in ids ?? Array.Empty<AsId>())
            {
                string? h = SafeHandle(id);
                if (h == null) continue;
                if (!map.TryGetValue(h, out List<string>? list)) map[h] = list = new List<string>();
                list.Add(t.ToString());
            }
        }
        trace.Write($"mapa de tipos: {map.Count} objetos");
        return map;
    }

    /// <summary>Progresso na linha de comando com tempo decorrido e estimativa do restante.</summary>
    private static void Progress(Editor ed, int done, int total, TimeSpan elapsed)
    {
        double rate = done / Math.Max(elapsed.TotalSeconds, 0.001);
        TimeSpan left = TimeSpan.FromSeconds((total - done) / Math.Max(rate, 0.001));
        ed.WriteMessage($"\nPASB: {done}/{total} objetos, {Clock(elapsed)} decorrido, ~{Clock(left)} restante...");
    }

    /// <summary>m:ss, sem estourar em 60 minutos.</summary>
    internal static string Clock(TimeSpan t) => $"{(int)t.TotalMinutes}:{t.Seconds:00}";

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
