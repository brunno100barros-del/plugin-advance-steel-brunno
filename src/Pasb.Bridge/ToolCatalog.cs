using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

namespace Pasb.Bridge;

/// <summary>Uma ferramenta exposta ao assistente de IA.</summary>
public sealed record ToolDef(string Name, string Title, string Description, JsonObject InputSchema, bool ReadOnly);

/// <summary>
/// Lista fechada de ferramentas do Conector IA. O servidor MCP publica exatamente esta lista
/// e o plugin recusa qualquer método fora dela. Nenhuma ferramenta altera o desenho.
/// </summary>
public static class ToolCatalog
{
    public const string Status = "pasb_status";
    public const string ReadModel = "pasb_read_model";
    public const string GetObject = "pasb_get_object";
    public const string ExportIfc = "pasb_export_ifc";

    public static readonly IReadOnlyList<ToolDef> All = new[]
    {
        new ToolDef(Status, "Status do Advance Steel",
            "Mostra se o Advance Steel está conectado, a versão do plugin PASB, o desenho ativo e a unidade do desenho. Não lê objetos.",
            Schema(new JsonObject()), true),
        new ToolDef(ReadModel, "Ler o modelo",
            "Lê TODOS os objetos do modelo do Advance Steel (somente leitura, transação descartada e DBMOD conferido) e devolve " +
            "contagens por classe e por categoria e uma lista de objetos (handle, classe, categoria, perfil/material quando houver). " +
            "Use pasb_get_object para ver todas as propriedades de um objeto.",
            Schema(new JsonObject
            {
                ["max_objects"] = new JsonObject { ["type"] = "integer", ["minimum"] = 0, ["maximum"] = 5000, ["description"] = "Máximo de objetos na lista (padrão 300). As contagens sempre consideram o modelo inteiro." },
                ["category"] = new JsonObject { ["type"] = "string", ["description"] = "Filtra a lista por categoria (beam, plate, bolt, anchor, weld, special_part, joint...)." },
            }), true),
        new ToolDef(GetObject, "Ler um objeto",
            "Lê um objeto do Advance Steel pelo handle (hexadecimal, como aparece em pasb_read_model) e devolve todas as propriedades acessíveis, " +
            "com valor bruto, valor normalizado (SI) e situação de leitura, além da geometria usada no IFC. Somente leitura.",
            Schema(new JsonObject
            {
                ["handle"] = new JsonObject { ["type"] = "string", ["description"] = "Handle do objeto, por exemplo \"2F1\"." },
            }, "handle"), true),
        new ToolDef(ExportIfc, "Exportar IFC4",
            "Exporta o modelo inteiro para IFC4 com o exportador próprio do PASB (não usa o exportador da Autodesk). Lê o modelo sem alterá-lo " +
            "e grava só o arquivo .ifc e o relatório .report.json. Campos omitidos usam a última configuração da janela 'Exportar IFC'.",
            Schema(new JsonObject
            {
                ["output_path"] = new JsonObject { ["type"] = "string", ["description"] = "Caminho completo do arquivo .ifc a gravar." },
                ["overwrite"] = new JsonObject { ["type"] = "boolean", ["description"] = "Sobrescrever se o arquivo já existir (padrão false)." },
                ["project_name"] = new JsonObject { ["type"] = "string" },
                ["site_name"] = new JsonObject { ["type"] = "string" },
                ["building_name"] = new JsonObject { ["type"] = "string" },
                ["object_groups"] = new JsonObject
                {
                    ["type"] = "array", ["items"] = new JsonObject { ["type"] = "string", ["enum"] = Enum("beams", "plates", "bolts", "anchors", "welds", "special_parts", "joints", "others") },
                    ["description"] = "Tipos de objeto a exportar (padrão: os marcados na janela).",
                },
                ["georeference"] = new JsonObject { ["type"] = "string", ["enum"] = Enum("none", "sirgas", "local"), ["description"] = "none, sirgas (SIRGAS 2000 / UTM) ou local (X, Y, Z da obra)." },
                ["utm_zone"] = new JsonObject { ["type"] = "integer", ["minimum"] = 1, ["maximum"] = 60 },
                ["hemisphere"] = new JsonObject { ["type"] = "string", ["enum"] = Enum("S", "N") },
                ["local_crs_name"] = new JsonObject { ["type"] = "string", ["description"] = "Nome do sistema de coordenadas da obra (modo local)." },
                ["base_point_model"] = Vec3("Ponto base no modelo, em unidades do desenho (mm), coordenadas globais."),
                ["target_point"] = Vec3("Coordenadas reais do ponto base (E, N, H ou X, Y, Z), na unidade target_unit."),
                ["target_unit"] = new JsonObject { ["type"] = "string", ["enum"] = Enum("m", "mm"), ["description"] = "Unidade de target_point (padrão: a da janela)." },
                ["angle_mode"] = new JsonObject { ["type"] = "string", ["enum"] = Enum("rotation", "azimuth"), ["description"] = "rotation: anti-horária do X do modelo até o Leste; azimuth: horário do Norte até o +Y do modelo." },
                ["angle_degrees"] = new JsonObject { ["type"] = "number" },
            }, "output_path"), false),
    };

    public static ToolDef? Find(string name) => All.FirstOrDefault(t => string.Equals(t.Name, name, StringComparison.Ordinal));

    private static JsonObject Schema(JsonObject properties, params string[] required)
    {
        var o = new JsonObject { ["type"] = "object", ["properties"] = properties, ["additionalProperties"] = false };
        if (required.Length > 0) o["required"] = new JsonArray(required.Select(r => (JsonNode)JsonValue.Create(r)!).ToArray());
        return o;
    }

    private static JsonArray Enum(params string[] values) => new(values.Select(v => (JsonNode)JsonValue.Create(v)!).ToArray());

    private static JsonObject Vec3(string description) => new()
    {
        ["type"] = "array", ["items"] = new JsonObject { ["type"] = "number" }, ["minItems"] = 3, ["maxItems"] = 3, ["description"] = description,
    };
}
