using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json.Nodes;

namespace Pasb.Diagnostics.Core;

/// <summary>
/// Junta as leituras de um objeto. Cada leitura é isolada: uma exceção vira um registro ReadError
/// e nunca interrompe as demais.
/// </summary>
public sealed class RecordBuilder
{
    /// <summary>Converte valores que o núcleo não conhece (tipos Autodesk). Retorna false se não souber.</summary>
    public delegate bool ExtraSerializer(object value, out JsonNode? node, out ReadStatus status);

    private readonly ExtraSerializer? _extra;
    private readonly Action<string>? _beforeRead;

    /// <param name="extra">Serializador de tipos que o núcleo não conhece.</param>
    /// <param name="beforeRead">Chamado com "Classe.Membro(argumento)" imediatamente antes de cada leitura (trace).</param>
    public RecordBuilder(ExtraSerializer? extra = null, Action<string>? beforeRead = null)
    {
        _extra = extra;
        _beforeRead = beforeRead;
    }

    public List<PropertyRecord> Records { get; } = new();

    /// <summary>Executa uma leitura e registra o resultado.</summary>
    public void Read(string name, string declaringType, string source, string? argument, Type declaredType, Func<object?> read)
    {
        _beforeRead?.Invoke(declaringType + "." + name + (argument is null ? "" : "(" + argument + ")") + " [" + source + "]");
        object? value;
        try
        {
            value = read();
        }
        catch (Exception ex)
        {
            Exception inner = ex is TargetInvocationException { InnerException: { } ie } ? ie : ex;
            Records.Add(new PropertyRecord(name, declaringType, source, argument, ValueSerializer.TypeName(declaredType),
                ReadStatus.ReadError, null, new ReadError(inner.GetType().FullName ?? inner.GetType().Name, inner.Message)));
            return;
        }
        Records.Add(Serialize(name, declaringType, source, argument, declaredType, value));
    }

    /// <summary>Registra um membro que não pode ser lido, com o motivo.</summary>
    public void Unavailable(string name, string declaringType, string source, Type declaredType, string reason) =>
        Records.Add(new PropertyRecord(name, declaringType, source, null, ValueSerializer.TypeName(declaredType),
            ReadStatus.Unavailable, null, new ReadError("Unavailable", reason)));

    public PropertyRecord Serialize(string name, string declaringType, string source, string? argument, Type declaredType, object? value)
    {
        string dataType = ValueSerializer.TypeName(value?.GetType() ?? declaredType);

        if (ValueSerializer.TrySerialize(value, out JsonNode? node, out ReadStatus status))
            return new PropertyRecord(name, declaringType, source, argument, dataType, status, node, null);

        if (_extra is not null)
        {
            try
            {
                if (_extra(value!, out node, out status))
                    return new PropertyRecord(name, declaringType, source, argument, dataType, status, node, null);
            }
            catch (Exception ex)
            {
                return new PropertyRecord(name, declaringType, source, argument, dataType, ReadStatus.ReadError, null,
                    new ReadError(ex.GetType().FullName ?? ex.GetType().Name, "Falha ao serializar: " + ex.Message));
            }
        }

        // Tipo desconhecido: guarda o nome e o texto, para revisão, sem fingir que foi serializado.
        string? text;
        try { text = value!.ToString(); } catch (Exception) { text = null; }
        return new PropertyRecord(name, declaringType, source, argument, dataType, ReadStatus.ComplexNotSerialized,
            new JsonObject { ["clrType"] = dataType, ["toString"] = text }, null);
    }
}
