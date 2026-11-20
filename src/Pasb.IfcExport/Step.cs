using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;

namespace Pasb.IfcExport;

/// <summary>Montagem de entidades STEP (ISO 10303-21) com numeração #n.</summary>
public sealed class StepWriter
{
    private readonly List<string> _lines = new();

    public int Add(string entity, params string[] attributes)
    {
        _lines.Add($"={entity.ToUpperInvariant()}({string.Join(",", attributes)});");
        return _lines.Count;
    }

    public int Count => _lines.Count;

    public string Build(string fileName, string timestampUtc, string description = "ViewDefinition [ReferenceView_V1.2]")
    {
        var sb = new StringBuilder();
        sb.Append("ISO-10303-21;\nHEADER;\n");
        sb.Append($"FILE_DESCRIPTION(({Str(description)}),'2;1');\n");
        sb.Append($"FILE_NAME({Str(fileName)},{Str(timestampUtc)},(''),(''),'PASB IfcExport','Plugin Advance Steel - Brunno','');\n");
        sb.Append("FILE_SCHEMA(('IFC4'));\nENDSEC;\nDATA;\n");
        for (int i = 0; i < _lines.Count; i++) sb.Append('#').Append(i + 1).Append(_lines[i]).Append('\n');
        sb.Append("ENDSEC;\nEND-ISO-10303-21;\n");
        return sb.ToString();
    }

    public static string Ref(int id) => "#" + id.ToString(CultureInfo.InvariantCulture);
    public static string Refs(IEnumerable<int> ids) => "(" + string.Join(",", ids.Select(Ref)) + ")";
    public const string Null = "$";
    public const string Derived = "*";
    public static string Enum(string v) => "." + v + ".";
    public static string Bool(bool b) => b ? ".T." : ".F.";

    /// <summary>Real no formato STEP: sempre com ponto decimal (1. / 1.5 / 1.E-05).</summary>
    public static string Real(double d)
    {
        if (!double.IsFinite(d)) throw new ArgumentException("Valor real não finito em STEP.");
        string s = d.ToString("R", CultureInfo.InvariantCulture);
        int e = s.IndexOfAny(new[] { 'E', 'e' });
        string mantissa = e >= 0 ? s[..e] : s, exp = e >= 0 ? "E" + s[(e + 1)..] : "";
        if (!mantissa.Contains('.')) mantissa += ".";
        return mantissa + exp;
    }

    public static string Int(long v) => v.ToString(CultureInfo.InvariantCulture);

    public static string Point(params double[] c) => "(" + string.Join(",", c.Select(Real)) + ")";

    /// <summary>Texto STEP: aspas duplicadas, barra invertida escapada, não-ASCII em \X2\...\X0\.</summary>
    public static string Str(string? s)
    {
        if (s is null) return Null;
        var sb = new StringBuilder("'");
        var pending = new StringBuilder();
        void Flush()
        {
            if (pending.Length == 0) return;
            sb.Append("\\X2\\").Append(pending).Append("\\X0\\");
            pending.Clear();
        }
        foreach (char c in s)
        {
            if (c >= 32 && c <= 126)
            {
                Flush();
                if (c == '\'') sb.Append("''");
                else if (c == '\\') sb.Append("\\\\");
                else sb.Append(c);
            }
            else pending.Append(((int)c).ToString("X4", CultureInfo.InvariantCulture));
        }
        Flush();
        return sb.Append('\'').ToString();
    }
}

/// <summary>GlobalId IFC: GUID de 128 bits comprimido em 22 caracteres (alfabeto IFC).</summary>
public static class IfcGuid
{
    private const string Alphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz_$";

    public static string Compress(Guid g)
    {
        BigInteger n = BigInteger.Parse("0" + g.ToString("N"), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        var chars = new char[22];
        for (int i = 21; i >= 0; i--)
        {
            chars[i] = Alphabet[(int)(n % 64)];
            n /= 64;
        }
        return new string(chars);
    }

    public static Guid Expand(string s)
    {
        BigInteger n = 0;
        foreach (char c in s) n = n * 64 + Alphabet.IndexOf(c);
        string hex = n.ToString("x32", CultureInfo.InvariantCulture);
        if (hex.Length > 32) hex = hex[^32..];
        return Guid.ParseExact(hex, "N");
    }

    /// <summary>GUID determinístico a partir de um texto (mesma entrada → mesmo GlobalId).</summary>
    public static Guid FromName(string name)
    {
        byte[] h = MD5.HashData(Encoding.UTF8.GetBytes(name));
        h[6] = (byte)((h[6] & 0x0F) | 0x30); // versão 3 (baseado em nome)
        h[8] = (byte)((h[8] & 0x3F) | 0x80);
        return new Guid(Convert.ToHexString(h));
    }
}
