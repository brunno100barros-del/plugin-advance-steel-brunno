# Código-fonte

Os módulos abaixo serão criados após a validação do runtime e da API do Advance Steel 2027. A estrutura lógica planejada está descrita em [docs/ARCHITECTURE.md](../docs/ARCHITECTURE.md).

- Plugin: integração com o host Autodesk.
- MCP: cliente do servidor AdvancedSteelMCP.
- Model: modelo neutro de dados.
- Georeferencing: transformação espacial.
- IfcExporter: geração IFC própria.

Não adicionar dependências de runtime antes de fechar a ADR-001.
