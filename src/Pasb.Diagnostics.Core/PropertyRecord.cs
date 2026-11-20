using System.Text.Json.Nodes;

namespace Pasb.Diagnostics.Core;

/// <summary>Estado de uma leitura, conforme docs/NEUTRAL-MODEL.md e docs/DIAGNOSTIC-DESIGN.md (seção 7.1).</summary>
public enum ReadStatus
{
    /// <summary>Valor lido com sucesso.</summary>
    Read,
    /// <summary>Texto vazio ("") ou coleção vazia.</summary>
    Empty,
    /// <summary>A API devolveu null.</summary>
    Null,
    /// <summary>O membro não se aplica a este objeto.</summary>
    NotApplicable,
    /// <summary>O membro existe, mas não pode ser lido por este diagnóstico (por exemplo, ponteiro nativo).</summary>
    Unavailable,
    /// <summary>A leitura lançou exceção.</summary>
    ReadError,
    /// <summary>Valor lido, mas de um tipo que o diagnóstico ainda não sabe serializar sem perda.</summary>
    ComplexNotSerialized,
}

/// <summary>Origem da leitura.</summary>
public static class ReadSource
{
    public const string Identity = "identity";
    public const string Getter = "getter";
    public const string Method = "method";
}

/// <summary>Erro de leitura individual.</summary>
public sealed record ReadError(string Type, string Message);

/// <summary>Uma leitura de um membro de um objeto.</summary>
public sealed record PropertyRecord(
    string TechnicalName,
    string DeclaringType,
    string Source,
    string? Argument,
    string DataType,
    ReadStatus Status,
    JsonNode? RawValue,
    ReadError? Error)
{
    public JsonObject ToJson()
    {
        var j = new JsonObject
        {
            ["technicalName"] = TechnicalName,
            ["declaringType"] = DeclaringType,
            ["source"] = Source,
        };
        if (Argument is not null) j["argument"] = Argument;
        j["dataType"] = DataType;
        j["status"] = Status.ToString();
        // rawValue só aparece quando houve leitura; null explícito para o estado Null.
        if (Status is ReadStatus.Read or ReadStatus.Empty or ReadStatus.ComplexNotSerialized)
            j["rawValue"] = RawValue?.DeepClone();
        else if (Status == ReadStatus.Null)
            j["rawValue"] = null;
        if (Error is not null)
            j["error"] = new JsonObject { ["type"] = Error.Type, ["message"] = Error.Message };
        return j;
    }
}
