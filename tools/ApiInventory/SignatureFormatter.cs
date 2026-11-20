using System.Collections.Immutable;
using System.Reflection.Metadata;

namespace ApiInventory;

/// <summary>
/// Converte assinaturas de metadados em texto legível (estilo C#).
/// Apenas decodifica bytes de metadados; não carrega nem executa nenhuma assembly.
/// </summary>
internal sealed class SignatureFormatter : ISignatureTypeProvider<string, object?>
{
    public string GetPrimitiveType(PrimitiveTypeCode typeCode) => typeCode switch
    {
        PrimitiveTypeCode.Boolean => "bool",
        PrimitiveTypeCode.Byte => "byte",
        PrimitiveTypeCode.SByte => "sbyte",
        PrimitiveTypeCode.Char => "char",
        PrimitiveTypeCode.Int16 => "short",
        PrimitiveTypeCode.UInt16 => "ushort",
        PrimitiveTypeCode.Int32 => "int",
        PrimitiveTypeCode.UInt32 => "uint",
        PrimitiveTypeCode.Int64 => "long",
        PrimitiveTypeCode.UInt64 => "ulong",
        PrimitiveTypeCode.Single => "float",
        PrimitiveTypeCode.Double => "double",
        PrimitiveTypeCode.String => "string",
        PrimitiveTypeCode.Object => "object",
        PrimitiveTypeCode.Void => "void",
        PrimitiveTypeCode.IntPtr => "nint",
        PrimitiveTypeCode.UIntPtr => "nuint",
        PrimitiveTypeCode.TypedReference => "TypedReference",
        _ => typeCode.ToString(),
    };

    public string GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind)
        => Names.Of(reader, handle);

    public string GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind)
        => Names.Of(reader, handle);

    public string GetTypeFromSpecification(MetadataReader reader, object? genericContext, TypeSpecificationHandle handle, byte rawTypeKind)
        => reader.GetTypeSpecification(handle).DecodeSignature(this, genericContext);

    public string GetSZArrayType(string elementType) => elementType + "[]";

    public string GetArrayType(string elementType, ArrayShape shape) => elementType + "[" + new string(',', Math.Max(0, shape.Rank - 1)) + "]";

    public string GetByReferenceType(string elementType) => elementType + "&";

    public string GetPointerType(string elementType) => elementType + "*";

    public string GetPinnedType(string elementType) => elementType;

    public string GetGenericInstantiation(string genericType, ImmutableArray<string> typeArguments)
    {
        int tick = genericType.IndexOf('`');
        string baseName = tick >= 0 ? genericType[..tick] : genericType;
        return baseName + "<" + string.Join(", ", typeArguments) + ">";
    }

    public string GetGenericTypeParameter(object? genericContext, int index) => "!" + index;

    public string GetGenericMethodParameter(object? genericContext, int index) => "!!" + index;

    // Modificadores (modopt/modreq) são comuns em assemblies C++/CLI (ex.: IsConst, IsLong).
    // São omitidos no texto; o tipo subjacente é preservado.
    public string GetModifiedType(string modifier, string unmodifiedType, bool isRequired) => unmodifiedType;

    public string GetFunctionPointerType(MethodSignature<string> signature) => "fnptr";
}

internal static class Names
{
    public static string Of(MetadataReader reader, TypeDefinitionHandle handle)
    {
        TypeDefinition td = reader.GetTypeDefinition(handle);
        string name = reader.GetString(td.Name);
        TypeDefinitionHandle declaring = td.GetDeclaringType();
        if (!declaring.IsNil) return Of(reader, declaring) + "+" + name;
        string ns = reader.GetString(td.Namespace);
        return ns.Length == 0 ? name : ns + "." + name;
    }

    public static string Of(MetadataReader reader, TypeReferenceHandle handle)
    {
        TypeReference tr = reader.GetTypeReference(handle);
        string name = reader.GetString(tr.Name);
        if (tr.ResolutionScope.Kind == HandleKind.TypeReference)
            return Of(reader, (TypeReferenceHandle)tr.ResolutionScope) + "+" + name;
        string ns = reader.GetString(tr.Namespace);
        return ns.Length == 0 ? name : ns + "." + name;
    }

    public static string? Of(MetadataReader reader, EntityHandle handle, SignatureFormatter formatter)
    {
        if (handle.IsNil) return null;
        return handle.Kind switch
        {
            HandleKind.TypeDefinition => Of(reader, (TypeDefinitionHandle)handle),
            HandleKind.TypeReference => Of(reader, (TypeReferenceHandle)handle),
            HandleKind.TypeSpecification => reader.GetTypeSpecification((TypeSpecificationHandle)handle).DecodeSignature(formatter, null),
            _ => handle.Kind.ToString(),
        };
    }
}
