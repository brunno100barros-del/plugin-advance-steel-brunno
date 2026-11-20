using System;
using System.IO;
using System.Linq;
using Pasb.Model;

// Uso: dotnet run --project tools\ToNeutral -- <pasta-do-diagnostico> [arquivo-saida.json]
// Lê run.json + objects.jsonl e grava o modelo neutro (padrão: neutral-model.json na mesma pasta).
if (args.Length < 1 || !Directory.Exists(args[0]))
{
    Console.Error.WriteLine("Uso: ToNeutral <pasta-do-diagnostico> [saida.json]");
    return 1;
}

string outPath = args.Length > 1 ? args[1] : Path.Combine(args[0], "neutral-model.json");
Extraction x = DiagnosticImporter.ImportDirectory(args[0]);
NeutralModelJson.Write(x, outPath);

Console.WriteLine($"Elementos: {x.Elements.Count}");
foreach (var g in x.Elements.GroupBy(e => e.Category).OrderBy(g => g.Key))
    Console.WriteLine($"  {g.Key,-18} {g.Count(),3}  estados: {string.Join(", ", g.GroupBy(e => e.Status).Select(s => $"{s.Key}={s.Count()}"))}");
Console.WriteLine($"Propriedades: {x.Elements.Sum(e => e.Properties.Count)} | com unidade: {x.Elements.Sum(e => e.Properties.Count(p => p.Unit != null))} | relações: {x.Elements.Sum(e => e.Relationships.Count)}");
foreach (string w in x.Info.Warnings) Console.WriteLine("Aviso: " + w);
Console.WriteLine("Saída: " + outPath);
return 0;
