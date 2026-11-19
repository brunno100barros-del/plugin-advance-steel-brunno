# PLUGIN ADVANCE STEEL - BRUNNO

Plugin independente para Autodesk Advance Steel 2027.

## Objetivo

Desenvolver um complemento próprio com integração ao AdvancedSteelMCP existente e exportador IFC próprio georreferenciado SIRGAS 2000 / UTM. O repositório original do MCP não será alterado nem copiado. O exportador IFC nativo do Advance Steel não faz parte da solução.

## Arquitetura

| Módulo | Responsabilidade |
|---|---|
| Plugin | Interface e ciclo de vida no Advance Steel |
| MCP | Comunicação com servidor AdvancedSteelMCP |
| Model | Representação neutra dos elementos |
| Georeferencing | Transformação de coordenadas |
| IfcExporter | Construção e gravação IFC |
| Tests | Testes unitários e de integração |

## Descobertas da Etapa 2

A revisão estática confirmou servidor Python/FastMCP, comunicação stdio com cliente, ponte TCP local entre Python e Loader C#, token por sessão, despacho para a thread principal e Core C# que acessa APIs Autodesk. O projeto declara 27 ferramentas e unidades mm, kg e graus, coordenadas WCS com Z para cima.

Os dados inspecionados usam handles do desenho; isso não equivale a GlobalIds IFC. A existência de ferramentas e propriedades não comprova fidelidade geométrica de todos os componentes.

Consulte docs/MCP-REVIEW.md e docs/LOCAL-VALIDATION.md. A conexão e a extração real ainda precisam ser testadas no computador com Advance Steel.

## Plataforma

O projeto original declara net10.0-windows e x64, com assemblies do AutoCAD 2027 e Advance Steel na subpasta ADVS. A documentação Autodesk consultada confirma .NET 10 para plugins gerenciados AutoCAD 2027 e disponibiliza SDK específico do Advance Steel 2027. A compatibilidade vertical final depende de validação local.

## IFC e georreferenciamento

Primeiro alvo: IFC4, com IfcProjectedCRS e IfcMapConversion quando aplicáveis. A zona UTM não será fixa.

## Status

Etapa 1: fundação documental publicada.
Etapa 2: revisão estática e levantamento técnico concluídos; teste em execução pendente.
Nenhuma extração própria ou exportação IFC foi implementada ainda.
