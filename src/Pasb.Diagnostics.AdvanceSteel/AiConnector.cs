using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Autodesk.AdvanceSteel.CADAccess;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using Pasb.Bridge;
using Pasb.Connector;
using Pasb.Model;
using Pasb.Ui;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;
using AcDoc = Autodesk.AutoCAD.ApplicationServices.Document;
using AcId = Autodesk.AutoCAD.DatabaseServices.ObjectId;
using AsId = Autodesk.AdvanceSteel.CADLink.Database.ObjectId;

[assembly: CommandClass(typeof(Pasb.Diagnostics.AdvanceSteel.AiConnector))]

namespace Pasb.Diagnostics.AdvanceSteel;

/// <summary>
/// Conector IA (docs/AI-CONNECTOR.md). PASB_CONECTOR_IA liga/desliga a ponte local; o servidor MCP (Pasb.Mcp.exe)
/// conversa com ela. Cada pedido de leitura vira o comando interno PASB_IA_EXECUTAR, que roda no contexto de comando
/// do AutoCAD e usa o mesmo caminho validado do PASB_DIAG_ALL (transação descartada, DBMOD conferido).
/// Nenhuma ferramenta altera o desenho.
/// </summary>
public sealed class AiConnector
{
    private sealed record Job(string Method, JsonObject Params, TaskCompletionSource<JsonNode?> Done, DateTime QueuedUtc);

    private static BridgeServer? _server;
    private static Control? _marshal;   // criado na thread principal: BeginInvoke acorda o AutoCAD mesmo ocioso
    private static readonly ConcurrentQueue<Job> Queue = new();
    private static readonly SemaphoreSlim OneAtATime = new(1, 1);
    private static readonly TimeSpan JobTimeout = TimeSpan.FromMinutes(10);

    public static bool IsRunning => _server?.Running == true;

