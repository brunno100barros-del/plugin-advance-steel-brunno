using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Pasb.Model;

/// <summary>Gravação e leitura do modelo neutro em JSON (enums como texto, nulos omitidos).</summary>
public static class NeutralModelJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    public static string Serialize(Extraction x) => JsonSerializer.Serialize(x, Options);

    public static void Write(Extraction x, string path) =>
        File.WriteAllText(path, Serialize(x), new UTF8Encoding(false));
}
