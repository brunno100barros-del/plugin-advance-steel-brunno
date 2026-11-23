using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Pasb.Georeferencing;
using Pasb.IfcExport;
using Pasb.Model;

int failed = 0, passed = 0;
void Run(string name, Action t)
{
    try { t(); passed++; Console.WriteLine("ok    " + name); }
    catch (Exception ex) { failed++; Console.WriteLine("FALHA " + name + ": " + ex.Message); }
}
void True(bool c, string what) { if (!c) throw new Exception(what); }

const string Run1 = """{"tool":"PASB Diagnostics","insunits":"Millimeters","transactionMode":"write-discarded","startedUtc":"2026-11-20T10:00:00Z"}""";
const string Plate = """{"handle":"C7","uniqueId":"8acce620-b3c0-4c40-9dd0-2cb000b8bcbf","clrType":"Autodesk.AdvanceSteel.Modelling.Plate","inheritance":["Plate","PlateBase","MainAlias","AtomicElement","ActiveConstructionElement","ConstructionElement","FilerObject"],"properties":[{"technicalName":"GetBaseContourPolygon","declaringType":"PlateBase","argument":"0","source":"method","dataType":"P","status":"Read","rawValue":[[0,0,0],[196,0,0],[196,100,0],[0,100,0]]},{"technicalName":"PlateNormal","declaringType":"PlateBase","source":"getter","dataType":"V","status":"Read","rawValue":[0,0,1]},{"technicalName":"Thickness","declaringType":"PlateBase","source":"getter","dataType":"System.Double","status":"Read","rawValue":4.75},{"technicalName":"Material","declaringType":"AtomicElement","source":"getter","dataType":"System.String","status":"Read","rawValue":"Aço çã 'x'"},{"technicalName":"Note","declaringType":"AtomicElement","source":"getter","dataType":"System.String","status":"Empty","rawValue":""},{"technicalName":"Camber","declaringType":"AtomicElement","source":"getter","dataType":"X","status":"Null","rawValue":null},{"technicalName":"IsMainPart","declaringType":"AtomicElement","source":"getter","dataType":"System.Boolean","status":"Read","rawValue":true},{"technicalName":"NumberOfHoles","declaringType":"AtomicElement","source":"getter","dataType":"System.Int32","status":"Read","rawValue":4},{"technicalName":"GetWeight","declaringType":"PlateBase","source":"method","dataType":"System.Double","status":"Read","rawValue":730.835}]}""";
const string Bolt = """{"handle":"C9","clrType":"Autodesk.AdvanceSteel.Modelling.FinitRectScrewBoltPattern","inheritance":["FinitRectScrewBoltPattern","CountableScrewBoltPattern","ScrewBoltPattern","BoltPattern","AtomicElement","FilerObject"],"properties":[{"technicalName":"GetMidpoints","declaringType":"BoltPattern","source":"method","dataType":"P","status":"Read","rawValue":[[30,30,2],[160,30,2]]},{"technicalName":"Normal","declaringType":"BoltPattern","source":"getter","dataType":"V","status":"Read","rawValue":[0,0,1]},{"technicalName":"ScrewDiameter","declaringType":"ScrewBoltPattern","source":"getter","dataType":"System.Double","status":"Read","rawValue":12},{"technicalName":"ScrewLength","declaringType":"ScrewBoltPattern","source":"getter","dataType":"System.Double","status":"Read","rawValue":35}]}""";
const string Joint = """{"handle":"CB","clrType":"Autodesk.AdvanceSteel.ConstructionTypes.ConnectionConstructionObject","inheritance":["ConnectionConstructionObject","PassiveConstructionObject","ConstructionElement","FilerObject"],"properties":[{"technicalName":"Drivers","declaringType":"PassiveConstructionObject","source":"getter","dataType":"L","status":"Read","rawValue":[{"handle":"C7"},{"handle":"C9"}]}]}""";
const string Internal = """{"handle":"83","openError":{"type":"X","message":"null"},"identification":{"autocadClass":"AstCounter"}}""";