    [CommandMethod("PASB", "PASB_CONECTOR_IA", CommandFlags.Modal)]
    public void Toggle()
    {
        AcDoc? doc = AcApp.DocumentManager.MdiActiveDocument;
        Editor? ed = doc?.Editor;
        try
        {
            if (IsRunning)
            {
                Stop();
                ed?.WriteMessage("\nPASB: Conector IA DESLIGADO.\n");
                MessageBox.Show("Conector IA desligado.", "PASB — Conector IA", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            Start();
            ed?.WriteMessage("\nPASB: Conector IA LIGADO (só aceita conexões deste computador, com token).\n" +
                             "PASB: ferramentas: pasb_status, pasb_read_model, pasb_get_object, pasb_export_ifc. Nenhuma altera o desenho.\n");
            MessageBox.Show(
                "Conector IA ligado.\n\nO assistente de IA (Claude Code com o servidor \"pasb\" registrado) agora pode ler o modelo e exportar IFC.\n" +
                "Nenhuma ferramenta altera o desenho.\n\nClique de novo em \"Conector IA\" para desligar.",
                "PASB — Conector IA", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (System.Exception ex)
        {
            ed?.WriteMessage($"\nPASB: PASB_CONECTOR_IA falhou: {ex.GetType().Name}: {ex.Message}\n");
        }
    }

    /// <summary>Comando interno: executa os pedidos na fila, no contexto de comando do documento ativo.</summary>
    [CommandMethod("PASB", "PASB_IA_EXECUTAR", CommandFlags.Modal | CommandFlags.NoHistory)]
    public void Execute()
    {
        AcDoc? doc = AcApp.DocumentManager.MdiActiveDocument;
        while (Queue.TryDequeue(out Job? job))
        {
            if (DateTime.UtcNow - job.QueuedUtc > JobTimeout) { job.Done.TrySetException(new BridgeException("Pedido expirou na fila.")); continue; }
            if (doc == null) { job.Done.TrySetException(new BridgeException("Nenhum desenho aberto no Advance Steel.")); continue; }
            try
            {
                doc.Editor.WriteMessage($"\nPASB IA: {job.Method}...");
                job.Done.TrySetResult(RunJob(doc, job.Method, job.Params));
                doc.Editor.WriteMessage(" concluído.\n");
            }
            catch (BridgeException ex) { job.Done.TrySetException(ex); doc.Editor.WriteMessage($" recusado: {ex.Message}\n"); }
            catch (System.Exception ex)
            {
                job.Done.TrySetException(new BridgeException($"Falha no Advance Steel: {ex.GetType().Name}: {ex.Message}"));
                doc.Editor.WriteMessage($" falhou: {ex.GetType().Name}: {ex.Message}\n");
            }
        }
    }

    internal static void Start()
    {
        if (IsRunning) return;
        if (_marshal == null) { _marshal = new Control(); _marshal.CreateControl(); _ = _marshal.Handle; }
        _server = new BridgeServer(Handle, Version);
        _server.Start();
        PasbRibbon.SetConnectorState(true);
    }

    internal static void Stop()
    {
        StopBridgeOnly();
        PasbRibbon.SetConnectorState(false);
    }

    /// <summary>
    /// Para a ponte sem tocar em interface (WPF/WinForms). Usado no Terminate: mexer na faixa de opções enquanto o AutoCAD
    /// fecha causou ERRO FATAL c015000f na 0.5.0.
    /// </summary>
    internal static void StopBridgeOnly()
    {
        BridgeServer? s = _server;
        _server = null;
        s?.Dispose();
        while (Queue.TryDequeue(out Job? job)) job.Done.TrySetException(new BridgeException("O Conector IA foi desligado."));
    }

    // ------------------------------------------------------------------ pedidos (thread da ponte)

    private static async Task<JsonNode?> Handle(string method, JsonObject p, CancellationToken ct)
    {
        if (ToolCatalog.Find(method) == null) throw new BridgeException("Ferramenta desconhecida: " + method);
        if (method == ToolCatalog.Status) return await OnMain(Status).ConfigureAwait(false);

        // Leituras do modelo: uma por vez, no contexto de comando.
        if (!await OneAtATime.WaitAsync(TimeSpan.FromSeconds(5), ct).ConfigureAwait(false))
            throw new BridgeException("Outro pedido do Conector IA está em andamento; tente de novo em instantes.");
        try
        {
            var job = new Job(method, p, new TaskCompletionSource<JsonNode?>(TaskCreationOptions.RunContinuationsAsynchronously), DateTime.UtcNow);
            Queue.Enqueue(job);
            await OnMain(() =>
            {
                AcDoc? doc = AcApp.DocumentManager.MdiActiveDocument;
                if (doc == null) { job.Done.TrySetException(new BridgeException("Nenhum desenho aberto no Advance Steel.")); return null; }
                // Não cancela o comando do usuário: se houver um em andamento, o pedido espera ele terminar.
                doc.SendStringToExecute("PASB_IA_EXECUTAR ", true, false, false);
                return null;
            }).ConfigureAwait(false);
            return await job.Done.Task.WaitAsync(JobTimeout, ct).ConfigureAwait(false);
        }
        catch (TimeoutException) { throw new BridgeException("O Advance Steel não executou o pedido a tempo (há algum comando ou janela aberta?)."); }
        finally { OneAtATime.Release(); }
    }

    private static Task<JsonNode?> OnMain(Func<JsonNode?> f)
    {
        Control m = _marshal ?? throw new BridgeException("Conector IA não inicializado.");
        var tcs = new TaskCompletionSource<JsonNode?>(TaskCreationOptions.RunContinuationsAsynchronously);
        m.BeginInvoke(new Action(() =>
        {
            try { tcs.TrySetResult(f()); }
            catch (BridgeException ex) { tcs.TrySetException(ex); }
            catch (System.Exception ex) { tcs.TrySetException(new BridgeException($"{ex.GetType().Name}: {ex.Message}")); }
        }));
        return tcs.Task;
    }

    // ------------------------------------------------------------------ ferramentas (thread principal)

    private static JsonNode? Status()
    {
        AcDoc? doc = AcApp.DocumentManager.MdiActiveDocument;
        return new JsonObject
        {
            ["connected"] = true,
            ["plugin"] = "PASB - Plugin Advance Steel Brunno",
            ["pluginVersion"] = Version,
            ["autocadVersion"] = AcApp.Version.ToString(),
            ["document"] = doc == null ? null : Path.GetFileName(doc.Name),
            ["documentSaved"] = doc?.IsNamedDrawing,
            ["drawingUnit"] = doc?.Database.Insunits.ToString(),
            ["tools"] = new JsonArray(ToolCatalog.All.Select(t => (JsonNode)JsonValue.Create(t.Name)!).ToArray()),
            ["modifiesDrawing"] = false,
        };
    }

    private static JsonNode? RunJob(AcDoc doc, string method, JsonObject p)
    {
        Editor ed = doc.Editor;
        double unit = ExportCommand.UnitToMeters(doc.Database.Insunits.ToString(), out _);
        switch (method)
        {
            case ToolCatalog.ReadModel:
            {
                var (dir, safe) = Read(ed, "PASB_IA_LER_MODELO", AllModel);
                Extraction x = Import(dir, doc);
                int max = p["max_objects"] is { } n ? n.GetValue<int>() : 300;
                return ConnectorLogic.Summarize(x, dir, safe, max, p["category"]?.GetValue<string>());
            }
            case ToolCatalog.GetObject:
            {
                string h = ConnectorLogic.NormalizeHandle(p["handle"]?.GetValue<string>());
                AcId id;
                try
                {
                    if (!doc.Database.TryGetObjectId(new Handle(Convert.ToInt64(h, 16)), out id) || id.IsNull || id.IsErased)
                        throw new BridgeException($"Não existe objeto com handle {h} neste desenho.");
                }
                catch (System.Exception ex) when (ex is not BridgeException) { throw new BridgeException($"Handle {h} inválido neste desenho."); }
                var (ids, _) = Commands.FromCad(new[] { id });
                if (ids.Length == 0) throw new BridgeException($"O objeto {h} não é um objeto do Advance Steel.");
                var (dir, safe) = Read(ed, "PASB_IA_LER_OBJETO", () => (ids, 0));
                return ConnectorLogic.DescribeObject(Import(dir, doc), h, safe);
            }
            case ToolCatalog.MaterialList:
            {
                var (dir, safe) = Read(ed, "PASB_IA_LISTA_MATERIAL", AllModel);
                JsonObject r = Pasb.Connector.MaterialList.ToJson(Pasb.Connector.MaterialList.Build(Import(dir, doc)));
                r["modelUnchanged"] = safe; r["diagnosticFolder"] = dir;
                return r;
            }
            case ToolCatalog.ExportIfc:
            {
                // Valida tudo antes de ler o modelo; a configuração da janela não é alterada.
                ExportSettings s = ConnectorLogic.ApplyExportParameters(ExportSettings.Load(), p, unit, out _);
                var (dir, _) = Read(ed, "PASB_IA_EXPORTAR_IFC", AllModel, requireSafe: true);
                return ConnectorLogic.Export(Import(dir, doc), s, unit, dir);
            }
            default:
                throw new BridgeException("Ferramenta desconhecida: " + method);
        }
    }

    private static (AsId[] ids, int nonSteel) AllModel()
    {
        DatabaseManager.GetModelObjectIds(out AsId[] ids);
        return (ids ?? Array.Empty<AsId>(), 0);
    }

    private static (string dir, bool safe) Read(Editor ed, string command, Func<(AsId[] ids, int nonSteel)> collect, bool requireSafe = true)
    {
        var (dir, safe) = Commands.Run(ed, command, collect);
        if (!safe && requireSafe)
            throw new BridgeException("A verificação de integridade do modelo falhou (DBMOD mudou ou o descarte não confirmou). " +
                                      "Feche o desenho SEM SALVAR. Pasta do diagnóstico: " + dir);
        return (dir, safe);
    }

    private static Extraction Import(string dir, AcDoc doc)
    {
        Extraction x = DiagnosticImporter.ImportDirectory(dir);
        x.SourceDocument.Name = Path.GetFileName(doc.Name);
        return x;
    }

    private static string Version =>
        typeof(AiConnector).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion?.Split('+')[0] ?? "";
}
