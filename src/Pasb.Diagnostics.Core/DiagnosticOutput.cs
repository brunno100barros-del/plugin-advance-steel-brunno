using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Pasb.Diagnostics.Core;

/// <summary>
/// Grava a saída de uma execução: objects.jsonl (streaming), run.json e coverage.csv.
/// </summary>
public sealed class DiagnosticOutput : IDisposable
{
    private static readonly JsonSerializerOptions Compact = new() { WriteIndented = false };
    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    private readonly StreamWriter _objects;

    /// <param name="flushEachObject">
    /// true (diagnóstico): cada objeto vai para o disco assim que é lido, para sobreviver a uma queda do AutoCAD.
    /// false (exportação): gravação com buffer; o arquivo só fica completo em Finish/Dispose.
    /// </param>
    public DiagnosticOutput(string directory, bool flushEachObject = true)
    {
        Directory.CreateDirectory(directory);
        DirectoryPath = directory;
        _objects = new StreamWriter(Path.Combine(directory, "objects.jsonl"), false, new UTF8Encoding(false), 1 << 16) { AutoFlush = flushEachObject };
        Coverage = new CoverageReport();
    }

    public string DirectoryPath { get; }

    public string TracePath => Path.Combine(DirectoryPath, "trace.log");

    public CoverageReport Coverage { get; }

    public int ObjectsWritten { get; private set; }

    /// <summary>Pasta padrão: Documentos\PASB-Diagnostics\aaaaMMdd-HHmmss.</summary>
    public static string DefaultDirectory(DateTime now) =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "PASB-Diagnostics", now.ToString("yyyyMMdd-HHmmss"));

    public void WriteObject(JsonObject obj, string objectType, System.Collections.Generic.IEnumerable<PropertyRecord> records)
    {
        var props = new JsonArray();
        foreach (PropertyRecord r in records)
        {
            props.Add(r.ToJson());
            Coverage.Add(objectType, r);
        }
        obj["properties"] = props;
        _objects.WriteLine(obj.ToJsonString(Compact));
        ObjectsWritten++;
    }

    /// <summary>Linha para um objeto que nem chegou a ser aberto.</summary>
    public void WriteOpenFailure(string? handle, Exception ex, JsonObject? identification = null)
    {
        var j = new JsonObject
        {
            ["handle"] = handle,
            ["openError"] = new JsonObject { ["type"] = ex.GetType().FullName, ["message"] = ex.Message },
        };
        // Identificação obtida sem abrir o objeto (classe AutoCAD, tipo Advance Steel por filtro).
        if (identification is not null) j["identification"] = identification;
        _objects.WriteLine(j.ToJsonString(Compact));
    }

    public void Finish(JsonObject run)
    {
        _objects.Flush();
        var totals = new JsonObject();
        foreach (var kv in Coverage.Totals) totals[kv.Key.ToString()] = kv.Value;
        run["readTotals"] = totals;
        run["objectsWritten"] = ObjectsWritten;
        File.WriteAllText(Path.Combine(DirectoryPath, "run.json"), run.ToJsonString(Indented), new UTF8Encoding(false));
        Coverage.WriteCsv(Path.Combine(DirectoryPath, "coverage.csv"));
    }

    public void Dispose() => _objects.Dispose();
}
