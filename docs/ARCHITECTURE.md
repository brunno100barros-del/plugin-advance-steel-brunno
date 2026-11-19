# Arquitetura inicial

## Princípio

A aplicação será dividida em módulos com contratos explícitos. A API do Advance Steel não deve contaminar o núcleo de transformação de dados nem o exportador IFC. Isso permite testar a lógica sem iniciar o CAD e trocar componentes de infraestrutura sem reescrever as regras de negócio.

## Fluxo lógico

```text
Advance Steel 2027
       |
       v
Plugin / Adapter
       |
       v
AdvancedSteelMCP (servidor existente)
       |
       v
MCP Client / Adapter
       |
       v
Neutral Model
       |
       +----------------------+
       |                      |
       v                      v
Georeferencing           IFC Exporter
       |                      |
       +----------+-----------+
                  v
              IFC4 file
```

O diagrama representa a arquitetura pretendida, não uma integração já implementada. A direção exata das chamadas entre o add-in e o servidor MCP será confirmada pela inspeção do repositório existente.

## Módulos

### Plugin

Responsável pelo carregamento no Advance Steel, comandos, interação com a seleção do usuário e acesso à API Autodesk. Deve ser fino: extrai dados e encaminha operações aos módulos centrais.

### MCP

Responsável por estabelecer a comunicação com o servidor AdvancedSteelMCP, respeitando o transporte, formato de mensagens, ciclo de vida e ferramentas realmente implementadas no projeto original.

### Model

Representação intermediária independente do CAD. Deve guardar identificadores de origem, tipo de objeto, geometria, unidades, propriedades e referências necessárias à rastreabilidade. Campos indisponíveis não devem ser inventados.

### Georeferencing

Responsável por parâmetros de referência espacial e transformações matemáticas. Não deve depender de interface gráfica nem de classes Autodesk.

### IfcExporter

Responsável por converter o modelo neutro em entidades IFC, definir unidades e estrutura espacial, aplicar o georreferenciamento e gravar o arquivo. Deve ser independente do exportador nativo do Advance Steel.

### Tests

Conterá testes unitários, casos geométricos conhecidos, testes de integração MCP e testes de validação IFC.

## Regras de dependência

- O núcleo Model não depende da API Autodesk nem da biblioteca IFC.
- Georeferencing não depende do Advance Steel nem da interface.
- IfcExporter recebe o modelo neutro e os parâmetros de georreferenciamento.
- O adapter Autodesk é o único módulo autorizado a depender diretamente das assemblies do Advance Steel.
- O servidor AdvancedSteelMCP permanece externo e independente.

## Decisões ainda não tomadas

- Target framework e runtime do plugin.
- Modelo de carregamento e referências da API Autodesk.
- Transporte e topologia exatos da conexão MCP.
- Biblioteca IFC e versão compatível.
- Estratégia de empacotamento e instalação.
