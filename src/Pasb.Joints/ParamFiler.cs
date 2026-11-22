using System;
using System.Globalization;
using System.Reflection;
using Autodesk.AdvanceSteel.ConstructionTypes;

namespace Pasb.Joints;

/// <summary>
/// Grava e lê os parâmetros de uma ligação no "filer", um item por propriedade (o nome do item é o nome
/// da propriedade, que é o que as abas da janela da ligação usam). Tipos no filer: double, string e int
/// (bool vira 0/1 e enum vira o número do item), que são os tipos aceitos pelas abas do Advance Steel.
/// </summary>
internal static class ParamFiler<T> where T : class, new()
{
    private static readonly PropertyInfo[] Props = Array.FindAll(typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance),
        p => p.CanRead && p.CanWrite && p.Name != "SchemaVersion");

    public static void Write(IFiler filer, T b)
    {
        foreach (PropertyInfo p in Props) filer.WriteItem(ToFiler(p.GetValue(b), p.PropertyType), p.Name);
    }

    /// <summary>Lê cada item; item ausente (ligação gravada por versão anterior) mantém o valor padrão.</summary>
    public static T Read(IFiler filer)
    {
        var b = new T();
        foreach (PropertyInfo p in Props)
        {
            try
            {
                object? v = filer.ReadItem(p.Name);
                if (v != null) p.SetValue(b, FromFiler(v, p.PropertyType));
            }
            catch (Exception) { }
        }
        return b;
    }

    private static object ToFiler(object? v, Type t)
    {
        if (t == typeof(bool)) return (bool)v! ? 1 : 0;
        if (t.IsEnum) return Convert.ToInt32(v, CultureInfo.InvariantCulture);
        if (t == typeof(double)) return (double)v!;
        if (t == typeof(int)) return (int)v!;
        return v as string ?? "";
    }

    private static object FromFiler(object v, Type t)
    {
        if (t == typeof(bool)) return Convert.ToInt32(v, CultureInfo.InvariantCulture) != 0;
        if (t.IsEnum) return Enum.ToObject(t, Convert.ToInt32(v, CultureInfo.InvariantCulture));
        if (t == typeof(double)) return Convert.ToDouble(v, CultureInfo.InvariantCulture);
        if (t == typeof(int)) return Convert.ToInt32(v, CultureInfo.InvariantCulture);
        return Convert.ToString(v, CultureInfo.InvariantCulture) ?? "";
    }
}
