using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace Pasb.Bridge;

/// <summary>
/// Arquivo de descoberta gravado pelo plugin quando o Conector IA é ligado.
/// Contém o token: nunca é exibido nem registrado em log.
/// </summary>
public sealed class BridgeInfo
{
    public int Port { get; set; }
    public string Token { get; set; } = "";
    public int ProcessId { get; set; }
    public string Version { get; set; } = "";
    public DateTime StartedUtc { get; set; }

    /// <summary>%LOCALAPPDATA%\PASB\conector-ia.json (a variável PASB_BRIDGE_INFO troca o caminho, para testes).</summary>
    public static string DefaultPath =>
        Environment.GetEnvironmentVariable("PASB_BRIDGE_INFO") is { Length: > 0 } over ? over
        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PASB", "conector-ia.json");

    public static BridgeInfo? TryLoad(string? path = null)
    {
        try
        {
            string p = path ?? DefaultPath;
            if (!File.Exists(p)) return null;
            return JsonSerializer.Deserialize<BridgeInfo>(File.ReadAllText(p));
        }
        catch (Exception) { return null; }
    }
}

/// <summary>Erro devolvido ao cliente (mensagem em português, sem dados sensíveis).</summary>
public sealed class BridgeException : Exception
{
    public BridgeException(string message) : base(message) { }
}

/// <summary>
/// Protocolo: uma linha JSON por pedido e uma por resposta (UTF-8).
/// Pedido: {"token":"…","id":1,"method":"pasb_status","params":{…}}.
/// Resposta: {"id":1,"ok":true,"result":…} ou {"id":1,"ok":false,"error":"…"}.
/// </summary>
public static class BridgeProtocol
{
    public const int MaxLineBytes = 1 << 20;   // 1 MB por pedido

    /// <summary>Lê uma linha limitada a <paramref name="max"/> bytes; null no fim do fluxo.</summary>
    public static async Task<string?> ReadLineAsync(Stream s, int max, CancellationToken ct)
    {
        var buf = new MemoryStream();
        var one = new byte[1];
        while (true)
        {
            int n = await s.ReadAsync(one, ct).ConfigureAwait(false);
            if (n == 0) return buf.Length == 0 ? null : Encoding.UTF8.GetString(buf.ToArray());
            if (one[0] == (byte)'\n') return Encoding.UTF8.GetString(buf.ToArray()).TrimEnd('\r');
            if (buf.Length >= max) throw new BridgeException("Pedido grande demais.");
            buf.WriteByte(one[0]);
        }
    }

    public static async Task WriteLineAsync(Stream s, JsonNode node, CancellationToken ct)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(node.ToJsonString() + "\n");
        await s.WriteAsync(bytes, ct).ConfigureAwait(false);
        await s.FlushAsync(ct).ConfigureAwait(false);
    }

    public static bool TokenEquals(string? a, string b) =>
        a != null && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(a), Encoding.UTF8.GetBytes(b));
}

/// <summary>
/// Servidor da ponte (roda dentro do Advance Steel). Escuta só em 127.0.0.1, porta aleatória,
/// e exige o token do arquivo de descoberta. O <c>handler</c> decide quais métodos existem.
/// </summary>
public sealed class BridgeServer : IDisposable
{
    public delegate Task<JsonNode?> Handler(string method, JsonObject parameters, CancellationToken ct);

    private readonly Handler _handler;
    private readonly string _infoPath;
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource _cts = new();
    private Task? _accept;

    public BridgeInfo Info { get; }
    public bool Running => _accept != null && !_cts.IsCancellationRequested;

