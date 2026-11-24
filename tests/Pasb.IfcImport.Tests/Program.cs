using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Pasb.IfcImport.Model;

// Testes da importação IFC: roda o Pasb.IfcImport.exe compilado (xBIM) no IFC sintético de 3 níveis
// (fixtures/niveis.ifc, gerado por fixtures/gera_ifc_niveis.py) e confere o resumo e as malhas.
int failed = 0, passed = 0;
void Run(string name, Action t)
{
    try { t(); passed++; Console.WriteLine("ok    " + name); }
    catch (Exception ex) { failed++; Console.WriteLine("FALHA " + name + ": " + ex.Message); }
}
void True(bool c, string what) { if (!c) throw new Exception(what); }

string tmp = Path.Combine(Path.GetTempPath(), "pasb-import-tests-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(tmp);
string ifc = Path.Combine(AppContext.BaseDirectory, "niveis.ifc");
string exe = FindExe();

(int code, string err, string output) Exe(params string[] args)
{
    var psi = new ProcessStartInfo(exe) { UseShellExecute = false, RedirectStandardError = true, RedirectStandardOutput = true };
    foreach (string a in args) psi.ArgumentList.Add(a);
    using Process p = Process.Start(psi)!;
    string o = p.StandardOutput.ReadToEnd();
    string e = p.StandardError.ReadToEnd();
    p.WaitForExit();
    return (p.ExitCode, e, o);
}

MeshPackage Convert(ConvertRequest r)
{
    string req = Path.Combine(tmp, Guid.NewGuid().ToString("N") + ".json"), outp = Path.ChangeExtension(req, ".pasbmesh");
    File.WriteAllText(req, JsonSerializer.Serialize(r, ScanJson.Options));
    var (code, err, _) = Exe("convert", req, outp);
    if (code != 0) throw new Exception("convert falhou: " + err);
    using var fs = File.OpenRead(outp);
    return MeshPackage.Read(fs);
}

try
{
    IfcScan scan = new();
    Run("scan: tipos, níveis com cota e layers", () =>
    {
        string outp = Path.Combine(tmp, "scan.json");
        var (code, err, _) = Exe("scan", ifc, outp);
        True(code == 0, "scan falhou: " + err);
        scan = ScanJson.Deserialize(File.ReadAllText(outp));
        True(scan.UnitsPerMetre == 1000 && scan.Schema == "Ifc4", "schema/unidade");
        True(scan.Storeys.Select(s => s.Name).SequenceEqual(new[] { "Terreo", "1o Pavimento", "Cobertura" }), "níveis em ordem de cota: " + string.Join(",", scan.Storeys.Select(s => s.Name)));
        True(scan.Storeys.Select(s => s.Elevation).SequenceEqual(new double?[] { 0, 3000, 6000 }), "cotas");
        True(scan.Storeys[0].Count == 10 && scan.Storeys[2].Count == 2, "elementos por nível: " + string.Join(",", scan.Storeys.Select(s => s.Count)));
        var t = scan.Types.ToDictionary(x => x.Name, x => x.Count);
        True(t["IfcColumn"] == 8 && t["IfcBeam"] == 2 && t["IfcMember"] == 6 && t["IfcSlab"] == 2 && t["IfcSpace"] == 2 && t["IfcOpeningElement"] == 2, "tipos");
        True(scan.Layers.SequenceEqual(new[] { "A-GLAZ", "S-BEAM", "S-COLUMN", "S-SLAB" }), "layers do IFC");
    });

    Run("convert padrão: sem aberturas e espaços; geometria em metros", () =>
    {
        MeshPackage p = Convert(new ConvertRequest { IfcFile = ifc });
        True(p.Instances.Count == 18, "18 elementos: " + p.Instances.Count);
        True(p.Instances.All(i => i.IfcType != "IfcOpeningElement" && i.IfcType != "IfcSpace"), "sem aberturas/espaços");
        // Pilar P2 do 1º pavimento: centro em (6; 0) m, de 3,0 a 5,8 m de altura.
        MeshInstance p2 = p.Instances.Single(i => i.Name == "P2" && i.StoreyKey == scan.Storeys[1].Key);
        MeshGeometry g = p.Geometries.Single(x => x.Id == p2.GeometryId);
        var pts = Enumerable.Range(0, g.VertexCount).Select(k => MeshPackage.Apply(p2.Transform, g.Vertices[3 * k], g.Vertices[3 * k + 1], g.Vertices[3 * k + 2])).ToList();
        True(Math.Abs(pts.Min(q => q.x) - 5.85) < 1e-6 && Math.Abs(pts.Max(q => q.x) - 6.15) < 1e-6, "x do pilar: " + pts.Min(q => q.x) + ".." + pts.Max(q => q.x));
        True(Math.Abs(pts.Min(q => q.z) - 3.0) < 1e-6 && Math.Abs(pts.Max(q => q.z) - 5.8) < 1e-6, "z do pilar: " + pts.Min(q => q.z) + ".." + pts.Max(q => q.z));
        True(g.TriangleCount == 12, "caixa = 12 triângulos");
        True(p2.Color != null && p2.Color[0] == 191 && p2.Color[2] == 184 && p2.IfcLayer == "S-COLUMN", "cor do concreto e layer");
    });

    Run("peças repetidas compartilham uma geometria (bloco)", () =>
    {
        MeshPackage p = Convert(new ConvertRequest { IfcFile = ifc, Types = { "IfcMember" } });
        True(p.Instances.Count == 6 && p.Instances.Select(i => i.GeometryId).Distinct().Count() == 1, "6 montantes, 1 geometria");
        True(p.Geometries.Single().ReferenceCount == 6, "contagem de reuso");
        True(p.Instances.Select(i => i.Transform[12]).Distinct().Count() == 3, "posições diferentes");
    });

    Run("filtro por nível e opções avançadas", () =>
    {
        MeshPackage roof = Convert(new ConvertRequest { IfcFile = ifc, Storeys = { scan.Storeys[2].Key } });
        True(roof.Instances.Count == 2 && roof.Instances.All(i => i.IfcType == "IfcBeam"), "cobertura: só as 2 vigas");
        MeshPackage all = Convert(new ConvertRequest { IfcFile = ifc, IncludeOpenings = true, IncludeSpaces = true });
        True(all.Instances.Count == 22, "com aberturas e espaços: " + all.Instances.Count);
        True(all.Instances.Where(i => i.IfcType == "IfcSpace").All(i => i.StoreyKey != MeshInstance.NoStorey), "espaços no nível certo");
    });

    Run("pacote de malhas: ida e volta", () =>
    {
        var p = new MeshPackage();
        p.Geometries.Add(new MeshGeometry { Id = 1, ReferenceCount = 2, Vertices = new[] { 0.0, 0, 0, 1, 0, 0, 0, 1, 0 }, Triangles = new[] { 0, 1, 2 } });
        p.Instances.Add(new MeshInstance { GeometryId = 1, IfcType = "IfcWall", Name = "Parede ç", StoreyKey = "abc", Color = new byte[] { 1, 2, 3 }, Transparency = 0.5 });
        p.Instances.Add(new MeshInstance { GeometryId = 1, IfcType = "IfcWall" });
        p.Warnings.Add("aviso");
        var ms = new MemoryStream(); p.Write(ms); ms.Position = 0;
        MeshPackage r = MeshPackage.Read(ms);
        True(r.Geometries[0].Triangles.SequenceEqual(new[] { 0, 1, 2 }) && r.Instances[0].Name == "Parede ç" && r.Instances[0].Color![2] == 3 && r.Instances[1].Name == null, "conteúdo");
        True(r.Warnings.Single() == "aviso" && r.Instances[0].Transparency == 0.5, "avisos e transparência");
        bool bad = false; try { MeshPackage.Read(new MemoryStream(Encoding.ASCII.GetBytes("LIXO-QUALQUER"))); } catch (InvalidDataException) { bad = true; }
        True(bad, "arquivo inválido é recusado");
    });

    Run("nomes de layer válidos e cores por nível", () =>
    {
        True(ImportNaming.Layer("IFC_", "Pav. 1/2: \"A\"") == "IFC_Pav. 1_2_ _A_", "caracteres inválidos: " + ImportNaming.Layer("IFC_", "Pav. 1/2: \"A\""));
        True(ImportNaming.Layer("IFC_", null) == "IFC_SemNome", "nome vazio");
        True(ImportNaming.StoreyColor(0) == 1 && ImportNaming.StoreyColor(16) == 1 && ImportNaming.StoreyColor(1) != ImportNaming.StoreyColor(0), "paleta cíclica");
    });

    Run("progresso: só avança, tem etapas em português e termina em 100%", () =>
    {
        foreach (string mode in new[] { "scan", "convert" })
        {
            string a1, a2 = Path.Combine(tmp, "p-" + mode + ".out");
            if (mode == "scan") a1 = ifc;
            else { a1 = Path.Combine(tmp, "p-req.json"); File.WriteAllText(a1, JsonSerializer.Serialize(new ConvertRequest { IfcFile = ifc }, ScanJson.Options)); }
            var (code, err, output) = Exe(mode, a1, a2);
            True(code == 0, mode + " falhou: " + err);
            var lines = output.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries).Where(l => l.StartsWith("PROGRESSO ")).ToList();
            var pcts = lines.Select(l => int.Parse(l.Split(' ')[1])).ToList();
            True(pcts.Count >= 3 && pcts.Last() == 100, mode + ": termina em 100% (" + string.Join(",", pcts) + ")");
            True(pcts.Zip(pcts.Skip(1)).All(x => x.Second >= x.First), mode + ": nunca volta (" + string.Join(",", pcts) + ")");
            True(lines.All(l => l.Split(' ', 3).Length == 3), mode + ": toda linha tem etapa");
            if (mode == "convert") True(lines.Any(l => l.Contains("Gerando a geometria das peças")) && lines.Any(l => l.Contains("Peças repetidas")), "etapas da geometria");
        }
    });

    Run("recorte com contorno quebrado não interrompe a importação", () =>
    {
        // Antes da 0.14.3 a xBIM parava o modelo inteiro ("Processing halted due to model error").
        MeshPackage p = Convert(new ConvertRequest { IfcFile = Path.Combine(AppContext.BaseDirectory, "recorte_quebrado.ifc") });
        MeshInstance a = p.Instances.Single(i => i.Name == "Parede A arco quebrado");
        MeshInstance b = p.Instances.Single(i => i.Name == "Parede B contorno aberto");
        MeshGeometry ga = p.Geometries.Single(x => x.Id == a.GeometryId), gb = p.Geometries.Single(x => x.Id == b.GeometryId);
        Console.WriteLine($"      parede A: {ga.TriangleCount} triângulos, parede B: {gb.TriangleCount}");
        True(ga.TriangleCount > 0 && gb.TriangleCount > 0, "as duas paredes têm malha");
        True(p.Warnings.Any(w => w.Contains("Parede A") && w.Contains("trocado por reta")), "aviso do arco reparado: " + string.Join(" | ", p.Warnings));
        True(p.Warnings.Any(w => w.Contains("Parede B") && w.Contains("sem esse recorte")), "aviso do recorte descartado: " + string.Join(" | ", p.Warnings));
        True(!p.Warnings.Any(w => w.Contains("Parede A") && w.Contains("sem esse recorte")), "parede A manteve o recorte");
    });

    Run("IFC inexistente: erro claro", () =>
    {
        var (code, err, _) = Exe("scan", Path.Combine(tmp, "nao-existe.ifc"), Path.Combine(tmp, "x.json"));
        True(code == 1 && err.Length > 0, "código 1 com mensagem");
    });
}
finally
{
    try { Directory.Delete(tmp, true); } catch (Exception) { }
}

Console.WriteLine($"{passed} passaram, {failed} falharam");
return failed == 0 ? 0 : 1;

static string FindExe()
{
    DirectoryInfo? d = new(AppContext.BaseDirectory);
    while (d != null && !File.Exists(Path.Combine(d.FullName, "PluginAdvanceSteelBrunno.slnx"))) d = d.Parent;
    if (d == null) throw new Exception("raiz do repositório não encontrada");
    string p = Path.Combine(d.FullName, "src", "Pasb.IfcImport", "bin", "Release", "net10.0-windows", "win-x64", "Pasb.IfcImport.exe");
    if (!File.Exists(p)) throw new Exception("Pasb.IfcImport.exe não compilado (dotnet build src\\Pasb.IfcImport -c Release)");
    return p;
}
