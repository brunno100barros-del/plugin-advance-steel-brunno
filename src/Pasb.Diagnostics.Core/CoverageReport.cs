using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Pasb.Diagnostics.Core;

/// <summary>
/// Contagem de estados por tipo de objeto × membro (docs/DIAGNOSTIC-DESIGN.md, seção 7.3).
/// </summary>
public sealed class CoverageReport
{
    private readonly Dictionary<(string objectType, string declaringType, string member, string argument, string source), int[]> _counts = new();
    private readonly Dictionary<ReadStatus, int> _totals = new();
    private static readonly ReadStatus[] Statuses = (ReadStatus[])Enum.GetValues(typeof(ReadStatus));

    public void Add(string objectType, PropertyRecord r)
    {
        var key = (objectType, r.DeclaringType, r.TechnicalName, r.Argument ?? "", r.Source);
        if (!_counts.TryGetValue(key, out int[]? c))
        {
            c = new int[Statuses.Length];
            _counts[key] = c;
        }
        c[(int)r.Status]++;
        _totals[r.Status] = _totals.GetValueOrDefault(r.Status) + 1;
    }

    public IReadOnlyDictionary<ReadStatus, int> Totals => _totals;

    public int Rows => _counts.Count;

    /// <summary>CSV com separador ";" e BOM UTF-8 (abre direto no Excel).</summary>
    public void WriteCsv(string path)
    {
        var sb = new StringBuilder();
        sb.Append("object_type;declaring_type;member;argument;source");
        foreach (ReadStatus s in Statuses) sb.Append(';').Append(s);
        sb.AppendLine();

        foreach (var kv in _counts.OrderBy(k => k.Key.objectType, StringComparer.Ordinal)
                                  .ThenBy(k => k.Key.declaringType, StringComparer.Ordinal)
                                  .ThenBy(k => k.Key.member, StringComparer.Ordinal)
                                  .ThenBy(k => k.Key.argument, StringComparer.Ordinal))
        {
            var (o, d, m, a, src) = kv.Key;
            sb.Append(Csv(o)).Append(';').Append(Csv(d)).Append(';').Append(Csv(m)).Append(';').Append(Csv(a)).Append(';').Append(Csv(src));
            foreach (int n in kv.Value) sb.Append(';').Append(n);
            sb.AppendLine();
        }
        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));
    }

    private static string Csv(string s) => "\"" + s.Replace("\"", "\"\"") + "\"";
}
