using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Pasb.IfcImport.Model;

// ======================================================================== resumo do arquivo (scan)

/// <summary>Resumo de um IFC: o que a janela mostra antes de importar.</summary>
public sealed class IfcScan
{
    public string File { get; set; } = "";
    public string Schema { get; set; } = "";
    public string? ProjectName { get; set; }
    /// <summary>Quantas unidades do IFC há em 1 metro (1000 = milímetros).</summary>
    public double UnitsPerMetre { get; set; } = 1;
    public List<ScanType> Types { get; set; } = new();
    public List<ScanStorey> Storeys { get; set; } = new();
    public List<string> Layers { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

public sealed class ScanType
{
    public string Name { get; set; } = "";          // ex.: IfcColumn
    public int Count { get; set; }
}

public sealed class ScanStorey
{
    /// <summary>Chave estável do nível (GlobalId; "(sem nível)" para elementos fora de pavimentos).</summary>
    public string Key { get; set; } = "";
    public string Name { get; set; } = "";
    public double? Elevation { get; set; }          // unidades do IFC
    public int Count { get; set; }
}

public static class ScanJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static string Serialize(IfcScan s) => JsonSerializer.Serialize(s, Options);
    public static IfcScan Deserialize(string json) => JsonSerializer.Deserialize<IfcScan>(json, Options) ?? new IfcScan();
}

// ======================================================================== pedido de conversão

/// <summary>O que converter (gravado em JSON e passado ao Pasb.IfcImport.exe).</summary>
public sealed class ConvertRequest
{
    public string IfcFile { get; set; } = "";
    /// <summary>Tipos IFC a incluir (vazio = todos os que têm geometria).</summary>
    public List<string> Types { get; set; } = new();
    /// <summary>Chaves dos níveis a incluir (vazio = todos).</summary>
    public List<string> Storeys { get; set; } = new();
    public bool IncludeOpenings { get; set; }
    public bool IncludeSpaces { get; set; }
}

// ======================================================================== pacote de malhas (binário)

/// <summary>Geometria triangulada (vértices em metros, no sistema do próprio desenho de origem do IFC).</summary>
public sealed class MeshGeometry
{
    public int Id { get; set; }
    public double[] Vertices { get; set; } = Array.Empty<double>();   // x,y,z,x,y,z...
    public int[] Triangles { get; set; } = Array.Empty<int>();        // a,b,c,a,b,c...
    /// <summary>Quantas instâncias usam esta geometria (≥ 2 = candidata a bloco).</summary>
    public int ReferenceCount { get; set; }
    public int VertexCount => Vertices.Length / 3;
    public int TriangleCount => Triangles.Length / 3;
}

/// <summary>Uma ocorrência de geometria num elemento IFC.</summary>
public sealed class MeshInstance
{
    public int GeometryId { get; set; }
    /// <summary>Matriz 4×4 (linhas, translação na última linha, como a xBIM) em metros.</summary>
    public double[] Transform { get; set; } = Identity();
    public string IfcType { get; set; } = "";
    public string? Name { get; set; }
    public string? GlobalId { get; set; }
    public string StoreyKey { get; set; } = NoStorey;
    public string? IfcLayer { get; set; }
    /// <summary>Cor RGB do IFC (0..255) ou nula se o IFC não tiver estilo.</summary>
    public byte[]? Color { get; set; }
    public double Transparency { get; set; }

    public const string NoStorey = "(sem nível)";
    public static double[] Identity() => new double[] { 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1 };
}

public sealed class MeshPackage
{
    public List<MeshGeometry> Geometries { get; } = new();
    public List<MeshInstance> Instances { get; } = new();
    public List<string> Warnings { get; } = new();

    private const string Magic = "PASBMESH";
    private const int FormatVersion = 1;

