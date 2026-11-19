# PLUGIN ADVANCE STEEL - BRUNNO

Plugin independente para Autodesk Advance Steel 2027.

## Objetivo

Desenvolver um complemento próprio para o Advance Steel 2027, com duas frentes:

1. **Integração com o AdvancedSteelMCP:** aproveitar o servidor MCP existente como ponte de comunicação e acesso às operações que ele efetivamente disponibiliza. O repositório original não será alterado nem incorporado como cópia.
2. **Exportador IFC próprio:** gerar IFC diretamente a partir dos dados extraídos do modelo Advance Steel, com georreferenciamento SIRGAS 2000 / UTM incorporado ao arquivo. O exportador IFC nativo do Advance Steel não faz parte da solução pretendida.

## Escopo inicial

- Investigar os requisitos de runtime e API do Advance Steel 2027 antes de definir o framework do plugin.
- Documentar o contrato de comunicação com o AdvancedSteelMCP.
- Definir uma representação intermediária independente da API do Advance Steel.
- Especificar e testar a transformação entre coordenadas locais e coordenadas projetadas.
- Implementar a geração IFC em módulo separado da integração CAD.

## Arquitetura planejada

| Módulo | Responsabilidade |
|---|---|
| Plugin | Interface e ciclo de vida dentro do Advance Steel |
| MCP | Comunicação com o servidor AdvancedSteelMCP |
| Model | Representação neutra dos elementos extraídos |
| Georeferencing | Parâmetros e transformação de coordenadas |
| IfcExporter | Construção e gravação do arquivo IFC |
| Tests | Testes unitários, geométricos e de integração |

## Decisões técnicas ainda pendentes

Não fixar versão do .NET, framework de UI, biblioteca IFC ou estratégia de carregamento até confirmar a compatibilidade com o Advance Steel 2027 e com o MCP existente.

O primeiro alvo de exportação será IFC4, utilizando os mecanismos próprios de georreferenciamento do esquema (incluindo `IfcProjectedCRS` e `IfcMapConversion`, quando aplicáveis). A compatibilidade IFC2x3 será avaliada separadamente.

A zona UTM não será fixa. A solução deverá permitir seleção ou determinação da zona adequada, com validação explícita.

## Estrutura

- `src/`: módulos da aplicação.
- `tests/`: testes automatizados e casos de validação.
- `docs/`: arquitetura, decisões e especificações técnicas.

## Status

**Etapa 1 — estrutura e especificações iniciais.** Nenhuma funcionalidade de extração ou exportação está implementada ainda.

Consulte [docs/ROADMAP.md](docs/ROADMAP.md) para as etapas e critérios de conclusão.