Extraction x = DiagnosticImporter.Import(Run1, new[] { Plate, Bolt, Joint, Internal });
var geo = new GeoParameters(GeoMode.SirgasUtm, new Point3(0, 0, 0), new Point3(500_000, 7_400_000, 800), 30, 0.001, 22, Hemisphere.South);
string ifc = IfcExporter.Export(x, new ExportOptions(Geo: geo), out ExportReport rep);

Run("cabeçalho e esquema IFC4", () =>
{
    True(ifc.StartsWith("ISO-10303-21;"), "início"); True(ifc.Contains("FILE_SCHEMA(('IFC4'));"), "schema");
    True(ifc.TrimEnd().EndsWith("END-ISO-10303-21;"), "fim");
});
Run("integridade: toda referência #n existe e ids são sequenciais", () =>
{
    var defined = new HashSet<int>(Regex.Matches(ifc, @"^#(\d+)=", RegexOptions.Multiline).Select(m => int.Parse(m.Groups[1].Value)));
    True(defined.Count > 0 && defined.Max() == defined.Count, "sequência");
    foreach (Match m in Regex.Matches(ifc, @"(?<!^)#(\d+)", RegexOptions.Multiline))
        True(defined.Contains(int.Parse(m.Groups[1].Value)), "referência inexistente #" + m.Groups[1].Value);
});
Run("parênteses balanceados em cada entidade", () =>
{
    foreach (string line in ifc.Split('\n').Where(l => l.StartsWith('#')))
    {
        string noStr = Regex.Replace(line, "'(?:[^']|'')*'", "''");
        True(noStr.Count(c => c == '(') == noStr.Count(c => c == ')'), "desbalanceado: " + line);
    }
});
Run("classes IFC por categoria; interno ignorado", () =>
{
    True(ifc.Contains("=IFCPLATE("), "chapa"); True(ifc.Contains("=IFCMECHANICALFASTENER("), "parafuso");
    True(ifc.Contains("=IFCGROUP("), "ligação"); True(rep.SkippedByCategory["internal"] == 1, "interno");
});
Run("georreferenciamento IfcProjectedCRS + IfcMapConversion", () =>
{
    True(ifc.Contains("=IFCPROJECTEDCRS('EPSG:31982'"), "CRS"); True(ifc.Contains("=IFCMAPCONVERSION("), "map conversion");
    True(rep.Georeference!["zone"]!.ToString() == "22S", "zona no relatório");
});
Run("coordenadas reais aplicadas na posição do IfcSite (padrão) e IfcMapConversion neutro", () =>
{
    True(ifc.Contains("=IFCCARTESIANPOINT((500000.,7400000.,800.));"), "origem do site em E,N,H");
    string cos = StepWriter.Real(Math.Cos(Math.PI / 6)), sin = StepWriter.Real(Math.Sin(Math.PI / 6));
    True(ifc.Contains($"=IFCDIRECTION(({cos},{sin},0.));"), "eixo X girado 30°");
    True(Regex.IsMatch(ifc, @"=IFCMAPCONVERSION\(#\d+,#\d+,0\.,0\.,0\.,1\.,0\.,1\.\);"), "map conversion neutro");
    True(Regex.IsMatch(ifc, @"=IFCSITE\([^;]*#(\d+),\$,\$,\.ELEMENT\."), "site com posição");
});
Run("caso real do teste: ponto base (-84,5; -50) → (493031,442; 2998,078; 23,02)", () =>
{
    var g = new GeoParameters(GeoMode.LocalXyz, new Point3(-84.5, -50, 0), new Point3(493031.442, 2998.078, 23.02), 0, 0.001, LocalCrsName: "Obra");
    string s = IfcExporter.Export(x, new ExportOptions(Geo: g), out _);
    True(s.Contains("=IFCCARTESIANPOINT((493031.5265,2998.128,23.02));"), "origem do site");
    // Conferência: origem do site + ponto base (em m) = coordenada real digitada.
    True(Math.Abs(493031.5265 + (-84.5 * 0.001) - 493031.442) < 1e-9 && Math.Abs(2998.128 + (-50 * 0.001) - 2998.078) < 1e-9, "ponto base cai na coordenada real");
});
Run("modo antigo: só IfcMapConversion, site na origem", () =>
{
    string s = IfcExporter.Export(x, new ExportOptions(Geo: geo) { GeoInPlacement = false }, out ExportReport r3);
    True(Regex.IsMatch(s, @"=IFCMAPCONVERSION\(#\d+,#\d+,500000\.,7400000\.,800\.,"), "map conversion com valores");
    True(!s.Contains("((500000.,7400000.,800.))"), "site não deslocado");
    True(r3.Georeference!["appliedTo"]!.ToString() == "IfcMapConversion", "relatório");
});
Run("propriedades preservadas com tipos e estados", () =>
{
    True(ifc.Contains("IFCLABEL('AS_Origin')") || ifc.Contains("'AS_Origin'"), "pset origem");
    True(ifc.Contains("'AS_AtomicElement'") && ifc.Contains("'AS_PlateBase'"), "psets por classe");
    True(ifc.Contains("IFCBOOLEAN(.T.)"), "booleano"); True(ifc.Contains("IFCINTEGER(4)"), "inteiro");
    True(ifc.Contains("IFCTEXT('')"), "vazio como texto vazio");
    True(Regex.IsMatch(ifc, @"IFCPROPERTYSINGLEVALUE\('Camber','Null',\$,\$\)"), "nulo sem valor");
    True(ifc.Contains(@"\X2\00E700E3\X0\") && ifc.Contains("''x''"), "acentos e aspas escapados");
    True(ifc.Contains("'GetWeight [SI]'"), "valor normalizado em kg");
});
Run("geometria: chapa extrudada e cilindros dos parafusos", () =>
{
    True(ifc.Contains("=IFCARBITRARYCLOSEDPROFILEDEF("), "perfil da chapa");
    True(Regex.Matches(ifc, "=IFCEXTRUDEDAREASOLID\\(").Count == 3, "1 chapa + 2 parafusos");
    True(ifc.Contains("IFCCIRCLEPROFILEDEF(.AREA.,$,") && ifc.Contains(",0.006);"), "raio 6 mm em m");
});
Run("ligação agrupa os membros exportados", () => True(ifc.Contains("=IFCRELASSIGNSTOGROUP("), "grupo"));
Run("GlobalId: 22 caracteres; ida e volta do GUID", () =>
{
    var g = Guid.Parse("8acce620-b3c0-4c40-9dd0-2cb000b8bcbf");
    string c = IfcGuid.Compress(g);
    True(c.Length == 22 && IfcGuid.Expand(c) == g, "ida e volta");
    True(ifc.Contains("'" + c + "'"), "GlobalId da chapa vem do GUID do Advance Steel");
    True(IfcGuid.FromName("a") == IfcGuid.FromName("a") && IfcGuid.FromName("a") != IfcGuid.FromName("b"), "determinístico");
});
Run("reais no formato STEP", () =>
{
    True(StepWriter.Real(1) == "1." && StepWriter.Real(1.5) == "1.5" && StepWriter.Real(1e-5) == "1.E-05", "formatos");
});
Run("sem georreferenciamento: aviso e sem CRS", () =>
{
    string s = IfcExporter.Export(x, new ExportOptions(), out ExportReport r2);
    True(!s.Contains("IFCMAPCONVERSION") && r2.Warnings.Any(w => w.Contains("georreferenciamento")), "aviso");
});

// ------------------------------------------------------------------ corpo sólido real (0.14.0)

// Cubo de lado 2h centrado em c, com vértices a partir de "offset"; se hole, a face de cima tem um furo quadrado.
static (List<double> v, List<string> f) Cube(double cx, double cy, double cz, double h, int offset, bool hole = false)
{
    var v = new List<double>();
    foreach (var (dx, dy, dz) in new[] { (-1, -1, -1), (1, -1, -1), (1, 1, -1), (-1, 1, -1), (-1, -1, 1), (1, -1, 1), (1, 1, 1), (-1, 1, 1) })
        v.AddRange(new[] { cx + dx * h, cy + dy * h, cz + dz * h });
    int o = offset;
    string L(params int[] i) => "[" + string.Join(",", i.Select(k => k + o)) + "]";
    var f = new List<string> { "[" + L(0, 3, 2, 1) + "]", "[" + L(0, 1, 5, 4) + "]", "[" + L(1, 2, 6, 5) + "]", "[" + L(2, 3, 7, 6) + "]", "[" + L(3, 0, 4, 7) + "]" };
    if (hole)
    {
        foreach (var (dx, dy) in new[] { (-0.5, -0.5), (0.5, -0.5), (0.5, 0.5), (-0.5, 0.5) }) v.AddRange(new[] { cx + dx * h, cy + dy * h, cz + h });
        f.Add("[" + L(4, 5, 6, 7) + "," + L(11, 10, 9, 8) + "]");
    }
    else f.Add("[" + L(4, 5, 6, 7) + "]");
    return (v, f);
}
static string BodyProp(params (List<double> v, List<string> f)[] parts) =>
    """{"technicalName":"GetModeler","declaringType":"AtomicElement","argument":"kDetailed","source":"method","dataType":"J","status":"Read","rawValue":{"v":[""" +
    string.Join(",", parts.SelectMany(p => p.v).Select(d => d.ToString(System.Globalization.CultureInfo.InvariantCulture))) + "],\"f\":[" + string.Join(",", parts.SelectMany(p => p.f)) + "]}}";
void Integrity(string s)
{
    var defined = new HashSet<int>(Regex.Matches(s, @"^#(\d+)=", RegexOptions.Multiline).Select(m => int.Parse(m.Groups[1].Value)));
    True(defined.Count > 0 && defined.Max() == defined.Count, "sequência");
    foreach (Match m in Regex.Matches(s, @"(?<!^)#(\d+)", RegexOptions.Multiline))
        True(defined.Contains(int.Parse(m.Groups[1].Value)), "referência inexistente #" + m.Groups[1].Value);
    foreach (string line in s.Split('\n').Where(l => l.StartsWith('#')))
    {
        string noStr = Regex.Replace(line, "'(?:[^']|'')*'", "''");
        True(noStr.Count(c => c == '(') == noStr.Count(c => c == ')'), "desbalanceado: " + line);
    }
}

string PlateWithBody = Plate.Replace("\"properties\":[", "\"properties\":[" + BodyProp(Cube(98, 50, 2.375, 2.375, 0, hole: true)) + ",");
string BoltWithBody = Bolt.Replace("\"properties\":[", "\"properties\":[" + BodyProp(Cube(30, 30, 2, 5, 0), Cube(160, 30, 2, 5, 8)) + ",");
Extraction xb = DiagnosticImporter.Import(Run1, new[] { PlateWithBody, BoltWithBody, Joint });
string ifcB = IfcExporter.Export(xb, new ExportOptions(), out ExportReport repB);

Run("corpo real: chapa como IfcPolygonalFaceSet, com furo; sem extrusão paramétrica", () =>
{
    Integrity(ifcB);
    True(ifcB.Contains("=IFCPOLYGONALFACESET(") && ifcB.Contains("=IFCCARTESIANPOINTLIST3D("), "face set");
    True(ifcB.Contains("=IFCINDEXEDPOLYGONALFACEWITHVOIDS(("), "face com furo");
    True(ifcB.Contains("'Tessellation'"), "tipo de representação");
    True(!ifcB.Contains("=IFCARBITRARYCLOSEDPROFILEDEF("), "sem contorno extrudado");
    True(Regex.IsMatch(ifcB, @"\(0\.0956[23],0\.0476[23],0\.\)"), "vértice em metros, 5 casas");
    True(!repB.Limitations.Any(l => l.Contains("Contorno externo apenas")), "limitação paramétrica não se aplica");
});
Run("corpo real: parafusos iguais viram 1 IfcRepresentationMap e 2 IfcMappedItem", () =>
{
    True(Regex.Matches(ifcB, "=IFCREPRESENTATIONMAP\\(").Count == 1, "uma geometria de parafuso");
    True(Regex.Matches(ifcB, "=IFCMAPPEDITEM\\(").Count == 2, "dois parafusos");
    True(ifcB.Contains("'MappedRepresentation'"), "representação mapeada");
    True(ifcB.Contains("=IFCCARTESIANPOINT((0.16,0.03,0.002));"), "posição do 2º parafuso (ponto médio, m)");
    True(!ifcB.Contains("=IFCCIRCLEPROFILEDEF("), "sem cilindro simplificado");
});
Run("corpo real: separação desigual volta ao corpo inteiro", () =>
{
    // Três pontos médios para dois cubos: um "parafuso" fica sem faces; usa o corpo inteiro, sem mapeamento.
    string three = BoltWithBody.Replace("[[30,30,2],[160,30,2]]", "[[30,30,2],[160,30,2],[95,500,2]]");
    string s = IfcExporter.Export(DiagnosticImporter.Import(Run1, new[] { three }), new ExportOptions(), out _);
    Integrity(s);
    True(!s.Contains("=IFCMAPPEDITEM(") && s.Contains("=IFCPOLYGONALFACESET("), "corpo inteiro");
});

const string RunSummary = """{"tool":"PASB Diagnostics","insunits":"Millimeters","transactionMode":"write-discarded","readProfile":"export-summary","startedUtc":"2026-11-20T10:00:00Z"}""";
string PlateSummary = Plate.Replace("\"properties\":[", "\"properties\":[" +
    """{"technicalName":"GetSinglePartPositionNumber","declaringType":"AtomicElement","source":"method","dataType":"System.String","status":"Read","rawValue":"p12"},""" +
    """{"technicalName":"GetPhysLengthAndWidth","declaringType":"PlateBase","source":"method","dataType":"J","status":"Read","rawValue":{"dLength":196,"dWidth":100}},""" +
    """{"technicalName":"GetPaintArea","declaringType":"PlateBase","source":"method","dataType":"System.Double","status":"Read","rawValue":41000},""");
Extraction xs = DiagnosticImporter.Import(RunSummary, new[] { PlateSummary, Bolt, Joint });
string ifcS = IfcExporter.Export(xs, new ExportOptions(), out ExportReport repS);

Run("resumida: só o Pset PASB_Resumo, com nomes e unidades SI", () =>
{
    Integrity(ifcS);
    True(xs.Info.SummaryProperties && xs.Info.Warnings.Any(w => w.Contains("resumida")), "modo e aviso");
    True(ifcS.Contains("'PASB_Resumo'") && !ifcS.Contains("'AS_AtomicElement'") && !ifcS.Contains("'AS_Origin'"), "só o resumo");
    True(ifcS.Contains(@"'Tipo de a\X2\00E7\X0\o',$,IFCLABEL("), "tipo de aço");
    True(ifcS.Contains("'Peso total',$,IFCMASSMEASURE(0.730835)"), "peso em kg");
    True(ifcS.Contains("'Espessura',$,IFCLENGTHMEASURE(0.00475)"), "espessura em m");
    True(ifcS.Contains("'Comprimento',$,IFCLENGTHMEASURE(0.196)") && ifcS.Contains("'Largura',$,IFCLENGTHMEASURE(0.1)"), "dimensões da chapa");
    True(ifcS.Contains(@"'\X2\00C1\X0\rea de pintura',$,IFCAREAMEASURE(0.041)"), "área de pintura em m²");
    True(ifcS.Contains(@"'Marca da pe\X2\00E7\X0\a',$,IFCLABEL('p12')"), "marca");
    True(ifcS.Contains(@"'Di\X2\00E2\X0\metro',$,IFCLENGTHMEASURE(0.012)"), "diâmetro do parafuso");
    True(repS.PropertySets == 3, "um Pset por elemento");
});

Console.WriteLine($"{passed} passaram, {failed} falharam");
return failed == 0 ? 0 : 1;
