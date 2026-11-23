using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json.Nodes;
using Autodesk.AdvanceSteel.CADAccess;
using Pasb.Diagnostics.Core;

namespace Pasb.Diagnostics.AdvanceSteel;

/// <summary>
/// Lê um objeto do Advance Steel sem alterá-lo (docs/DIAGNOSTIC-DESIGN.md, seção 5).
/// </summary>
internal static class ObjectReader
{
    /// <summary>
    /// Getters que gravam internamente e derrubam o AutoCAD numa transação SOMENTE LEITURA.
    /// Só são excluídos quando Commands.WriteWithDiscard = false; no modo com descarte são lidos normalmente.
    /// Cada inclusão exige evidência registrada em docs/DIAGNOSTIC-TEST-LOG.md.
    /// </summary>
    private static readonly HashSet<string> ReadOnlyCrashGetters = new(StringComparer.Ordinal)
    {
        // Teste 2: ASId de um StraightBeam → eNotOpenForWrite (0xE0000001). Indício de ID criado sob demanda.
        "FilerObject.ASId",
        // Teste 5: AssemblyUsedForNumbering de um CompoundStraightBeam → eNotOpenForWrite (funcionou num StraightBeam).
        "AtomicElement.AssemblyUsedForNumbering",
    };

    private static readonly HashSet<string> NoExclusions = new(StringComparer.Ordinal);

    private static HashSet<string> ExcludedGetters => Commands.WriteWithDiscard ? NoExclusions : ReadOnlyCrashGetters;

    /// <summary>Motivo registrado no JSON para cada getter excluído.</summary>
    private const string ExclusionReason =
        "Excluído no modo somente leitura: a leitura derrubou o AutoCAD (eNotOpenForWrite); ver docs/DIAGNOSTIC-TEST-LOG.md.";

    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> GetterCache = new();

    /// <param name="trace">Recebe uma linha antes de cada leitura (ver TraceLog); nulo = sem trace por leitura.</param>
    public static (JsonObject header, List<PropertyRecord> records, string objectType) Read(FilerObject o, Action<string>? trace,
        ReadProfile profile = ReadProfile.Diagnostic)
    {
        Type t = o.GetType();
        // Sem trace por leitura, o RecordBuilder nem monta o texto de cada passo.
        var b = new RecordBuilder(AsValueSerializer.TrySerialize, trace);
        Action<string> step = trace ?? (static _ => { });

        // 5.1 Identidade
        var header = new JsonObject
        {
            ["clrType"] = t.FullName,
            ["inheritance"] = new JsonArray(Inheritance(t).Select(n => (JsonNode)JsonValue.Create(n)!).ToArray()),
        };
        step("identidade: Handle");
        TryHeader(header, "handle", () => JsonValue.Create(o.Handle));
        header["transactionMode"] = Commands.TransactionMode;
        if (ExcludedGetters.Contains("FilerObject.ASId"))
            header["asIdExcluded"] = ExclusionReason;
        else
        {
            step("identidade: ASId");
            TryHeader(header, "asId", () => JsonValue.Create(o.ASId));
        }
        step("identidade: GetUniqueId");
        TryHeader(header, "uniqueId", () => JsonValue.Create(o.GetUniqueId().ToString("D")));
        step("identidade: Type");
        TryHeader(header, "objectType", () => ValueSerializer.EnumValue(o.Type()));

        // 5.2 Getters automáticos (públicos, de instância, legíveis, sem índice, incluindo herdados)
        foreach (PropertyInfo p in Getters(t))
        {
            string declaring = p.DeclaringType?.Name ?? "?";
            if (p.PropertyType.IsPointer || p.PropertyType.IsByRef)
            {
                b.Unavailable(p.Name, declaring, ReadSource.Getter, p.PropertyType, "Tipo ponteiro nativo; não serializável.");
                continue;
            }
            if (ExcludedGetters.Contains(declaring + "." + p.Name))
            {
                b.Unavailable(p.Name, declaring, ReadSource.Getter, p.PropertyType, ExclusionReason);
                continue;
            }
            PropertyInfo prop = p;
            b.Read(p.Name, declaring, ReadSource.Getter, null, p.PropertyType, () => prop.GetValue(o));
        }

        // 5.3 Métodos aprovados, chamados explicitamente
        ApprovedMethods.Read(o, b, profile);

        return (header, b.Records, t.Name);
    }

    private static void TryHeader(JsonObject header, string key, Func<JsonNode?> f)
    {
        try { header[key] = f(); }
        catch (Exception ex) { header[key + "Error"] = ex.GetType().Name + ": " + ex.Message; }
    }

    private static IEnumerable<string> Inheritance(Type t)
    {
        for (Type? cur = t; cur is not null && cur != typeof(object); cur = cur.BaseType)
            yield return cur.Name;
    }

    private static PropertyInfo[] Getters(Type t) => GetterCache.GetOrAdd(t, type =>
        type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.GetMethod is { IsPublic: true } && p.GetIndexParameters().Length == 0)
            // Uma linha por nome: se uma subclasse redeclara (new) a propriedade, vale a mais derivada.
            .GroupBy(p => p.Name)
            .Select(g => g.OrderByDescending(p => Depth(p.DeclaringType)).First())
            .OrderBy(p => Depth(p.DeclaringType))
            .ThenBy(p => p.Name, StringComparer.Ordinal)
            .ToArray());

    private static int Depth(Type? t)
    {
        int d = 0;
        for (Type? cur = t; cur is not null; cur = cur.BaseType) d++;
        return d;
    }
}
