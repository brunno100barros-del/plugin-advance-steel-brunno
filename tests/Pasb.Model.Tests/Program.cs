using System;
using System.Collections.Generic;
using System.Linq;
using Pasb.Model;

int failed = 0, passed = 0;
void Run(string name, Action t)
{
    try { t(); passed++; Console.WriteLine("ok    " + name); }
    catch (Exception ex) { failed++; Console.WriteLine("FALHA " + name + ": " + ex.Message); }
}
void Eq<T>(T exp, T act, string what)
{
    if (!EqualityComparer<T>.Default.Equals(exp, act)) throw new Exception($"{what}: esperado <{exp}>, obtido <{act}>");
}

const string RunMm = """{"tool":"PASB Diagnostics","insunits":"Millimeters","transactionMode":"write-discarded"}""";
const string Beam = """{"handle":"52A","uniqueId":"g1","clrType":"Autodesk.AdvanceSteel.Modelling.StraightBeam","inheritance":["StraightBeam","Beam","MainAlias","AtomicElement","ActiveConstructionElement","ConstructionElement","FilerObject"],"properties":[{"technicalName":"Material","declaringType":"AtomicElement","source":"getter","dataType":"System.String","status":"Read","rawValue":"S235JRG2"},{"technicalName":"Note","declaringType":"AtomicElement","source":"getter","dataType":"System.String","status":"Empty","rawValue":""},{"technicalName":"Camber","declaringType":"StraightBeam","source":"getter","dataType":"X","status":"Null","rawValue":null},{"technicalName":"GetWeight","declaringType":"Beam","argument":"1","source":"method","dataType":"System.Double","status":"Read","rawValue":15168.57},{"technicalName":"SysLength","declaringType":"Beam","source":"getter","dataType":"System.Double","status":"Read","rawValue":1500},{"technicalName":"GetFeatures","declaringType":"AtomicElement","argument":"true","source":"method","dataType":"L","status":"Read","rawValue":[{"handle":"4EF"},{"handle":"4F3"}]},{"technicalName":"Desconhecida","declaringType":"Futura","source":"getter","dataType":"Y","status":"Read","rawValue":{"a":1}}]}""";
const string Fail = """{"handle":"83","openError":{"type":"X","message":"DatabaseManager.Open retornou null."},"identification":{"autocadClass":"AstCounter"}}""";

Extraction x = DiagnosticImporter.Import(RunMm, new[] { Beam, Fail });
NeutralElement b = x.Elements[0];
NeutralProperty P(string key) => b.Properties.Single(p => p.Key == key);

Run("categoria pela herança", () => Eq("beam", b.Category, "categoria"));
Run("nenhuma propriedade descartada", () => Eq(7, b.Properties.Count, "propriedades"));
Run("vazio, nulo e lido distintos", () =>
{
    Eq(PropertyStatus.Empty, P("AtomicElement.Note").Status, "Note");
    Eq(PropertyStatus.Null, P("StraightBeam.Camber").Status, "Camber");
    Eq("S235JRG2", P("AtomicElement.Material").RawValue!.GetValue<string>(), "Material");
});
Run("peso em gramas normalizado para kg", () =>
{
    NeutralProperty w = P("Beam.GetWeight(1)");
    Eq("g", w.Unit, "unidade"); Eq(15168.57, w.RawValue!.GetValue<double>(), "bruto preservado");
    Eq(true, Math.Abs(w.NormalizedValue!.Value - 15.16857) < 1e-9, "kg");
});
Run("comprimento em mm normalizado para m", () => Eq(1.5, P("Beam.SysLength").NormalizedValue, "m"));
Run("propriedade desconhecida preservada sem unidade", () =>
{
    NeutralProperty d = P("Futura.Desconhecida");
    Eq(null, d.Unit, "unidade"); Eq(1, d.RawValue!["a"]!.GetValue<int>(), "valor");
});
Run("relações a partir de handles", () =>
{
    Eq(2, b.Relationships.Count, "relações");
    Eq("4EF", b.Relationships[0].TargetHandle, "alvo");
    Eq("AtomicElement.GetFeatures(true)", b.Relationships[0].Kind, "tipo");
});
Run("GUID de origem separado do IfcGlobalId", () => { Eq("g1", b.SourceUniqueId, "uid"); Eq(null, b.IfcGlobalId, "ifc"); });
Run("objeto não aberto vira Unsupported, com identificação", () =>
{
    NeutralElement f = x.Elements[1];
    Eq(ElementStatus.Unsupported, f.Status, "status"); Eq("AstCounter", f.SourceType, "classe");
});
Run("aviso do modo com descarte", () => Eq(true, x.Info.Warnings.Any(w => w.Contains("descartada")), "aviso"));
Run("sem INSUNITS em mm: comprimento sem normalização", () =>
{
    Extraction y = DiagnosticImporter.Import("""{"insunits":"Inches"}""", new[] { Beam });
    Eq(null, y.Elements[0].Properties.Single(p => p.Key == "Beam.SysLength").NormalizedValue, "sem normalizar");
    Eq(true, y.Elements[0].Properties.Single(p => p.Key == "Beam.GetWeight(1)").NormalizedValue.HasValue, "peso independe");
});
Run("JSON serializa enums como texto", () => Eq(true, NeutralModelJson.Serialize(x).Contains("\"Status\": \"Unsupported\""), "enum"));

Console.WriteLine($"{passed} passaram, {failed} falharam");
return failed == 0 ? 0 : 1;
