# Plano das ferramentas de inventário e diagnóstico

Status: planejamento. Nada descrito aqui foi implementado ou executado.

Data: 2026-11-19

## 1. Visão geral

Duas ferramentas independentes, ambas somente leitura:

| Ferramenta | Onde roda | O que produz | Precisa do Advance Steel aberto? |
|---|---|---|---|
| `tools/ApiInventory` | Console .NET 10 fora do CAD | Catálogo de tipos e membros públicos das assemblies | Não |
| Diagnóstico de modelo | Add-in .NET 10 dentro do Advance Steel 2027 | Valores lidos de cada objeto do DWG + relatório de cobertura | Sim |

A primeira define o que a API oferece; a segunda mede o que a API devolve num modelo real. A matriz [API-MCP-COVERAGE-MATRIX.md](API-MCP-COVERAGE-MATRIX.md) é alimentada pelas duas.

## 2. `tools/ApiInventory` (fora do CAD)

### Objetivo
Listar, para cada assembly `AS*Mgd.dll` instalada, todos os tipos públicos com herança, propriedades, métodos, campos, enums e interfaces.

### Abordagem
- Ler os metadados com `System.Reflection.Metadata` (parte do próprio .NET 10, sem pacotes externos).
- Não carregar nem executar as assemblies da Autodesk; não chamar nenhum código delas.
- Resolver a cadeia de herança entre assemblies (por exemplo `Beam` → `AtomicElement` → `ConstructionElement` → `FilerObject`).
- Cruzar cada membro com o XML de documentação, quando existir.

### Saídas
- `inventory/api-2027.json`: estrutura completa.
- `inventory/api-2027-members.csv`: uma linha por membro (assembly, tipo, base, membro, tipo de membro, assinatura, getter/setter, documentado sim/não).
- Cabeçalho com versão de cada assembly e data da extração.

As saídas geradas ficam fora do controle de versão até decidirmos se devem ser publicadas, porque derivam de arquivos da Autodesk.

### Critério de pronto
- Executa sem erros nas assemblies 31.0.260.0.
- Classes centrais (`FilerObject`, `AtomicElement`, `Beam`, `PlateBase`, `ScrewBoltPattern`, `AnchorPattern`, `WeldPattern`, `SpecialPart`, `UserAutoConstructionObject`, `IJoint`, `IFiler`) aparecem com herança completa.
- Resultado conferido por amostragem contra o `advsteel_api_help` do MCP, quando ele estiver disponível.

## 3. Diagnóstico de modelo (dentro do CAD)

### Requisitos
1. **Somente leitura.** Nenhuma escrita no modelo, nenhuma transação de modificação, nenhum comando de numeração.
2. **Sem execução de código arbitrário.** O comando tem lógica fixa; não aceita scripts nem trechos de C#.
3. **Thread principal.** Executar como comando registrado do AutoCAD/Advance Steel, no contexto de documento exigido pela API.
4. **Sem whitelist.** Para cada objeto, enumerar os getters públicos sem parâmetros obtidos do inventário e chamar também os métodos específicos conhecidos (User Attributes, contornos, features).
5. **Estados explícitos.** Cada leitura registra `Read`, `Empty`, `Null`, `NotApplicable`, `Unavailable` ou `ReadError` com a mensagem da exceção, conforme [NEUTRAL-MODEL.md](NEUTRAL-MODEL.md).
6. **Valores brutos.** Sem arredondamento e sem conversão de unidade; tipo CLR original registrado.
7. **Proteção contra efeitos colaterais.** Getters com efeito colateral conhecido ou suspeito (por exemplo `ExportToACIS`, `CreateACIS`) ficam fora da enumeração automática e só entram por lista explícita revisada.

### Fluxo
1. Usuário abre uma cópia do DWG de teste e executa o comando.
2. Enumerar todos os objetos do modelo, sem filtro de categoria.
3. Para cada objeto: classe real, cadeia de herança, handle e leituras.
4. User Attributes: varrer os índices documentados e registrar todos, inclusive vazios e erros.
5. Ligações: registrar drivers/drivens e as propriedades públicas do objeto de ligação; leitura de `IFiler` só se a investigação comprovar um caminho documentado e seguro.
6. Gravar JSON estruturado e relatório de cobertura em pasta escolhida pelo usuário.

### Relatório de cobertura
- Objetos por classe.
- Por classe: membros descobertos, lidos com sucesso, vazios, com erro e não aplicáveis.
- Comparação com o que o MCP devolve para os mesmos handles, quando disponível.

## 4. Preparação para o teste real

1. Primeiro teste com os DWGs de exemplo do SDK (`Portal.dwg`, `CreatePlate.dwg`), que não são de cliente.
2. Depois, o DWG de teste fornecido pelo desenvolvedor, com vigas, chapas, parafusos, chumbadores, soldas, peças especiais e ligações automáticas.
3. Para cada categoria, comparar manualmente uma amostra com o painel de propriedades do Advance Steel.
4. Nenhum resultado será registrado como validado sem o JSON gerado e a comparação documentada.

## 5. Decisões pendentes

- Pasta e nome dos projetos (`tools/ApiInventory`, `src/...`).
- Instalação do AdvancedSteelMCP neste computador para comparação lado a lado.
- Formato de carregamento do add-in de diagnóstico (bundle `ApplicationPlugins` ou `NETLOAD` manual).
