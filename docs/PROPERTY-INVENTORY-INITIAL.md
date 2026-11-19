# Inventário técnico inicial de propriedades — AdvanceSteelMCP

Status: análise estática do código-fonte público do AdvancedSteelMCP. Ainda não é inventário completo da API Advance Steel 2027 nem validação em modelo real.

Data da análise: 2026-11-19

## 1. Escopo e fontes examinadas

Repositório original: https://github.com/Shame625/AdvancedSteelMCP

Arquivos examinados:
- `plugin/Core/Objects.cs`
- `plugin/Core/QueryHandlers.cs`
- `plugin/Core/AdvanceSteelMcp.Core.csproj`
- `plugin/README.md`

Este documento descreve somente o que foi confirmado no código examinado. Não presume que os campos serializados sejam todas as propriedades disponíveis pela API.

## 2. Conclusão principal

O MCP atual não realiza descoberta dinâmica de propriedades. O método `Objects.Describe` constrói manualmente um JSON com campos explicitamente selecionados por tipo/classe. Portanto, a resposta detalhada atual é uma seleção curada de propriedades, não um dump integral da API.

A estratégia do nosso projeto deve ser:
1. preservar a resposta bruta atual do MCP;
2. inventariar as classes e membros disponíveis na API instalada do Advance Steel 2027;
3. combinar reflexão com getters documentados e mecanismos específicos, incluindo User Attributes e dados de ligações;
4. registrar os resultados de leitura sem omitir estados vazios, nulos ou erros;
5. ampliar nosso adaptador independente quando necessário, sem modificar o MCP original.

## 3. Categorias identificadas no MCP

O código declara as categorias:

| Categoria | Classe-base/identificador usado |
|---|---|
| beam | `kBeam` |
| plate | `kPlateBase` |
| bolts | `kBoltPattern` |
| anchors | `kAnchorPattern` |
| weld | `kWeldPattern` |
| special_part | `kSpecialPart` |
| connector | `kConnector` |
| concrete | paredes, lajes, sapatas e vigas de concreto |
| feature | `kFeatureObject` |
| joint | `kUserAutoConstructionObject`, `kUserDefinedConnectionInstance` |
| grid | `kGrid` |
| user_point | `kUserDefinedPoint` |

A listagem padrão usa somente categorias físicas: beam, plate, bolts, anchors, weld, special_part e connector. Categorias como feature e joint precisam ser solicitadas explicitamente para aparecerem na consulta.

## 4. Propriedades efetivamente serializadas

### 4.1 Campos gerais

- Handle
- Tipo
- Categoria
- Layer
- Role, quando o objeto é ConstructionElement

### 4.2 AtomicElement

O MCP serializa:
- Denotation/name
- Material
- Coating
- Assembly
- Single-part position number
- Main-part position number
- Note (detalhado)
- ItemNumber (detalhado)
- IsMainPart (detalhado)
- SinglePartPrefix (detalhado)
- MainPartPrefix (detalhado)
- Volume (detalhado)
- User Attributes (detalhado)
- Referências de features (detalhado)

### 4.3 Beam

- Profile section name
- Profile section type
- Start/end points
- Length
- Weight
- Para subtipos curvos: SystemlineRadius
- Detalhado: profile full name, system length, rotation angle, reference axis, weight per meter, paint area, local Z
- Para PolyBeam detalhada: vértices da polilinha

### 4.4 PlateBase

- Thickness
- Physical length/width
- Weight
- Detalhado: normal, area, portioning, rectangular flag, base contour polygon

### 4.5 ScrewBoltPattern / AnchorPattern

- Standard
- Grade
- Diameter
- Length
- Bolt assembly
- Number of screws
- Assembly location
- Detalhado: center, normal, X direction, hole tolerance, midpoint positions
- Countable pattern: Nx, Ny, Dx, Dy
- Circular pattern: radius
- Anchor pattern: anchor type

### 4.6 WeldPattern

- Seam thickness (upper)
- Weld type (upper)
- Length
- Assembly location
- Detalhado: center point, closed flag, lower seam type and thickness when defined

### 4.7 SpecialPart

- Block name
- Weight

### 4.8 Features e joints

- FeatureObject detalhado: runtime type name
- PassiveConstructionObject: drivers e driven objects
- UserAutoConstructionObject: primary joint name ou NSA module name

### 4.9 ConstructionElement detalhado

- Center point
- Geometric extents (min/max)

## 5. Lacunas e riscos confirmados no serializador atual

