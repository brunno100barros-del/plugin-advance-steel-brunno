using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Pasb.Bridge;
using Pasb.Connector;
using Pasb.IfcExport;
using Pasb.Mcp;
using Pasb.Model;
using Pasb.Ui;

namespace Pasb.Connector.Tests;

/// <summary>
/// Testes do Conector IA sem Advance Steel: ponte local, servidor MCP e lógica das ferramentas.
/// O "Advance Steel" aqui é um manipulador falso que responde com um modelo sintético.
/// </summary>
internal static class Program
{
    private static int _failed, _passed;
    private static readonly string Temp = Path.Combine(Path.GetTempPath(), "pasb-conn-tests-" + Guid.NewGuid().ToString("N"));

    private const string Run_ = """{"insunits":"Millimeters"}""";
    private const string Plate = """{"handle":"2F1","clrType":"Autodesk.AdvanceSteel.Modelling.Plate","inheritance":["Plate","PlateBase","AtomicElement","FilerObject"],"properties":[{"technicalName":"Thickness","declaringType":"PlateBase","source":"property","dataType":"Double","status":"Read","rawValue":12.5}]}""";
    private const string Weld = """{"handle":"D5","clrType":"Autodesk.AdvanceSteel.Modelling.WeldLine","inheritance":["WeldLine","WeldPattern","AtomicElement","FilerObject"],"properties":[]}""";

    private static Extraction Model() => DiagnosticImporter.Import(Run_, new[] { Plate, Weld });

    [STAThread]
    private static int Main()
    {
        Directory.CreateDirectory(Temp);
        try
        {
            RunAll().GetAwaiter().GetResult();
        }
        finally
        {
            try { Directory.Delete(Temp, recursive: true); } catch (Exception) { }
        }
        Console.WriteLine($"{_passed} passaram, {_failed} falharam");
        return _failed == 0 ? 0 : 1;
    }

