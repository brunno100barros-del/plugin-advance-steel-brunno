using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using Pasb.Diagnostics.Core;

namespace Pasb.Diagnostics.Core.Tests;

internal static class Program
{
    private static int _failed;
    private static int _passed;

    private static int Main()
    {
        Run(nameof(StringEmptyIsEmptyNotNull), StringEmptyIsEmptyNotNull);
        Run(nameof(StringKeepsWhitespace), StringKeepsWhitespace);
        Run(nameof(NullIsNull), NullIsNull);
        Run(nameof(ZeroAndFalseAreRead), ZeroAndFalseAreRead);
        Run(nameof(DoubleKeepsFullPrecision), DoubleKeepsFullPrecision);
        Run(nameof(NaNIsTextNotZero), NaNIsTextNotZero);
        Run(nameof(EnumHasNameAndValue), EnumHasNameAndValue);
        Run(nameof(ExceptionBecomesReadError), ExceptionBecomesReadError);
        Run(nameof(UnknownTypeIsComplexNotSerialized), UnknownTypeIsComplexNotSerialized);
        Run(nameof(ExtraSerializerIsUsed), ExtraSerializerIsUsed);
        Run(nameof(NullRecordHasExplicitNullRawValue), NullRecordHasExplicitNullRawValue);
        Run(nameof(OutputWritesAllFiles), OutputWritesAllFiles);
        Run(nameof(TraceIsWrittenBeforeEachRead), TraceIsWrittenBeforeEachRead);
        Run(nameof(TraceSurvivesWithoutDispose), TraceSurvivesWithoutDispose);
        Run(nameof(FastTraceSurvivesWithoutDispose), FastTraceSurvivesWithoutDispose);
        Run(nameof(BufferedOutputIsCompleteAfterFinish), BufferedOutputIsCompleteAfterFinish);

        Console.WriteLine($"{_passed} passaram, {_failed} falharam");
        return _failed == 0 ? 0 : 1;
    }

    private static void Run(string name, Action test)
    {
        try { test(); _passed++; Console.WriteLine("ok    " + name); }
        catch (Exception ex) { _failed++; Console.WriteLine("FALHA " + name + ": " + ex.Message); }
    }