1. **Sem reflexão:** não há enumeração geral de propriedades públicas, herdadas ou específicas.
2. **Whitelist implícita:** os campos são definidos manualmente em `Describe`, `DescribeBeam`, `DescribePlate`, `DescribeBolts` e `DescribeWeld`.
3. **User Attributes incompletos para auditoria:** o laço percorre 10 índices, mas só adiciona ao JSON valores não vazios; exceções são ignoradas. Assim, não distingue atributo vazio, indisponível e erro.
4. **Tratamento de erros:** vários getters são encapsulados por `Json.Try` ou blocos `catch`; a resposta não fornece necessariamente um inventário de campos que falharam.
5. **Cobertura de classes:** há categorias adicionais (feature, joint, grid, user_point, concrete), mas nem todas têm descrição detalhada equivalente às peças metálicas.
6. **Geometria:** os dados serializados não demonstram, por si só, que todos os cortes, furos, recortes, chanfros e detalhes de fabricação possam ser reconstruídos.
7. **Ligações:** drivers/drivens e nome do joint não equivalem a uma captura integral dos parâmetros serializados da ligação.
8. **BOM não é inventário:** a lista de materiais agrega e seleciona campos; não deve ser usada como fonte de preservação integral.
9. **Dados de catálogo:** o projeto possui handlers de banco de dados, mas os resultados de catálogo não substituem a leitura das propriedades efetivas de cada objeto.

## 6. Evidência oficial relevante para investigação posterior

A documentação pública Autodesk consultada confirma a existência de:
- API .NET e COM documentadas no SDK Advance Steel 2027;
- `IFiler` com `ReadItem`, `GetItems`, `ReadVersion` e métodos de serialização, usado especialmente por joints;
- `IJoint` com `InputObjects`, `CreatedObjects`, `Save` e `Load`;
- mecanismos de User Attributes em `AtomicElement`.

Referências:
- Autodesk, Advance Steel 2027 SDK: https://www.autodesk.com/support/technical/article/caas/tsarticles/ts/4zenYDjCdtwInyFwn2YbbH.html
- Autodesk, Advance Steel .NET and COM API: https://help.autodesk.com/cloudhelp/2022/ESP/AdvSteel-Developers/files/GUID-AF2072A9-3F8F-4DAC-B595-470A9E32BEE0.htm
- Autodesk COM API Developer Training Guide (exemplo de User Attributes): https://forums.autodesk.com/autodesk/attachments/autodesk/vba-forum-en/2525/1/Advance%20Steel%20COM%20API%20Developer%20Training%20Guide.pdf

A documentação consultada não basta para afirmar que `IFiler.GetItems()` pode ser chamado indiscriminadamente em todos os objetos ou que todos os atributos de conexão são acessíveis. Isso deverá ser confirmado no SDK 2027 e em testes controlados.

## 7. Próxima atividade técnica

### A. Inspeção local do SDK 2027

Identificar no pacote extraído:
- índice/arquivo de referência da API .NET;
- referência da API COM;
- Developer Guide / Training Guide;
- exemplos C# de leitura de objetos;
- documentação de FilerObject, AtomicElement, Beam, PlateBase, ScrewBoltPattern, AnchorPattern, WeldPattern, SpecialPart, FeatureObject, IJoint e IFiler;
- documentação e exemplos de User Attributes.

Registrar nome e versão dos assemblies efetivamente instalados no Advance Steel 2027. Não assumir que a documentação de versões anteriores é idêntica.

### B. Matriz API × MCP

Para cada classe e propriedade:
- classe declaradora;
- propriedade/método;
- tipo CLR;
- getter ou forma de acesso;
- documentação que comprova;
- presença ou ausência no JSON do MCP;
- comportamento de erro;
- estado de validação (documentado, compilado, executado).

### C. Diagnóstico de leitura

Depois da inspeção, projetar um comando de diagnóstico independente, somente leitura, com:
- enumeração de tipos e propriedades;
- valores tipados;
- User Attributes com todos os índices e estados;
- inventário de dados de joints com uso seguro/documentado de IFiler;
- erros individualizados;
- exportação JSON;
- execução no contexto/thread exigidos pelo Advance Steel.

## 8. Critério de conclusão desta investigação

Esta análise estática inicial está concluída, mas o inventário completo NÃO está concluído.

A etapa só poderá ser considerada concluída após:
- leitura do SDK específico 2027;
- matriz API × MCP baseada em documentação;
- diagnóstico executado no Advance Steel 2027;
- amostras reais de cada categoria;
- comparação de propriedades no painel do Advance Steel com os dados extraídos.

Nenhuma implementação do exportador IFC deve começar antes dessa validação.