    private static async Task RunAll()
    {
        // ------------------------------------------------------------------ lógica das ferramentas
        Run("handle: aceita 2f1 e 0x2F1; recusa texto", () =>
        {
            True(ConnectorLogic.NormalizeHandle("2f1") == "2F1" && ConnectorLogic.NormalizeHandle(" 0x2F1 ") == "2F1", "normaliza");
            Throws(() => ConnectorLogic.NormalizeHandle("abc;drop"), "inválido");
            Throws(() => ConnectorLogic.NormalizeHandle(""), "vazio");
        });
        Run("resumo do modelo: contagens, filtro e limite", () =>
        {
            JsonObject r = ConnectorLogic.Summarize(Model(), "C:\\diag", true, 1, null);
            True(r["totalObjects"]!.GetValue<int>() == 2 && r["countsByCategory"]!["plate"]!.GetValue<int>() == 1, "contagens");
            True(r["listedObjects"]!.GetValue<int>() == 1 && r["truncated"]!.GetValue<bool>(), "limite");
            JsonObject f = ConnectorLogic.Summarize(Model(), "C:\\diag", true, 10, "weld");
            True(f["matchingObjects"]!.GetValue<int>() == 1 && f["objects"]![0]!["handle"]!.GetValue<string>() == "D5", "filtro por categoria");
            True(r["objects"]![0]!["Thickness"]!.GetValue<double>() == 12.5, "descritor curto");
        });
        Run("objeto: todas as propriedades; handle inexistente vira erro", () =>
        {
            JsonObject o = ConnectorLogic.DescribeObject(Model(), "2f1", true);
            True(o["category"]!.GetValue<string>() == "plate" && o["propertyCount"]!.GetValue<int>() == 1, "propriedades");
            True(o["properties"]![0]!["key"]!.GetValue<string>() == "PlateBase.Thickness", "chave");
            Throws(() => ConnectorLogic.DescribeObject(Model(), "FFF", true), "inexistente");
        });
        Run("exportação: parâmetros sobre a configuração da janela, sem alterá-la", () =>
        {
            var baseline = new ExportSettings { ProjectName = "Janela", Geo = GeoChoice.None };
            var p = JsonNode.Parse("""
                {"output_path":"__OUT__","georeference":"local","local_crs_name":"Obra","target_point":[12500,3000,800],
                 "target_unit":"mm","angle_mode":"azimuth","angle_degrees":30,"object_groups":["plates"]}
                """.Replace("__OUT__", Path.Combine(Temp, "a.ifc").Replace("\\", "\\\\")))!.AsObject();
            ExportSettings s = ConnectorLogic.ApplyExportParameters(baseline, p, 0.001, out bool ow);
            True(!ow && s.Geo == GeoChoice.Local && s.TargetUnit == TargetUnit.Millimeters && s.AngleMode == AngleInput.Azimuth, "campos");
            True(s.ProjectName == "Janela" && baseline.Geo == GeoChoice.None, "herda da janela e não altera a janela");
            True(s.Groups[ObjectGroups.Plates] && !s.Groups[ObjectGroups.Welds], "grupos");
            True(Math.Abs(s.ToGeoParameters(0.001)!.TargetPoint.X - 12.5) < 1e-12, "mm → m");
        });
        Run("exportação: erros claros (desconhecido, relativo, enum, tipo)", () =>
        {
            var b = new ExportSettings();
            string Err(string json)
            {
                try { ConnectorLogic.ApplyExportParameters(b, JsonNode.Parse(json)!.AsObject(), 0.001, out _); return ""; }
                catch (BridgeException ex) { return ex.Message; }
            }
            True(Err("""{"output_path":"C:\\x\\a.ifc","apagar_tudo":true}""").Contains("desconhecido"), "parâmetro desconhecido");
            True(Err("""{"output_path":"a.ifc"}""").Contains("caminho completo"), "caminho relativo");
            True(Err("""{"output_path":"C:\\x\\a.ifc","georeference":"wgs"}""").Contains("georeference"), "enum");
            True(Err("""{"output_path":"C:\\x\\a.ifc","utm_zone":"vinte"}""").Contains("tipo errado"), "tipo");
            True(Err("""{}""").Contains("arquivo de saída"), "saída obrigatória");
        });
        Run("exportação: grava IFC e relatório; recusa sobrescrever sem overwrite", () =>
        {
            string outPath = Path.Combine(Temp, "b.ifc");
            var p = new JsonObject { ["output_path"] = outPath };
            ExportSettings s = ConnectorLogic.ApplyExportParameters(new ExportSettings(), p, 0.001, out _);
            JsonObject r = ConnectorLogic.Export(Model(), s, 0.001, "C:\\diag");
            True(File.Exists(outPath) && File.ReadAllText(outPath).Contains("=IFCPLATE("), "IFC");
            True(File.Exists(Path.ChangeExtension(outPath, ".report.json")), "relatório");
            True(r["exportedByIfcClass"]!["IfcPlate"]!.GetValue<int>() == 1, "resumo");
            Throws(() => ConnectorLogic.ApplyExportParameters(new ExportSettings(), new JsonObject { ["output_path"] = outPath }, 0.001, out _), "já existe");
            ConnectorLogic.ApplyExportParameters(new ExportSettings(), new JsonObject { ["output_path"] = outPath, ["overwrite"] = true }, 0.001, out bool ow);
            True(ow, "overwrite");
        });

        Run("registro no Claude Code: só a entrada pasb, com cópia de segurança", () =>
        {
            string cfg = Path.Combine(Temp, "claude.json");
            File.WriteAllText(cfg, """{"outraChave":{"x":1},"mcpServers":{"outro":{"command":"a.exe"}}}""");
            Registration.Register(cfg, @"C:\PASB\Pasb.Mcp.exe");
            JsonObject r = JsonNode.Parse(File.ReadAllText(cfg))!.AsObject();
            True(r["outraChave"]!["x"]!.GetValue<int>() == 1 && r["mcpServers"]!["outro"]!["command"]!.GetValue<string>() == "a.exe", "preserva o resto");
            True(r["mcpServers"]!["pasb"]!["command"]!.GetValue<string>() == @"C:\PASB\Pasb.Mcp.exe", "entrada pasb");
            True(Directory.GetFiles(Temp, "claude.json.pasb-backup-*").Length == 1, "cópia de segurança");
            Registration.Unregister(cfg);
            JsonObject u = JsonNode.Parse(File.ReadAllText(cfg))!.AsObject();
            True(u["mcpServers"]!["pasb"] == null && u["mcpServers"]!["outro"] != null, "remove só pasb");
            string bad = Path.Combine(Temp, "bad.json");
            File.WriteAllText(bad, "{ quebrado");
            bool refused = false;
            try { Registration.Register(bad, "x.exe"); } catch (InvalidDataException) { refused = true; }
            True(refused && File.ReadAllText(bad) == "{ quebrado", "JSON inválido: não altera");
        });

        Run("lista de material: agrupa barras, ignora compostas, chapas e parafusos", () =>
        {
            static string Beam(string h, string cls, double len, double grams, double paintMm2) =>
                "{\"handle\":\"" + h + "\",\"clrType\":\"A." + cls + "\",\"inheritance\":[\"" + cls + "\",\"Beam\",\"AtomicElement\",\"FilerObject\"],\"properties\":[" +
                "{\"technicalName\":\"ProfSectionName\",\"declaringType\":\"Beam\",\"status\":\"Read\",\"rawValue\":\"U100\"}," +
                "{\"technicalName\":\"Material\",\"declaringType\":\"AtomicElement\",\"status\":\"Read\",\"rawValue\":\"S235JRG2\"}," +
                "{\"technicalName\":\"GetLength\",\"declaringType\":\"Beam\",\"status\":\"Read\",\"rawValue\":" + len.ToString(System.Globalization.CultureInfo.InvariantCulture) + "}," +
                "{\"technicalName\":\"GetWeight\",\"declaringType\":\"Beam\",\"argument\":\"1\",\"status\":\"Read\",\"rawValue\":" + grams.ToString(System.Globalization.CultureInfo.InvariantCulture) + "}," +
                "{\"technicalName\":\"GetPaintArea\",\"declaringType\":\"Beam\",\"status\":\"Read\",\"rawValue\":" + paintMm2.ToString(System.Globalization.CultureInfo.InvariantCulture) + "}]}";
            const string plate = """{"handle":"P1","clrType":"A.Plate","inheritance":["Plate","PlateBase","AtomicElement","FilerObject"],"properties":[{"technicalName":"Thickness","declaringType":"PlateBase","status":"Read","rawValue":12.5},{"technicalName":"Length","declaringType":"PlateBase","status":"Read","rawValue":200},{"technicalName":"Width","declaringType":"PlateBase","status":"Read","rawValue":300},{"technicalName":"GetWeight","declaringType":"PlateBase","status":"Read","rawValue":5887.5},{"technicalName":"Material","declaringType":"AtomicElement","status":"Read","rawValue":"S275"}]}""";
            const string bolts = """{"handle":"B1","clrType":"A.FinitRectScrewBoltPattern","inheritance":["FinitRectScrewBoltPattern","ScrewBoltPattern","BoltPattern","AtomicElement","FilerObject"],"properties":[{"technicalName":"ScrewDiameter","declaringType":"ScrewBoltPattern","status":"Read","rawValue":16},{"technicalName":"ScrewLength","declaringType":"ScrewBoltPattern","status":"Read","rawValue":50},{"technicalName":"GetMidpoints","declaringType":"BoltPattern","status":"Read","rawValue":[[0,0,0],[1,0,0],[2,0,0],[3,0,0]]}]}""";
            Extraction x = DiagnosticImporter.Import(Run_, new[]
            {
                Beam("51C", "StraightBeam", 1084.4985857864378, 11495.685, 403400), Beam("524", "StraightBeam", 1084.4985857864378, 11495.685, 403400),
                Beam("528", "StraightBeam", 1669, 17691.4, 620900), Beam("4CC", "CompoundStraightBeam", 1084.5, 21730, 762600), plate, bolts,
            });
            MaterialListResult m = MaterialList.Build(x);
            True(m.Rows.Count == 4, "4 linhas: " + m.Rows.Count);
            True(m.Rows[0].Kind == "Perfil" && m.Rows[0].LengthMm == 1669 && m.Rows[1].Quantity == 2 && m.Rows[1].LengthMm == 1084.5, "perfis agrupados, maior primeiro");
            True(Math.Abs(m.Rows[1].TotalKg!.Value - 22.99) < 0.01 && Math.Abs(m.Rows[1].TotalPaintM2!.Value - 0.8068) < 1e-4, "peso e pintura do grupo");
            True(m.Rows[2].Description == "Chapa #12,50 300x200" && Math.Abs(m.Rows[2].UnitKg!.Value - 5.8875) < 1e-9, "chapa: " + m.Rows[2].Description);
            True(m.Rows[3].Description == "M16x50" && m.Rows[3].Quantity == 4, "parafusos: " + m.Rows[3].Description);
            True(m.ExcludedCompounds.SequenceEqual(new[] { "4CC" }) && m.Notes[0].Contains("composta"), "composta fora da soma");
            True(Math.Abs(m.TotalKg - (11.495685 * 2 + 17.6914 + 5.8875)) < 1e-6 && m.TotalBeamLengthMm == 1084.5 * 2 + 1669, "totais");
            JsonObject j = MaterialList.ToJson(m);
            True(j["rows"]!.AsArray().Count == 4 && j["excludedCompoundBeams"]![0]!.GetValue<string>() == "4CC", "json");
        });

        // ------------------------------------------------------------------ ponte local
        await RunAsync("ponte: ida e volta, erro do manipulador e arquivo de descoberta", async () =>
        {
            string info = Path.Combine(Temp, "bridge1.json");
            using (var server = new BridgeServer((m, p, _) => m == "eco"
                       ? Task.FromResult<JsonNode?>(new JsonObject { ["eco"] = p["x"]?.DeepClone() })
                       : throw new BridgeException("não existe"), "teste", info))
            {
                server.Start();
                True(File.Exists(info) && server.Info.Port > 0 && server.Info.Token.Length == 64, "descoberta");
                JsonNode? r = await BridgeClient.CallAsync("eco", new JsonObject { ["x"] = "olá" }, TimeSpan.FromSeconds(10), info);
                True(r!["eco"]!.GetValue<string>() == "olá", "eco com acento");
                string err = "";
                try { await BridgeClient.CallAsync("outro", null, TimeSpan.FromSeconds(10), info); } catch (BridgeException ex) { err = ex.Message; }
                True(err == "não existe", "erro repassado: " + err);
            }
            True(!File.Exists(info), "arquivo apagado ao desligar");
            string off = "";
            try { await BridgeClient.CallAsync("eco", null, TimeSpan.FromSeconds(5), info); } catch (BridgeException ex) { off = ex.Message; }
            True(off.Contains("desligado"), "mensagem de conector desligado");
        });
        await RunAsync("ponte: token errado é recusado e a conexão fechada", async () =>
        {
            string info = Path.Combine(Temp, "bridge2.json");
            bool called = false;
            using var server = new BridgeServer((_, _, _) => { called = true; return Task.FromResult<JsonNode?>(null); }, "teste", info);
            server.Start();
            using var c = new TcpClient();
            await c.ConnectAsync("127.0.0.1", server.Info.Port);
            NetworkStream s = c.GetStream();
            await BridgeProtocol.WriteLineAsync(s, new JsonObject { ["token"] = "00", ["id"] = 7, ["method"] = "eco" }, CancellationToken.None);
            string? line = await BridgeProtocol.ReadLineAsync(s, 1 << 16, CancellationToken.None);
            True(line != null && JsonNode.Parse(line)!["error"]!.GetValue<string>() == "Token inválido." && !called, "recusado: " + line);
            string? after = await BridgeProtocol.ReadLineAsync(s, 1 << 16, CancellationToken.None);
            True(after == null, "conexão fechada");
        });
        await RunAsync("ponte: escuta só no endereço local (127.0.0.1)", async () =>
        {
            string info = Path.Combine(Temp, "bridge3.json");
            using var server = new BridgeServer((_, _, _) => Task.FromResult<JsonNode?>(null), "teste", info);
            server.Start();
            var props = System.Net.NetworkInformation.IPGlobalProperties.GetIPGlobalProperties();
            var mine = props.GetActiveTcpListeners().Where(e => e.Port == server.Info.Port).ToList();
            True(mine.Count > 0 && mine.All(e => System.Net.IPAddress.IsLoopback(e.Address)), "somente loopback");
            await Task.CompletedTask;
        });

        // ------------------------------------------------------------------ servidor MCP (em memória)
        await RunAsync("MCP: initialize, ping, tools/list e notificação", async () =>
        {
            var mcp = new McpServer((_, _) => Task.FromResult<JsonNode?>(null));
            JsonNode init = (await mcp.HandleAsync("""{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"t","version":"1"}}}"""))!;
            True(init["result"]!["protocolVersion"]!.GetValue<string>() == "2025-06-18" && init["result"]!["capabilities"]!["tools"] != null, "initialize");
            JsonNode other = (await mcp.HandleAsync("""{"jsonrpc":"2.0","id":2,"method":"initialize","params":{"protocolVersion":"1999-01-01"}}"""))!;
            True(other["result"]!["protocolVersion"]!.GetValue<string>() == McpServer.SupportedProtocolVersions[0], "versão desconhecida → mais recente");
            True(await mcp.HandleAsync("""{"jsonrpc":"2.0","method":"notifications/initialized"}""") == null, "notificação sem resposta");
            JsonNode ping = (await mcp.HandleAsync("""{"jsonrpc":"2.0","id":"p","method":"ping"}"""))!;
            True(ping["id"]!.GetValue<string>() == "p" && ping["result"] is JsonObject, "ping");
            JsonNode list = (await mcp.HandleAsync("""{"jsonrpc":"2.0","id":3,"method":"tools/list"}"""))!;
            var names = list["result"]!["tools"]!.AsArray().Select(t => t!["name"]!.GetValue<string>()).ToArray();
            True(names.SequenceEqual(ToolCatalog.All.Select(t => t.Name)), "lista de ferramentas");
            True(list["result"]!["tools"]![0]!["inputSchema"]!["type"]!.GetValue<string>() == "object", "schema");
        });
        await RunAsync("MCP: erros de protocolo", async () =>
        {
            var mcp = new McpServer((_, _) => Task.FromResult<JsonNode?>(null));
            True((await mcp.HandleAsync("{nao json"))!["error"]!["code"]!.GetValue<int>() == -32700, "parse");
            True((await mcp.HandleAsync("""{"jsonrpc":"2.0","id":1,"method":"resources/list"}"""))!["error"]!["code"]!.GetValue<int>() == -32601, "método");
            True((await mcp.HandleAsync("""{"jsonrpc":"2.0","id":1,"method":"tools/call","params":{"name":"as_apagar_pecas"}}"""))!["error"]!["code"]!.GetValue<int>() == -32602, "ferramenta fora da lista");
        });
        await RunAsync("MCP: falha da ponte vira resultado com isError", async () =>
        {
            var mcp = new McpServer((_, _) => throw new BridgeException("O Conector IA está desligado."));
            JsonNode r = (await mcp.HandleAsync("""{"jsonrpc":"2.0","id":9,"method":"tools/call","params":{"name":"pasb_status","arguments":{}}}"""))!;
            True(r["result"]!["isError"]!.GetValue<bool>() && r["result"]!["content"]![0]!["text"]!.GetValue<string>().Contains("desligado"), "isError");
        });

        // ------------------------------------------------------------------ ponta a ponta: Pasb.Mcp.exe real + ponte + Advance Steel falso
        await RunAsync("ponta a ponta: Pasb.Mcp.exe ↔ ponte ↔ manipulador falso", async () =>
        {
            string exe = FindMcpExe();
            string info = Path.Combine(Temp, "bridge-e2e.json");
            string outPath = Path.Combine(Temp, "e2e.ifc");
            int calls = 0;
            using var server = new BridgeServer((m, p, ct) =>
            {
                Interlocked.Increment(ref calls);
                // Mesmo roteamento do plugin, sem Advance Steel: o modelo vem do diagnóstico sintético.
                JsonNode? r = m switch
                {
                    ToolCatalog.Status => new JsonObject { ["connected"] = true, ["document"] = "Teste.dwg" },
                    ToolCatalog.ReadModel => ConnectorLogic.Summarize(Model(), Temp, true, p["max_objects"]?.GetValue<int>() ?? 300, null),
                    ToolCatalog.GetObject => ConnectorLogic.DescribeObject(Model(), p["handle"]!.GetValue<string>(), true),
                    ToolCatalog.ExportIfc => ConnectorLogic.Export(Model(), ConnectorLogic.ApplyExportParameters(new ExportSettings(), p, 0.001, out _), 0.001, Temp),
                    _ => throw new BridgeException("Ferramenta desconhecida"),
                };
                return Task.FromResult<JsonNode?>(r);
            }, "teste", info);
            server.Start();

            var psi = new ProcessStartInfo(exe)
            {
                RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false,
                StandardInputEncoding = new UTF8Encoding(false), StandardOutputEncoding = Encoding.UTF8,
            };
            psi.Environment["PASB_BRIDGE_INFO"] = info;
            using Process proc = Process.Start(psi)!;
            try
            {
                async Task<JsonNode> Rpc(string json)
                {
                    await proc.StandardInput.WriteLineAsync(json);
                    await proc.StandardInput.FlushAsync();
                    string? line = await proc.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(30));
                    return JsonNode.Parse(line ?? throw new Exception("sem resposta; stderr: " + proc.StandardError.ReadToEnd()))!;
                }
                JsonNode init = await Rpc("""{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"teste","version":"1"}}}""");
                True(init["result"]!["serverInfo"]!["name"]!.GetValue<string>() == "pasb", "initialize");
                await proc.StandardInput.WriteLineAsync("""{"jsonrpc":"2.0","method":"notifications/initialized"}""");
                JsonNode st = await Rpc("""{"jsonrpc":"2.0","id":2,"method":"tools/call","params":{"name":"pasb_status","arguments":{}}}""");
                True(!st["result"]!["isError"]!.GetValue<bool>() && st["result"]!["structuredContent"]!["document"]!.GetValue<string>() == "Teste.dwg", "status");
                JsonNode rm = await Rpc("""{"jsonrpc":"2.0","id":3,"method":"tools/call","params":{"name":"pasb_read_model","arguments":{"max_objects":5}}}""");
                True(rm["result"]!["structuredContent"]!["totalObjects"]!.GetValue<int>() == 2, "ler modelo");
                JsonNode go = await Rpc("""{"jsonrpc":"2.0","id":4,"method":"tools/call","params":{"name":"pasb_get_object","arguments":{"handle":"2f1"}}}""");
                True(go["result"]!["structuredContent"]!["class"]!.GetValue<string>().EndsWith("Plate"), "ler objeto");
                JsonNode miss = await Rpc("""{"jsonrpc":"2.0","id":5,"method":"tools/call","params":{"name":"pasb_get_object","arguments":{"handle":"ABC"}}}""");
                True(miss["result"]!["isError"]!.GetValue<bool>(), "objeto inexistente → isError");
                string args = new JsonObject { ["output_path"] = outPath, ["georeference"] = "sirgas", ["utm_zone"] = 22, ["hemisphere"] = "S", ["target_point"] = new JsonArray(500000, 7400000, 800), ["target_unit"] = "m" }.ToJsonString();
                JsonNode ex = await Rpc("""{"jsonrpc":"2.0","id":6,"method":"tools/call","params":{"name":"pasb_export_ifc","arguments":""" + args + "}}");
                True(!ex["result"]!["isError"]!.GetValue<bool>() && File.Exists(outPath), "exportar: " + ex.ToJsonString());
                string ifc = File.ReadAllText(outPath);
                True(ifc.Contains("IFCMAPCONVERSION(") && ifc.Contains("EPSG:31982"), "georreferência SIRGAS 22S no IFC");
                JsonNode again = await Rpc("""{"jsonrpc":"2.0","id":7,"method":"tools/call","params":{"name":"pasb_export_ifc","arguments":""" + args + "}}");
                True(again["result"]!["isError"]!.GetValue<bool>() && again["result"]!["content"]![0]!["text"]!.GetValue<string>().Contains("já existe"), "não sobrescreve sem permissão");
                True(calls == 6, "chamadas à ponte: " + calls);
            }
            finally
            {
                proc.StandardInput.Close();
                if (!proc.WaitForExit(5000)) proc.Kill();
            }
            True(proc.ExitCode == 0 || proc.HasExited, "MCP encerra quando a entrada fecha");
        });
    }

    private static string FindMcpExe()
    {
        DirectoryInfo? d = new(AppContext.BaseDirectory);
        while (d != null && !File.Exists(Path.Combine(d.FullName, "PluginAdvanceSteelBrunno.slnx"))) d = d.Parent;
        if (d == null) throw new Exception("raiz do repositório não encontrada");
        foreach (string cfg in new[] { "Release", "Debug" })
        {
            string p = Path.Combine(d.FullName, "src", "Pasb.Mcp", "bin", cfg, "net10.0", "Pasb.Mcp.exe");
            if (File.Exists(p)) return p;
        }
        throw new Exception("Pasb.Mcp.exe não compilado");
    }

    private static void Run(string name, Action t)
    {
        try { t(); _passed++; Console.WriteLine("ok    " + name); }
        catch (Exception ex) { _failed++; Console.WriteLine("FALHA " + name + ": " + ex.Message); }
    }

    private static async Task RunAsync(string name, Func<Task> t)
    {
        try { await t(); _passed++; Console.WriteLine("ok    " + name); }
        catch (Exception ex) { _failed++; Console.WriteLine("FALHA " + name + ": " + ex.Message); }
    }

    private static void True(bool c, string what) { if (!c) throw new Exception(what); }

    private static void Throws(Action a, string what)
    {
        try { a(); } catch (BridgeException) { return; }
        throw new Exception("esperava erro: " + what);
    }
}