    private static void Equal<T>(T expected, T actual, string what)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new Exception($"{what}: esperado <{expected}>, obtido <{actual}>");
    }

    private static PropertyRecord One(object? value, Type? declared = null)
    {
        var b = new RecordBuilder();
        b.Read("X", "T", ReadSource.Getter, null, declared ?? value?.GetType() ?? typeof(object), () => value);
        return b.Records.Single();
    }

    private static void StringEmptyIsEmptyNotNull()
    {
        PropertyRecord r = One("", typeof(string));
        Equal(ReadStatus.Empty, r.Status, "status");
        Equal("\"\"", r.ToJson()["rawValue"]!.ToJsonString(), "rawValue");
    }

    private static void StringKeepsWhitespace()
    {
        PropertyRecord r = One("  S355 ", typeof(string));
        Equal(ReadStatus.Read, r.Status, "status");
        Equal("  S355 ", r.RawValue!.GetValue<string>(), "valor");
    }

    private static void NullIsNull()
    {
        PropertyRecord r = One(null, typeof(string));
        Equal(ReadStatus.Null, r.Status, "status");
        Equal("System.String", r.DataType, "dataType");
    }

    private static void ZeroAndFalseAreRead()
    {
        Equal(ReadStatus.Read, One(0.0).Status, "zero");
        Equal(ReadStatus.Read, One(false).Status, "false");
        Equal("0", One(0).RawValue!.ToJsonString(), "int zero");
        Equal("false", One(false).RawValue!.ToJsonString(), "false json");
    }

    private static void DoubleKeepsFullPrecision()
    {
        double v = 1234.5678901234567;
        PropertyRecord r = One(v);
        double back = double.Parse(r.RawValue!.ToJsonString(), System.Globalization.CultureInfo.InvariantCulture);
        Equal(v, back, "ida e volta");
        Equal(0.1 + 0.2, double.Parse(One(0.1 + 0.2).RawValue!.ToJsonString(), System.Globalization.CultureInfo.InvariantCulture), "0.1+0.2");
    }

    private static void NaNIsTextNotZero()
    {
        PropertyRecord r = One(double.NaN);
        Equal(ReadStatus.Read, r.Status, "status");
        Equal("\"NaN\"", r.RawValue!.ToJsonString(), "NaN");
        Equal("\"Infinity\"", One(double.PositiveInfinity).RawValue!.ToJsonString(), "Infinity");
    }

    private static void EnumHasNameAndValue()
    {
        PropertyRecord r = One(DayOfWeek.Friday);
        Equal("Friday", r.RawValue!["name"]!.GetValue<string>(), "nome");
        Equal(5L, r.RawValue!["value"]!.GetValue<long>(), "valor");
    }

    private static void ExceptionBecomesReadError()
    {
        var b = new RecordBuilder();
        b.Read("Falha", "T", ReadSource.Getter, null, typeof(double), () => throw new InvalidOperationException("sem acesso"));
        b.Read("Depois", "T", ReadSource.Getter, null, typeof(int), () => 7);
        Equal(2, b.Records.Count, "a falha não interrompe a próxima leitura");
        Equal(ReadStatus.ReadError, b.Records[0].Status, "status");
        Equal("System.InvalidOperationException", b.Records[0].Error!.Type, "tipo do erro");
        Equal("sem acesso", b.Records[0].Error!.Message, "mensagem");
        Equal(false, b.Records[0].ToJson().ContainsKey("rawValue"), "erro não tem rawValue");
    }

    private sealed class Opaque { public override string ToString() => "opaco"; }

    private static void UnknownTypeIsComplexNotSerialized()
    {
        PropertyRecord r = One(new Opaque());
        Equal(ReadStatus.ComplexNotSerialized, r.Status, "status");
        Equal("opaco", r.RawValue!["toString"]!.GetValue<string>(), "toString");
    }

    private static void ExtraSerializerIsUsed()
    {
        var b = new RecordBuilder((object v, out JsonNode? n, out ReadStatus s) =>
        {
            n = null; s = ReadStatus.Read;
            if (v is not Opaque) return false;
            n = JsonValue.Create("convertido");
            return true;
        });
        b.Read("X", "T", ReadSource.Getter, null, typeof(Opaque), () => new Opaque());
        Equal(ReadStatus.Read, b.Records[0].Status, "status");
        Equal("convertido", b.Records[0].RawValue!.GetValue<string>(), "valor");
    }

    private static void NullRecordHasExplicitNullRawValue()
    {
        JsonObject j = One(null, typeof(string)).ToJson();
        Equal(true, j.ContainsKey("rawValue"), "chave presente");
        Equal(null, j["rawValue"], "valor null");
    }

    private static void TraceIsWrittenBeforeEachRead()
    {
        var steps = new List<string>();
        var b = new RecordBuilder(null, s => steps.Add(s));
        string? seenDuringRead = null;
        b.Read("Peso", "Beam", ReadSource.Method, "1", typeof(double), () => { seenDuringRead = steps.LastOrDefault(); return 1.0; });
        Equal("Beam.Peso(1) [method]", seenDuringRead, "trace gravado antes da leitura");
    }

    /// <summary>Simula a queda: o arquivo é lido sem fechar o TraceLog; as linhas já precisam estar no disco.</summary>
    private static void TraceSurvivesWithoutDispose() => TraceSurvives(durable: true);

    /// <summary>Modo de trabalho: sem gravação física, mas cada linha já foi entregue ao sistema (sobrevive à queda do AutoCAD).</summary>
    private static void FastTraceSurvivesWithoutDispose() => TraceSurvives(durable: false);

    private static void TraceSurvives(bool durable)
    {
        string path = Path.Combine(Path.GetTempPath(), "pasb-trace-" + Guid.NewGuid().ToString("N") + ".log");
        var t = new TraceLog(path, durable);
        try
        {
            t.Write("passo 1");
            t.Write("passo 2");
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var sr = new StreamReader(fs);
            string[] lines = sr.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries);
            Equal(2, lines.Length, "linhas no disco antes do Dispose");
            Equal(true, lines[1].TrimEnd().EndsWith("passo 2"), "última linha");
        }
        finally
        {
            t.Dispose();
            File.Delete(path);
        }
    }

    /// <summary>Modo de trabalho: objects.jsonl com buffer fica completo depois de Finish/Dispose.</summary>
    private static void BufferedOutputIsCompleteAfterFinish()
    {
        string dir = Path.Combine(Path.GetTempPath(), "pasb-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            using (var o = new DiagnosticOutput(dir, flushEachObject: false))
            {
                for (int i = 0; i < 500; i++)
                {
                    var b = new RecordBuilder();
                    b.Read("A", "T", ReadSource.Getter, null, typeof(int), () => i);
                    o.WriteObject(new JsonObject { ["handle"] = i.ToString("X") }, "Tipo", b.Records);
                }
                o.Finish(new JsonObject { ["command"] = "teste" });
            }
            string[] lines = File.ReadAllLines(Path.Combine(dir, "objects.jsonl"));
            Equal(500, lines.Length, "linhas jsonl");
            Equal("1F3", JsonNode.Parse(lines[^1])!["handle"]!.GetValue<string>(), "último objeto");
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }

    private static void OutputWritesAllFiles()
    {
        string dir = Path.Combine(Path.GetTempPath(), "pasb-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            using (var o = new DiagnosticOutput(dir))
            {
                var b = new RecordBuilder();
                b.Read("A", "T", ReadSource.Getter, null, typeof(string), () => "");
                b.Read("B", "T", ReadSource.Getter, null, typeof(int), () => 1);
                o.WriteObject(new JsonObject { ["handle"] = "1A" }, "Tipo", b.Records);
                o.Finish(new JsonObject { ["command"] = "teste" });
            }
            Equal(1, File.ReadAllLines(Path.Combine(dir, "objects.jsonl")).Length, "linhas jsonl");
            JsonNode run = JsonNode.Parse(File.ReadAllText(Path.Combine(dir, "run.json")))!;
            Equal(1, run["readTotals"]!["Empty"]!.GetValue<int>(), "total Empty");
            Equal(1, run["readTotals"]!["Read"]!.GetValue<int>(), "total Read");
            Equal(3, File.ReadAllLines(Path.Combine(dir, "coverage.csv")).Length, "linhas csv (cabeçalho + 2)");
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }
}
