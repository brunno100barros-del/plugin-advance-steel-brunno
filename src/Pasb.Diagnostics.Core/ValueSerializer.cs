using System;
using System.Globalization;
using System.Text.Json.Nodes;

namespace Pasb.Diagnostics.Core;

/// <summary>
/// Serialização sem perda de valores .NET comuns (docs/DIAGNOSTIC-DESIGN.md, seção 6).
/// Tipos específicos da Autodesk são tratados no adapter, não aqui.
/// </summary>
public static class ValueSerializer
{
    /// <summary>
    /// Tenta serializar um valor de tipo .NET básico.
    /// Retorna false se o tipo não for básico (o chamador tenta outras regras).
    /// </summary>
    public static bool TrySerialize(object? value, out JsonNode? node, out ReadStatus status)
    {
        node = null;
        status = ReadStatus.Read;

        switch (value)
        {
            case null:
                status = ReadStatus.Null;
                return true;

            case string s:
                // Sem Trim: espaços fazem parte do valor original.
                node = JsonValue.Create(s);
                status = s.Length == 0 ? ReadStatus.Empty : ReadStatus.Read;
                return true;

            case bool b:
                node = JsonValue.Create(b);
                return true;

            case double d:
                node = Number(d);
                return true;

            case float f:
                node = Number(f);
                return true;

            case decimal m:
                node = JsonValue.Create(m);
                return true;

            case byte or sbyte or short or ushort or int or uint or long:
                node = JsonValue.Create(Convert.ToInt64(value, CultureInfo.InvariantCulture));
                return true;

            case ulong ul:
                node = JsonValue.Create(ul);
                return true;

            case char c:
                node = JsonValue.Create(c.ToString());
                return true;

            case Guid g:
                node = JsonValue.Create(g.ToString("D"));
                return true;

            case DateTime dt:
                node = JsonValue.Create(dt.ToString("O", CultureInfo.InvariantCulture));
                return true;

            case Enum e:
                node = EnumValue(e);
                return true;

            case byte[] bytes:
                node = JsonValue.Create(Convert.ToBase64String(bytes));
                status = bytes.Length == 0 ? ReadStatus.Empty : ReadStatus.Read;
                return true;
        }

        return false;
    }

    /// <summary>
    /// Número com precisão total. System.Text.Json grava double no formato mais curto que volta ao mesmo valor.
    /// NaN e infinito não existem em JSON: são gravados como texto ("NaN", "Infinity", "-Infinity").
    /// </summary>
    public static JsonNode Number(double d) =>
        double.IsFinite(d)
            ? JsonValue.Create(d)
            : JsonValue.Create(d.ToString(CultureInfo.InvariantCulture));

    public static JsonObject EnumValue(Enum e)
    {
        object underlying = Convert.ChangeType(e, Enum.GetUnderlyingType(e.GetType()), CultureInfo.InvariantCulture);
        return new JsonObject
        {
            ["name"] = e.ToString(),
            ["value"] = JsonValue.Create(Convert.ToInt64(underlying, CultureInfo.InvariantCulture)),
        };
    }

    /// <summary>Nome legível de um tipo, incluindo argumentos genéricos.</summary>
    public static string TypeName(Type? t)
    {
        if (t is null) return "null";
        if (!t.IsGenericType) return t.FullName ?? t.Name;
        string name = t.GetGenericTypeDefinition().FullName ?? t.Name;
        int tick = name.IndexOf('`');
        if (tick >= 0) name = name[..tick];
        return name + "<" + string.Join(", ", Array.ConvertAll(t.GetGenericArguments(), TypeName)) + ">";
    }
}
