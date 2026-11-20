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
        // Criar janelas (teste da numeração) instalaria um contexto que exige laço de mensagens; sem ele os testes
        // assíncronos seguintes ficariam esperando para sempre.
        System.Windows.Forms.WindowsFormsSynchronizationContext.AutoInstall = false;
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

        Run("tabelas: resumo de perfis, parafusos, chumbadores e chapas por espessura", () =>
        {
            static string Bar(string h, string sec, double len, double kgm, double paintMm2) =>
                "{\"handle\":\"" + h + "\",\"clrType\":\"A.StraightBeam\",\"inheritance\":[\"StraightBeam\",\"Beam\",\"AtomicElement\",\"FilerObject\"],\"properties\":[" +
                "{\"technicalName\":\"ProfSectionName\",\"declaringType\":\"Beam\",\"status\":\"Read\",\"rawValue\":\"" + sec + "\"}," +
                "{\"technicalName\":\"Material\",\"declaringType\":\"AtomicElement\",\"status\":\"Read\",\"rawValue\":\"S235JRG2\"}," +
                "{\"technicalName\":\"GetLength\",\"declaringType\":\"Beam\",\"status\":\"Read\",\"rawValue\":" + len.ToString(System.Globalization.CultureInfo.InvariantCulture) + "}," +
                "{\"technicalName\":\"GetWeightPerMeter\",\"declaringType\":\"Beam\",\"status\":\"Read\",\"rawValue\":" + kgm.ToString(System.Globalization.CultureInfo.InvariantCulture) + "}," +
                "{\"technicalName\":\"GetWeight\",\"declaringType\":\"Beam\",\"argument\":\"1\",\"status\":\"Read\",\"rawValue\":" + (len * kgm).ToString(System.Globalization.CultureInfo.InvariantCulture) + "}," +
                "{\"technicalName\":\"GetPaintArea\",\"declaringType\":\"Beam\",\"status\":\"Read\",\"rawValue\":" + paintMm2.ToString(System.Globalization.CultureInfo.InvariantCulture) + "}]}";
            static string Plate(string h, double t, double areaMm2, double grams, double paintMm2) =>
                "{\"handle\":\"" + h + "\",\"clrType\":\"A.Plate\",\"inheritance\":[\"Plate\",\"PlateBase\",\"AtomicElement\",\"FilerObject\"],\"properties\":[" +
                "{\"technicalName\":\"Thickness\",\"declaringType\":\"PlateBase\",\"status\":\"Read\",\"rawValue\":" + t.ToString(System.Globalization.CultureInfo.InvariantCulture) + "}," +
                "{\"technicalName\":\"Material\",\"declaringType\":\"AtomicElement\",\"status\":\"Read\",\"rawValue\":\"S235JRG2\"}," +
                "{\"technicalName\":\"GetArea\",\"declaringType\":\"PlateBase\",\"status\":\"Read\",\"rawValue\":" + areaMm2.ToString(System.Globalization.CultureInfo.InvariantCulture) + "}," +
                "{\"technicalName\":\"GetWeight\",\"declaringType\":\"PlateBase\",\"status\":\"Read\",\"rawValue\":" + grams.ToString(System.Globalization.CultureInfo.InvariantCulture) + "}," +
                "{\"technicalName\":\"GetPaintArea\",\"declaringType\":\"PlateBase\",\"status\":\"Read\",\"rawValue\":" + paintMm2.ToString(System.Globalization.CultureInfo.InvariantCulture) + "}]}";
            static string Bolts(string h, string cls, string inh, double d, double l, int n, double grams) =>
                "{\"handle\":\"" + h + "\",\"clrType\":\"A." + cls + "\",\"inheritance\":[\"" + cls + "\"," + inh + "\"BoltPattern\",\"AtomicElement\",\"FilerObject\"],\"properties\":[" +
                "{\"technicalName\":\"ScrewDiameter\",\"declaringType\":\"ScrewBoltPattern\",\"status\":\"Read\",\"rawValue\":" + d.ToString(System.Globalization.CultureInfo.InvariantCulture) + "}," +
                "{\"technicalName\":\"ScrewLength\",\"declaringType\":\"ScrewBoltPattern\",\"status\":\"Read\",\"rawValue\":" + l.ToString(System.Globalization.CultureInfo.InvariantCulture) + "}," +
                "{\"technicalName\":\"Grade\",\"declaringType\":\"ScrewBoltPattern\",\"status\":\"Read\",\"rawValue\":\"10.9\"}," +
                "{\"technicalName\":\"Standard\",\"declaringType\":\"ScrewBoltPattern\",\"status\":\"Read\",\"rawValue\":\"14399-4\"}," +
                "{\"technicalName\":\"NumberOfScrews\",\"declaringType\":\"BoltPattern\",\"status\":\"Read\",\"rawValue\":" + n + "}," +
                "{\"technicalName\":\"GetWeight\",\"declaringType\":\"" + (cls == "AnchorPattern" ? "AnchorPattern" : "ScrewBoltPattern") + "\",\"status\":\"Read\",\"rawValue\":" + grams.ToString(System.Globalization.CultureInfo.InvariantCulture) + "}]}";
            Extraction x = DiagnosticImporter.Import(Run_, new[]
            {
                Bar("1", "U100", 1084.5, 10.6, 403500), Bar("2", "U100", 965.5, 10.6, 359200), Bar("3", "U100", 1669, 10.6, 620900), Bar("4", "HEA200", 3000, 42.3, 3420000),
                Plate("P1", 4.75, 19600, 730.835, 42012), Plate("P2", 4.75, 19600, 730.835, 42012), Plate("P3", 12.5, 40000, 3925, 85000),
                Bolts("B1", "FinitRectScrewBoltPattern", "\"ScrewBoltPattern\",", 12, 35, 4, 149.44), Bolts("B2", "FinitRectScrewBoltPattern", "\"ScrewBoltPattern\",", 12, 35, 2, 74.72),
                Bolts("B3", "FinitRectScrewBoltPattern", "\"ScrewBoltPattern\",", 16, 50, 6, 600), Bolts("A1", "AnchorPattern", "\"ScrewBoltPattern\",", 20, 400, 4, 4000),
            });
            MaterialTable s = MaterialTables.Build(x, MaterialTableKind.ProfileSummary, "T.dwg");
            True(s.Rows.Count == 2 && s.Rows[1][1] == "U100" && s.Rows[1][3] == "3", "U100 num único item com 3 barras");
            True(s.Rows[1][4] == "3,719" && s.Rows[1][5] == "10,60" && s.Rows[1][6] == "39,42", "comprimento total, kg/m e kg: " + string.Join("|", s.Rows[1]));
            True(s.Rows[1][7] == "1,384" && s.Total![3] == "4" && s.Total[6] == "166,32", "pintura e total: " + string.Join("|", s.Total!));
            MaterialTable b = MaterialTables.Build(x, MaterialTableKind.Bolts, "T.dwg");
            True(b.Rows.Count == 2 && b.Rows[0][1] == "M12 x 35" && b.Rows[0][4] == "6" && b.Rows[0][2] == "10.9" && b.Rows[0][3] == "14399-4", "parafusos M12: " + string.Join("|", b.Rows[0]));
            True(b.Total![4] == "12" && b.Rows.All(r => !r[1].Contains("400")), "total de parafusos sem chumbadores");
            MaterialTable a = MaterialTables.Build(x, MaterialTableKind.Anchors, "T.dwg");
            True(a.Rows.Count == 1 && a.Rows[0][4] == "4" && a.Rows[0][5] == "4,000", "chumbadores: " + string.Join("|", a.Rows[0]));
            MaterialTable p = MaterialTables.Build(x, MaterialTableKind.Plates, "T.dwg");
            True(p.Rows.Count == 2 && p.Rows[0][1] == "4,75" && p.Rows[0][3] == "2" && p.Rows[0][4] == "0,039" && p.Rows[0][5] == "1,46", "chapas #4,75: " + string.Join("|", p.Rows[0]));
            True(p.Rows[1][1] == "12,50" && p.Total![3] == "3", "chapas #12,50 e total");
            True(MaterialTables.Build(DiagnosticImporter.Import(Run_, new[] { Bar("9", "U100", 1000, 10.6, 1) }), MaterialTableKind.Anchors, "T").IsEmpty, "sem chumbadores = tabela vazia");
            True(MaterialTables.TryParseKind("profile_summary", out var k) && k == MaterialTableKind.ProfileSummary && !MaterialTables.TryParseKind("xyz", out _), "nomes do conector");
            JsonObject j = MaterialTables.ToJson(s);
            True(j["columns"]!.AsArray().Count == 8 && j["rows"]!.AsArray().Count == 2, "json da tabela");
        });

        Run("numeração itemizada: itens por espessura, perfil e diâmetro x comprimento", () =>
        {
            static string P(string tn, string dt, string v, string? arg = null) =>
                "{\"technicalName\":\"" + tn + "\",\"declaringType\":\"" + dt + "\"" + (arg != null ? ",\"argument\":\"" + arg + "\"" : "") + ",\"status\":\"Read\",\"rawValue\":" + v + "}";
            static string E(string h, string cls, string inh, params string[] props) =>
                "{\"handle\":\"" + h + "\",\"clrType\":\"A." + cls + "\",\"inheritance\":[\"" + cls + "\"," + inh + "\"AtomicElement\",\"FilerObject\"],\"properties\":[" + string.Join(",", props) + "]}";
            string beam(string h, string sec, string mat = "S235", string note = "\"\"", string cls = "StraightBeam") =>
                E(h, cls, "\"Beam\",", P("ProfSectionName", "Beam", "\"" + sec + "\""), P("Material", "AtomicElement", "\"" + mat + "\""), P("Note", "AtomicElement", note), P("GetUserAttribute", "AtomicElement", "\"" + (h == "B3" ? "SAIR" : "") + "\"", "0"));
            string plate(string h, double t, string cls = "Plate") => E(h, cls, "\"PlateBase\",", P("Thickness", "PlateBase", t.ToString(System.Globalization.CultureInfo.InvariantCulture)), P("Material", "AtomicElement", "\"S275\""));
            string bolt(string h, double d, double l, string cls = "FinitRectScrewBoltPattern", string inh = "\"ScrewBoltPattern\",\"BoltPattern\",") =>
                E(h, cls, inh, P("ScrewDiameter", "ScrewBoltPattern", d.ToString(System.Globalization.CultureInfo.InvariantCulture)), P("ScrewLength", "ScrewBoltPattern", l.ToString(System.Globalization.CultureInfo.InvariantCulture)), P("Grade", "ScrewBoltPattern", "\"10.9\""));
            string compound = E("C1", "CompoundStraightBeam", "\"CompoundBeam\",\"Beam\",", P("ProfSectionName", "Beam", "\"Default\""), P("CompoundClassName", "CompoundBeam", "\"Compound2U\""));
            Extraction x = DiagnosticImporter.Import(Run_, new[]
            {
                beam("B1", "U100"), beam("B2", "U100"), beam("B3", "U100"), beam("B4", "W200x15"), beam("B5", "U100", cls: "BentBeam"), compound,
                plate("P1", 4.75), plate("P2", 4.75), plate("P3", 12.5), plate("P4", 4.75, "FoldedPlate"),
                bolt("S1", 12, 35), bolt("S2", 12, 35), bolt("S3", 16, 50), bolt("A1", 20, 400, "AnchorPattern", "\"ScrewBoltPattern\",\"BoltPattern\","),
                E("W1", "WeldLine", "\"WeldPattern\","),
            });
            var o = new ItemOptions();
            ItemResult r = ItemNumbering.Compute(x, o);
            string Show(ItemResult rr) => string.Join(" | ", rr.Rows.Select(w => $"{w.Mark}:{ItemNumbering.CategoryName(w.Category)} {w.Type} x{w.Quantity}"));
            // Ordem padrão: chapas, vigas, parafusos; chumbador desmarcado por padrão; solda ignorada.
            True(Show(r) == "1:Chapa 4.75 x3 | 2:Chapa 12.5 x1 | 3:Viga 2U (composto) x1 | 4:Viga U100 x4 | 5:Viga W200x15 x1 | 6:Parafuso Ø12 x 35 10.9 x2 | 7:Parafuso Ø16 x 50 10.9 x1", Show(r));
            True(r.Excluded.Single().Handle == "A1" && r.Excluded.Single().Reason == "categoria desmarcada", "chumbador fora");
            True(r.Assignments("")["P4"].mark == "1" && r.Assignments("")["B5"].mark == "4", "dobrada/curva agrupadas com as retas");

            o.GroupBent = false; o.Prefix = "M-"; o.Start = 10; o.Enabled.Add(ItemCategory.Anchor);
            o.Order = new() { ItemCategory.Beam, ItemCategory.Plate, ItemCategory.Bolt, ItemCategory.Anchor };
            ItemResult r2 = ItemNumbering.Compute(x, o);
            True(r2.Rows[0].Mark == "M-10" && r2.Rows[0].Category == ItemCategory.Beam, "prefixo, início e ordem das categorias");
            True(r2.Rows.Any(w => w.Type == "U100 (curva)" && w.Quantity == 1) && r2.Rows.Any(w => w.Type == "4.75 (dobrada)"), "separa curvas e dobradas");
            True(r2.Rows.Last().Category == ItemCategory.Anchor && r2.Rows.Last().Type == "Ø20 x 400 10.9", "chumbador por último");
            var asg = r2.Assignments("M-");
            True(asg["B1"] == ("M-11", "M-", "11") || asg["B1"].prefix == "M-", "atribuição com prefixo e número separados");

            o.SortBy = ItemSortBy.Quantity; o.Descending = true; o.GroupBent = true;
            ItemResult r3 = ItemNumbering.Compute(x, o);
            True(r3.Rows[0].Type == "U100" && r3.Rows[0].Quantity == 4, "vigas por quantidade, decrescente");

            o.Filters.Add(new ItemFilter(FilterField.UserAttribute1, FilterOp.NotContains, "sair"));
            o.Filters.Add(new ItemFilter(FilterField.Type, FilterOp.NotEquals, "w200x15"));
            ItemResult r4 = ItemNumbering.Compute(x, o);
            True(r4.Excluded.Any(e => e.Handle == "B3" && e.Reason.StartsWith("filtro: Atributo do usuário 1 não contém")), "filtro por atributo");
            True(r4.Excluded.Any(e => e.Handle == "B4") && !r4.Rows.Any(w => w.Type == "W200x15"), "filtro por tipo (sem diferenciar maiúsculas)");
            True(r4.Rows.First(w => w.Type == "U100").Quantity == 3, "B3 fora, campo dele não seria alterado");
            True(NaturalComparer.Instance.Compare("4.75", "12.5") < 0 && NaturalComparer.Instance.Compare("U100", "U80") > 0 && NaturalComparer.Instance.Compare("L50x5", "L100x10") < 0, "ordem natural");
        });

        Run("janela de numeração: prévia, prefixo, ordem e opções lembradas", () =>
        {
            const string beam = """{"handle":"B1","clrType":"A.StraightBeam","inheritance":["StraightBeam","Beam","AtomicElement","FilerObject"],"properties":[{"technicalName":"ProfSectionName","declaringType":"Beam","status":"Read","rawValue":"U100"}]}""";
            const string plate = """{"handle":"P1","clrType":"A.Plate","inheritance":["Plate","PlateBase","AtomicElement","FilerObject"],"properties":[{"technicalName":"Thickness","declaringType":"PlateBase","status":"Read","rawValue":6.3}]}""";
            Extraction x = DiagnosticImporter.Import(Run_, new[] { beam, plate });
            var o = new ItemOptions();
            using var dlg = new NumberingDialog(x, o);
            True(dlg.Result.Rows.Count == 2 && dlg.Result.Rows[0].Category == ItemCategory.Plate && dlg.Result.Rows[0].Type == "6.3", "prévia inicial: chapa primeiro");
            var prefix = (System.Windows.Forms.TextBox)typeof(NumberingDialog).GetField("_prefix", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(dlg)!;
            prefix.Text = "IT-";
            True(dlg.Result.Rows[0].Mark == "IT-1" && dlg.Result.Rows[1].Mark == "IT-2", "prefixo atualiza a prévia: " + string.Join(",", dlg.Result.Rows.Select(r => r.Mark)));
            var cats = (System.Windows.Forms.DataGridView)typeof(NumberingDialog).GetField("_cats", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(dlg)!;
            typeof(NumberingDialog).GetMethod("MoveCategory", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(dlg, new object[] { 1, -1 });
            True(o.Order[0] == ItemCategory.Beam && dlg.Result.Rows[0].Type == "U100", "↑ coloca vigas antes das chapas");
            cats.Rows[0].Cells[0].Value = false; dlg.Collect();
            True(dlg.Result.Rows.Single().Category == ItemCategory.Plate && dlg.Result.Excluded.Single().Reason == "categoria desmarcada", "categoria desmarcada sai da numeração");
            string path = Path.Combine(Temp, "numbering.json");
            o.Filters.Add(new ItemFilter(FilterField.Material, FilterOp.IsEmpty, ""));
            NumberingSettings.Save(o, path);
            ItemOptions r = NumberingSettings.Load(path);
            True(r.Prefix == "IT-" && r.Order[0] == ItemCategory.Beam && !r.Enabled.Contains(ItemCategory.Beam) && r.Filters.Single().Op == FilterOp.IsEmpty, "opções lembradas");
        });

        Run("contraventamento: reproduz as medidas do exemplo do SteelX (diagonal 1)", () =>
        {
            var a = new V3(330.0128, 95.7679, 0); var b = new V3(1389.5217, 1353.1876, 0);
            double L = (b - a).Length;
            var p = new BracingParams { Diagonals = BracingDiagonals.Diagonal1, TurnbuckleMode = TurnbucklePositionMode.FromCorner1, TurnbucklePosition = 1079.8 };
            BracingResult r = BracingGeometry.Build(a, b, p);
            True(r.Errors.Count == 0, string.Join("; ", r.Errors));
            V3 u = (b - a).Unit;
            double S(V3 pt) => V3.Dot(pt - a, u);
            bool Near(double x, double y, double tol = 0.15) => Math.Abs(x - y) < tol;
            var rods = r.Parts.Where(x => x.Kind == BracingPartKind.Rod).ToList();
            var thr = r.Parts.Where(x => x.Kind == BracingPartKind.ThreadedRod).ToList();
            True(rods.Count == 2 && thr.Count == 2, "2 barras lisas e 2 roscadas");
            True(Near(Math.Min(S(rods[0].Start), S(rods[0].End)), 114.5) && Near(Math.Max(S(rods[0].Start), S(rods[0].End)), 929.8), "barra 1: 114,5 a 929,8");
            True(Near(Math.Min(S(thr[0].Start), S(thr[0].End)), 929.8) && Near(Math.Max(S(thr[0].Start), S(thr[0].End)), 1054.8), "rosca 1: 929,8 a 1054,8");
            True(Near(Math.Min(S(thr[1].Start), S(thr[1].End)), 1104.8) && Near(Math.Max(S(thr[1].Start), S(thr[1].End)), 1229.8), "rosca 2: 1104,8 a 1229,8");
            True(Near(Math.Min(S(rods[1].Start), S(rods[1].End)), 1229.8) && Near(Math.Max(S(rods[1].Start), S(rods[1].End)), L - 114.5), "barra 2 até 114,5 do canto 2");
            var cleat = r.Parts.Single(x => x.Kind == BracingPartKind.Cleat);
            True(Near(S(cleat.Start), 1009.8) && Near(S(cleat.End), 1149.8), "cantoneira 1009,8 a 1149,8");
            var bolts = r.Parts.Where(x => x.Kind == BracingPartKind.Bolt).Select(x => S(x.Origin)).OrderBy(x => x).ToList();
            True(bolts.Count == 2 && Near(bolts[0], 84.5) && Near(bolts[1], L - 84.5), "parafusos a 84,5 dos cantos");
            var welds = r.Parts.Where(x => x.Kind == BracingPartKind.Weld && x.Size == 5).Select(x => S(x.Origin)).OrderBy(x => x).ToList();
            True(Near(welds[0], 164.5) && Near(welds[1], L - 164.5), "solda barra-gusset a 164,5");
            var g = r.Parts.First(x => x.Kind == BracingPartKind.Gusset);
            True(Near(S(g.Polygon[0]), 54.5) && Near(S(g.Polygon[1]), 214.5) && Near(g.Polygon[0].Z, 6.35) && g.Thickness == 6.35, "gusset 54,5 a 214,5, topo em 6,35 (sobre a chapa de base)");
            True(Near(rods[0].Start.Z, 6.35 + 6.35), "eixo da barra apoiado no gusset");
            True(r.Parts.Count(x => x.Kind == BracingPartKind.Nut) == 4 && r.Parts.Count(x => x.Kind == BracingPartKind.Washer) == 4, "porca e arruela dos dois lados de cada chapa de topo");
            True(r.Parts.Count(x => x.Kind == BracingPartKind.SpliceBar) == 4 && r.Parts.Count(x => x.Kind == BracingPartKind.EndPlate) == 2, "talas e chapas de topo");
            // Toda solda e parafuso ligam peças existentes.
            True(r.Parts.Where(x => x.Kind is BracingPartKind.Weld or BracingPartKind.Bolt).All(x => x.Connects.Length >= 2 && x.Connects.All(i => i >= 0 && i < r.Parts.Count && r.Parts[i].Kind is not (BracingPartKind.Weld or BracingPartKind.Bolt))), "conexões válidas");
        });
        Run("contraventamento: X, opções e validação", () =>
        {
            var a = new V3(0, 0, 3000); var b = new V3(6000, 4000, 7777);
            var p = new BracingParams();
            BracingResult r = BracingGeometry.Build(a, b, p);
            True(r.Errors.Count == 0 && r.DiagonalLines.Count == 2, "X com 2 diagonais (padrão 65%): " + string.Join("; ", r.Errors));
            True(BracingGeometry.Build(a, b, new BracingParams { TurnbucklePosition = 50 }).Errors.Any(e => e.Contains("se chocam")), "X a 50%: esticadores no cruzamento são recusados");
            True(r.Parts.Where(x => x.Kind is BracingPartKind.Rod).All(x => Math.Abs(x.Start.Z - (3000 + 6.35 + 6.35)) < 1e-6), "tudo no Z do primeiro ponto (chapa de base abaixo, gusset em cima)");
            var (q1, q2) = r.DiagonalLines[1];
            True(q1.X == 0 && q1.Y == 4000 && q2.X == 6000 && q2.Y == 0, "diagonal 2 entre os outros cantos");
            var cl = r.Parts.Where(x => x.Kind == BracingPartKind.Cleat).ToList();
            True(Math.Abs(((cl[0].Start + cl[0].End) * 0.5 - (a with { Z = 0 }) - new V3(0, 0, cl[0].Start.Z)).Length - 7211.10 * 0.65) < 0.1, "esticador a 65% da diagonal");
            p.BasePlate = false; p.Bolt = false; p.NutsAndWashers = false; p.SpliceBars = false; p.Diagonals = BracingDiagonals.Diagonal2;
            BracingResult r2 = BracingGeometry.Build(a, b, p);
            True(r2.Errors.Count == 0 && !r2.Parts.Any(x => x.Kind is BracingPartKind.BasePlate or BracingPartKind.Bolt or BracingPartKind.Nut or BracingPartKind.Washer or BracingPartKind.SpliceBar), "opções desligadas");
            True(r2.Parts.Where(x => x.Kind == BracingPartKind.Gusset).All(x => Math.Abs(x.Polygon[0].Z - (3000 + 6.35)) < 1e-6), "sem chapa de base, gusset apoiado no Z do ponto");
            p.Bolt = true;
            True(BracingGeometry.Build(a, b, p).Errors.Any(e => e.Contains("chapa de base")), "parafuso sem chapa de base é recusado");
            p.Bolt = false; p.TurnbuckleMode = TurnbucklePositionMode.FromCorner1; p.TurnbucklePosition = 100;
            True(BracingGeometry.Build(a, b, p).Errors.Any(e => e.Contains("gusset do canto 1")), "esticador em cima do gusset é recusado");
            True(BracingGeometry.Build(a, new V3(0, 5000, 0), new BracingParams()).Errors.Any(e => e.Contains("retângulo")), "pontos alinhados são recusados");
            string json = p.ToJson();
            True(BracingParams.FromJson(json).TurnbucklePosition == 100 && BracingParams.FromJson(json).Diagonals == BracingDiagonals.Diagonal2, "parâmetros ida e volta em JSON");
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
