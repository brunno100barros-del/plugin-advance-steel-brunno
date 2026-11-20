using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;

namespace Pasb.Model;

/// <summary>Estado de uma propriedade (docs/NEUTRAL-MODEL.md, seção 8).</summary>
public enum PropertyStatus { Read, Empty, Null, NotApplicable, Unavailable, ReadError, ComplexNotSerialized }

/// <summary>Estado de um elemento (docs/NEUTRAL-MODEL.md, seção 8).</summary>
public enum ElementStatus { Valid, Partial, Unsupported, Invalid }

/// <summary>Uma extração completa: origem, metadados, elementos e inventário de propriedades.</summary>
public sealed class Extraction
{
    public SourceDocument SourceDocument { get; set; } = new();
    public ExtractionInfo Info { get; set; } = new();
    public List<NeutralElement> Elements { get; } = new();

    /// <summary>Inventário: categoria → nomes técnicos de propriedades descobertas.</summary>
    public SortedDictionary<string, SortedSet<string>> PropertyInventory { get; } = new(StringComparer.Ordinal);

    /// <summary>Dados brutos da origem, para auditoria (docs/NEUTRAL-MODEL.md, seção 2).</summary>
    public JsonObject? RawRun { get; set; }
}

public sealed class SourceDocument
{
    public string? Name { get; set; }
    /// <summary>Unidade de comprimento declarada no desenho (INSUNITS), como texto original.</summary>
    public string? DeclaredLengthUnit { get; set; }
    public string? ApplicationVersion { get; set; }
}

public sealed class ExtractionInfo
{
    public string? Tool { get; set; }
    public string? ToolVersion { get; set; }
    public string? Command { get; set; }
    public string? StartedUtc { get; set; }
    public string? TransactionMode { get; set; }
    public List<string> Warnings { get; } = new();
}

public sealed class NeutralElement
{
    public string InternalId { get; set; } = "";
    public string SourceSystem { get; set; } = "AdvanceSteel";
    /// <summary>Handle de origem. Não é GlobalId.</summary>
    public string? SourceHandle { get; set; }
    public string? SourceType { get; set; }
    public string Category { get; set; } = "unknown";
    public List<string> Inheritance { get; } = new();
    /// <summary>GUID lido da origem (GetUniqueId). Persistência ainda não comprovada; não usar como IfcGlobalId sem validação.</summary>
    public string? SourceUniqueId { get; set; }
    /// <summary>Gerado só na exportação IFC; permanece nulo no modelo neutro.</summary>
    public string? IfcGlobalId { get; set; }
    public List<NeutralProperty> Properties { get; } = new();
    public List<Relationship> Relationships { get; } = new();
    public ElementStatus Status { get; set; } = ElementStatus.Valid;
    public List<string> ValidationMessages { get; } = new();
    /// <summary>Identificação de objetos que não puderam ser abertos (classe AutoCAD, tipos AS).</summary>
    public JsonNode? Identification { get; set; }
}

public sealed class NeutralProperty
{
    public string TechnicalName { get; set; } = "";
    public string DeclaringType { get; set; } = "";
    public string? Argument { get; set; }
    public string Source { get; set; } = "";
    public string DataType { get; set; } = "";
    public PropertyStatus Status { get; set; }
    public JsonNode? RawValue { get; set; }
    /// <summary>Unidade original, só quando comprovada (docs/DIAGNOSTIC-TEST-LOG.md).</summary>
    public string? Unit { get; set; }
    /// <summary>Valor convertido para a unidade interna (m, kg), quando a unidade de origem é conhecida.</summary>
    public double? NormalizedValue { get; set; }
    public string? NormalizedUnit { get; set; }
    public string? Error { get; set; }

    /// <summary>Chave única dentro do elemento: Classe.Membro(argumento).</summary>
    public string Key => DeclaringType + "." + TechnicalName + (Argument is null ? "" : "(" + Argument + ")");
}

public sealed record Relationship(string Kind, string TargetHandle);
