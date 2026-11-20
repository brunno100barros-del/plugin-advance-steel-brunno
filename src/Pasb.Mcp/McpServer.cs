using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Pasb.Bridge;

namespace Pasb.Mcp;

/// <summary>
/// Servidor MCP mínimo (transporte stdio: uma mensagem JSON-RPC por linha).
/// Métodos: initialize, notifications/initialized, ping, tools/list, tools/call.
/// Não escreve nada em stdout além das respostas; diagnósticos vão para stderr.
/// </summary>
public sealed class McpServer
{
    public static readonly string[] SupportedProtocolVersions = { "2025-11-25", "2025-06-18", "2025-03-26", "2024-11-05" };

    public delegate Task<JsonNode?> BridgeCall(string method, JsonObject parameters);

    private readonly BridgeCall _call;

    public McpServer(BridgeCall call) { _call = call; }

    public static string Version =>
        typeof(McpServer).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion?.Split('+')[0] ?? "0";

    public async Task RunAsync(TextReader input, TextWriter output)
    {
        string? line;
        while ((line = await input.ReadLineAsync().ConfigureAwait(false)) != null)
        {
            if (line.Trim().Length == 0) continue;
            JsonNode? resp = await HandleAsync(line).ConfigureAwait(false);
            if (resp == null) continue;
            await output.WriteLineAsync(resp.ToJsonString()).ConfigureAwait(false);
            await output.FlushAsync().ConfigureAwait(false);
        }
    }

    /// <summary>Processa uma mensagem; devolve a resposta ou null (notificações não têm resposta).</summary>
    public async Task<JsonNode?> HandleAsync(string line)
    {
        JsonNode? msg;
        try { msg = JsonNode.Parse(line); }
        catch (JsonException) { return Error(null, -32700, "Parse error"); }
        if (msg is JsonArray) return Error(null, -32600, "Batch não suportado");
        if (msg is not JsonObject req || req["jsonrpc"]?.ToString() != "2.0") return Error(null, -32600, "Invalid Request");

        JsonNode? id = req["id"]?.DeepClone();
        string? method = req["method"] is JsonValue mv && mv.TryGetValue(out string? m) ? m : null;
        if (method == null) return id == null ? null : Error(id, -32600, "Invalid Request");   // resposta do cliente: ignora
        bool notification = !req.ContainsKey("id");
        JsonObject p = req["params"] as JsonObject ?? new JsonObject();

        try
        {
            JsonNode? result = method switch
            {
                "initialize" => Initialize(p),
                "ping" => new JsonObject(),
                "tools/list" => ToolsList(),
                "tools/call" => await ToolsCall(p).ConfigureAwait(false),
                _ when method.StartsWith("notifications/", StringComparison.Ordinal) => null,
                _ => throw new RpcException(-32601, "Method not found: " + method),
            };
            if (notification) return null;
            return new JsonObject { ["jsonrpc"] = "2.0", ["id"] = id, ["result"] = result ?? new JsonObject() };
        }
        catch (RpcException ex) { return notification ? null : Error(id, ex.Code, ex.Message); }
    }

    private static JsonObject Initialize(JsonObject p)
    {
        string requested = p["protocolVersion"]?.ToString() ?? "";
        string version = SupportedProtocolVersions.Contains(requested) ? requested : SupportedProtocolVersions[0];
        return new JsonObject
        {
            ["protocolVersion"] = version,
            ["capabilities"] = new JsonObject { ["tools"] = new JsonObject { ["listChanged"] = false } },
            ["serverInfo"] = new JsonObject { ["name"] = "pasb", ["title"] = "PASB - Plugin Advance Steel Brunno", ["version"] = Version },
            ["instructions"] =
                "Ferramentas do plugin PASB para o Advance Steel 2027. Leem o modelo sem alterá-lo; pasb_export_ifc só grava o arquivo IFC. " +
                "Comece por pasb_status. Se o conector estiver desligado, peça ao usuário para clicar em PASB > Conector IA no Advance Steel.",
        };
    }

    private static JsonObject ToolsList()
    {
        var tools = new JsonArray();
        foreach (ToolDef t in ToolCatalog.All)
        {
            tools.Add(new JsonObject
            {
                ["name"] = t.Name,
                ["title"] = t.Title,
                ["description"] = t.Description,
                ["inputSchema"] = t.InputSchema.DeepClone(),
                ["annotations"] = new JsonObject
                {
                    ["readOnlyHint"] = t.ReadOnly,
                    ["destructiveHint"] = false,
                    ["openWorldHint"] = false,
                },
            });
        }
        return new JsonObject { ["tools"] = tools };
    }

    private async Task<JsonObject> ToolsCall(JsonObject p)
    {
        string name = p["name"]?.ToString() ?? throw new RpcException(-32602, "tools/call sem 'name'");
        if (ToolCatalog.Find(name) == null) throw new RpcException(-32602, "Ferramenta desconhecida: " + name);
        JsonObject args = p["arguments"] as JsonObject ?? new JsonObject();
        try
        {
            JsonNode? result = await _call(name, (JsonObject)args.DeepClone()).ConfigureAwait(false);
            string text = result?.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) ?? "null";
            var r = new JsonObject
            {
                ["content"] = new JsonArray(new JsonObject { ["type"] = "text", ["text"] = text }),
                ["isError"] = false,
            };
            if (result is JsonObject obj) r["structuredContent"] = obj.DeepClone();
            return r;
        }
        catch (BridgeException ex)
        {
            return new JsonObject
            {
                ["content"] = new JsonArray(new JsonObject { ["type"] = "text", ["text"] = ex.Message }),
                ["isError"] = true,
            };
        }
    }

    private static JsonObject Error(JsonNode? id, int code, string message) =>
        new() { ["jsonrpc"] = "2.0", ["id"] = id, ["error"] = new JsonObject { ["code"] = code, ["message"] = message } };

    private sealed class RpcException : Exception
    {
        public int Code { get; }
        public RpcException(int code, string message) : base(message) { Code = code; }
    }
}

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        if (args.Contains("--version")) { Console.WriteLine("Pasb.Mcp " + McpServer.Version); return 0; }
        if (args.Contains("--register") || args.Contains("--unregister"))
        {
            try
            {
                int i = Array.IndexOf(args, "--config");
                string config = i >= 0 && i + 1 < args.Length ? args[i + 1] : Registration.DefaultConfigPath;
                string exe = Environment.ProcessPath ?? throw new InvalidOperationException("caminho do programa desconhecido");
                Console.WriteLine(args.Contains("--register") ? Registration.Register(config, exe) : Registration.Unregister(config));
                return 0;
            }
            catch (Exception ex) { Console.Error.WriteLine("Falhou: " + ex.Message); return 1; }
        }
        var utf8 = new UTF8Encoding(false);
        Console.InputEncoding = utf8;
        var stdout = new StreamWriter(Console.OpenStandardOutput(), utf8) { AutoFlush = true, NewLine = "\n" };
        var stdin = new StreamReader(Console.OpenStandardInput(), utf8);
        // Exportar e ler modelos grandes pode demorar: 10 minutos por chamada.
        var server = new McpServer((m, p) => BridgeClient.CallAsync(m, p, TimeSpan.FromMinutes(10)));
        try { await server.RunAsync(stdin, stdout); }
        catch (Exception ex) { Console.Error.WriteLine("Pasb.Mcp: " + ex.GetType().Name + ": " + ex.Message); return 1; }
        return 0;
    }
}