    public BridgeServer(Handler handler, string version, string? infoPath = null)
    {
        _handler = handler;
        _infoPath = infoPath ?? BridgeInfo.DefaultPath;
        Info = new BridgeInfo
        {
            Token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)),
            ProcessId = Environment.ProcessId,
            Version = version,
        };
    }

    public void Start()
    {
        _listener.Start();
        Info.Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        Info.StartedUtc = DateTime.UtcNow;
        Directory.CreateDirectory(Path.GetDirectoryName(_infoPath)!);
        // Grava num temporário e troca, para o cliente nunca ler um arquivo pela metade.
        string tmp = _infoPath + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(Info), new UTF8Encoding(false));
        File.Move(tmp, _infoPath, overwrite: true);
        _accept = Task.Run(AcceptLoop);
    }

    private async Task AcceptLoop()
    {
        while (!_cts.IsCancellationRequested)
        {
            TcpClient c;
            try { c = await _listener.AcceptTcpClientAsync(_cts.Token).ConfigureAwait(false); }
            catch (Exception) { break; }
            _ = Task.Run(() => Serve(c));
        }
    }

    private async Task Serve(TcpClient client)
    {
        using (client)
        {
            try
            {
                if (client.Client.RemoteEndPoint is IPEndPoint ep && !IPAddress.IsLoopback(ep.Address)) return;
                NetworkStream s = client.GetStream();
                while (!_cts.IsCancellationRequested)
                {
                    string? line = await BridgeProtocol.ReadLineAsync(s, BridgeProtocol.MaxLineBytes, _cts.Token).ConfigureAwait(false);
                    if (line == null) return;
                    if (line.Length == 0) continue;
                    JsonObject resp = await Process(line).ConfigureAwait(false);
                    await BridgeProtocol.WriteLineAsync(s, resp, _cts.Token).ConfigureAwait(false);
                    if (resp["fatal"] is not null) return;
                }
            }
            catch (Exception) { /* conexão encerrada ou pedido inválido: fecha só esta conexão */ }
        }
    }

    private async Task<JsonObject> Process(string line)
    {
        JsonNode? id = null;
        try
        {
            JsonObject req = JsonNode.Parse(line) as JsonObject ?? throw new BridgeException("Pedido inválido.");
            id = req["id"]?.DeepClone();
            if (!BridgeProtocol.TokenEquals(req["token"]?.GetValue<string>(), Info.Token))
                return new JsonObject { ["id"] = id, ["ok"] = false, ["error"] = "Token inválido.", ["fatal"] = true };
            string method = req["method"]?.GetValue<string>() ?? throw new BridgeException("Pedido sem 'method'.");
            JsonObject p = req["params"] as JsonObject ?? new JsonObject();
            JsonNode? result = await _handler(method, (JsonObject)p.DeepClone(), _cts.Token).ConfigureAwait(false);
            return new JsonObject { ["id"] = id, ["ok"] = true, ["result"] = result };
        }
        catch (BridgeException ex) { return new JsonObject { ["id"] = id, ["ok"] = false, ["error"] = ex.Message }; }
        catch (JsonException) { return new JsonObject { ["id"] = id, ["ok"] = false, ["error"] = "JSON inválido." }; }
        catch (InvalidOperationException) { return new JsonObject { ["id"] = id, ["ok"] = false, ["error"] = "Campo com tipo inválido." }; }
        catch (Exception ex) { return new JsonObject { ["id"] = id, ["ok"] = false, ["error"] = "Falha interna: " + ex.GetType().Name + ": " + ex.Message }; }
    }

    public void Dispose()
    {
        _cts.Cancel();
        try { _listener.Stop(); } catch (Exception) { }
        try
        {
            // Só apaga o arquivo se ainda for o nosso (outra instância pode ter ligado depois).
            if (BridgeInfo.TryLoad(_infoPath) is { } cur && cur.Token == Info.Token) File.Delete(_infoPath);
        }
        catch (Exception) { }
    }
}

/// <summary>Cliente da ponte (usado pelo servidor MCP). Uma conexão por chamada.</summary>
public static class BridgeClient
{
    public static async Task<JsonNode?> CallAsync(string method, JsonObject? parameters, TimeSpan timeout, string? infoPath = null)
    {
        BridgeInfo info = BridgeInfo.TryLoad(infoPath)
            ?? throw new BridgeException("O Conector IA está desligado. No Advance Steel, clique em PASB > Conector IA.");
        using var cts = new CancellationTokenSource(timeout);
        using var client = new TcpClient();
        try { await client.ConnectAsync(IPAddress.Loopback, info.Port, cts.Token).ConfigureAwait(false); }
        catch (Exception)
        {
            throw new BridgeException("Não consegui falar com o Advance Steel. Ele pode ter sido fechado: abra-o e ligue PASB > Conector IA de novo.");
        }
        NetworkStream s = client.GetStream();
        var req = new JsonObject { ["token"] = info.Token, ["id"] = 1, ["method"] = method, ["params"] = parameters ?? new JsonObject() };
        try
        {
            await BridgeProtocol.WriteLineAsync(s, req, cts.Token).ConfigureAwait(false);
            string? line = await BridgeProtocol.ReadLineAsync(s, 64 << 20, cts.Token).ConfigureAwait(false)
                ?? throw new BridgeException("O Advance Steel fechou a conexão sem responder.");
            JsonObject resp = JsonNode.Parse(line) as JsonObject ?? throw new BridgeException("Resposta inválida da ponte.");
            if (resp["ok"]?.GetValue<bool>() == true) return resp["result"];
            throw new BridgeException(resp["error"]?.GetValue<string>() ?? "Erro desconhecido na ponte.");
        }
        catch (OperationCanceledException)
        {
            throw new BridgeException($"O Advance Steel não respondeu em {timeout.TotalSeconds:0} s (pode estar ocupado com outro comando).");
        }
    }
}