    public void Write(Stream s)
    {
        using var w = new BinaryWriter(s, Encoding.UTF8, leaveOpen: true);
        w.Write(Encoding.ASCII.GetBytes(Magic)); w.Write(FormatVersion);
        w.Write(Geometries.Count);
        foreach (MeshGeometry g in Geometries)
        {
            w.Write(g.Id); w.Write(g.ReferenceCount);
            w.Write(g.Vertices.Length); foreach (double v in g.Vertices) w.Write(v);
            w.Write(g.Triangles.Length); foreach (int t in g.Triangles) w.Write(t);
        }
        w.Write(Instances.Count);
        foreach (MeshInstance i in Instances)
        {
            w.Write(i.GeometryId);
            for (int k = 0; k < 16; k++) w.Write(i.Transform[k]);
            w.Write(i.IfcType); Str(w, i.Name); Str(w, i.GlobalId); w.Write(i.StoreyKey); Str(w, i.IfcLayer);
            w.Write(i.Color != null);
            if (i.Color != null) w.Write(i.Color, 0, 3);
            w.Write(i.Transparency);
        }
        w.Write(Warnings.Count);
        foreach (string x in Warnings) w.Write(x);
    }

    public static MeshPackage Read(Stream s)
    {
        using var r = new BinaryReader(s, Encoding.UTF8, leaveOpen: true);
        if (Encoding.ASCII.GetString(r.ReadBytes(Magic.Length)) != Magic) throw new InvalidDataException("Arquivo de malhas inválido.");
        int ver = r.ReadInt32();
        if (ver != FormatVersion) throw new InvalidDataException($"Versão do arquivo de malhas não suportada: {ver}.");
        var p = new MeshPackage();
        int ng = r.ReadInt32();
        for (int k = 0; k < ng; k++)
        {
            var g = new MeshGeometry { Id = r.ReadInt32(), ReferenceCount = r.ReadInt32() };
            g.Vertices = new double[r.ReadInt32()]; for (int j = 0; j < g.Vertices.Length; j++) g.Vertices[j] = r.ReadDouble();
            g.Triangles = new int[r.ReadInt32()]; for (int j = 0; j < g.Triangles.Length; j++) g.Triangles[j] = r.ReadInt32();
            p.Geometries.Add(g);
        }
        int ni = r.ReadInt32();
        for (int k = 0; k < ni; k++)
        {
            var i = new MeshInstance { GeometryId = r.ReadInt32() };
            for (int j = 0; j < 16; j++) i.Transform[j] = r.ReadDouble();
            i.IfcType = r.ReadString(); i.Name = RStr(r); i.GlobalId = RStr(r); i.StoreyKey = r.ReadString(); i.IfcLayer = RStr(r);
            if (r.ReadBoolean()) i.Color = r.ReadBytes(3);
            i.Transparency = r.ReadDouble();
            p.Instances.Add(i);
        }
        int nw = r.ReadInt32();
        for (int k = 0; k < nw; k++) p.Warnings.Add(r.ReadString());
        return p;
    }

    private static void Str(BinaryWriter w, string? s) { w.Write(s != null); if (s != null) w.Write(s); }
    private static string? RStr(BinaryReader r) => r.ReadBoolean() ? r.ReadString() : null;

    /// <summary>Aplica a matriz (linhas, translação na última linha) a um ponto.</summary>
    public static (double x, double y, double z) Apply(double[] m, double x, double y, double z) =>
        (x * m[0] + y * m[4] + z * m[8] + m[12],
         x * m[1] + y * m[5] + z * m[9] + m[13],
         x * m[2] + y * m[6] + z * m[10] + m[14]);
}

// ======================================================================== nomes e cores

public static class ImportNaming
{
    /// <summary>Nome de layer válido no AutoCAD (sem &lt;&gt;/\":;?*|,=`), com prefixo.</summary>
    public static string Layer(string prefix, string? name)
    {
        string n = string.IsNullOrWhiteSpace(name) ? "SemNome" : name.Trim();
        var sb = new StringBuilder();
        foreach (char c in n) sb.Append("<>/\\\":;?*|,=`".IndexOf(c) >= 0 || char.IsControl(c) ? '_' : c);
        string s = prefix + sb;
        return s.Length > 255 ? s[..255] : s;
    }

    /// <summary>Paleta padrão para colorir níveis (índices de cor do AutoCAD, bem distintos entre si).</summary>
    public static readonly short[] StoreyPalette = { 1, 3, 5, 30, 6, 4, 2, 140, 210, 41, 92, 172, 12, 62, 152, 232 };

    public static short StoreyColor(int index) => StoreyPalette[index % StoreyPalette.Length];
}
