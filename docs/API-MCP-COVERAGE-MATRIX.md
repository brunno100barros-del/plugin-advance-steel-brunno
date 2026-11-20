# Matriz preliminar de cobertura: API Advance Steel 2027 × AdvancedSteelMCP

Status: preliminar. Baseada em leitura estática do código do MCP e na documentação/assemblies do SDK 2027. Nenhuma linha foi validada em modelo real.

Data: 2026-11-19

## 1. Fontes

- **MCP:** https://github.com/Shame625/AdvancedSteelMCP, commit `b1a627e`. Arquivos `plugin/Core/Objects.cs`, `QueryHandlers.cs`, `Infrastructure.cs`, `ScriptHandlers.cs`, `Entry.cs`.
- **SDK:** ver [SDK-2027-FINDINGS.md](SDK-2027-FINDINGS.md).
- **XML:** `AS*Mgd.xml` da instalação local do Advance Steel 2027 (31.0.260.0).

## 2. Legenda

| Código | Significado |
|---|---|
| MCP | Já disponível no MCP (o MCP compila contra esse membro) |
| MCP parcial | Disponível no MCP com perda ou limitação documentada |
| API | Documentado na API oficial e ausente no MCP |
| INV | Necessita investigação adicional |
| N/A | Não acessível pelos meios identificados |

Uma linha "MCP" comprova que o membro existe na API, mas não que o valor retornado esteja correto ou completo.

## 3. Matriz

| Categoria | Classe/API | Propriedade | Método de acesso | Disp. | Observações / referência |
|---|---|---|---|---|---|
| Geral | FilerObject | Handle | `Handle` | MCP | `Objects.cs:185` |
| Geral | FilerObject | Tipo | `Type()` (`eObjectType`) | MCP | `Objects.cs:48` |
| Geral | FilerObject | Layer | `Layer` | MCP | `Objects.cs:186` |
| Geral | ConstructionElement | Role | `Role` | MCP | `Objects.cs:189` |
| Identificação | AtomicElement | Denotation, Material, Coating, Assembly | propriedades | MCP parcial | `Objects.cs:193-197`; `Blank()` converte vazio, `-` e `Not defined` em nulo (`Objects.cs:391`) |
| Identificação | AtomicElement | Note | `Note` | MCP | Só no detalhado (`Objects.cs:202`) |
| Numeração | AtomicElement | SP/MP number | `GetSinglePartPositionNumber()`, `GetMainPartPositionNumber()` | MCP parcial | `Objects.cs:198-199`; mesma perda de `Blank()` |
| Numeração | AtomicElement | Prefixos, ItemNumber, IsMainPart | propriedades | MCP | `Objects.cs:203-206` |
| Numeração | AtomicElement | HolesUsedForNumbering | propriedade | API | XML `ASObjectsMgd` |
| User Attributes | AtomicElement | Atributos 1 a 10 | `GetUserAttribute(i)` | MCP parcial | Só valores não vazios; exceções descartadas (`Objects.cs:343-357`). Índice a partir de 0 confirmado no Training Guide |
| User Attributes | AtomicElement | Uso na numeração | `getUserAttributeUsedForNumbering` | INV | Só o nome encontrado no binário |
| Geometria | AtomicElement | Volume | `Volume` | MCP | `Objects.cs:207` |
| Geometria | ConstructionElement | Centro, extensões | `CenterPoint`, `GeomExtents` | MCP | `Objects.cs:236-241` |
| Geometria | AtomicElement | Sólido (BRep) | `GetModeler`, `GetModelerBodyList` | API | XML `ASObjectsMgd`. `ExportToACIS(string)` grava arquivo em disco |
| Features | AtomicElement | Cortes e recortes | `GetFeatures(bool)` | MCP parcial | Só handle e tipo; parâmetros não lidos (`Objects.cs:209, 359-367`) |
| Viga | Beam | Perfil e tipo de seção | `ProfSectionName`, `ProfSectionType`, `ProfName` | MCP | `Objects.cs:248-249, 172` |
| Viga | Beam | Pontos inicial e final, comprimento | `GetPointAtStart()`, `GetPointAtEnd()`, `GetLength()` | MCP | `Objects.cs:250-252` |
| Viga | Beam | Peso, peso/m, área de pintura | `GetWeight(1)`, `GetWeightPerMeter()`, `GetPaintArea()` | MCP | `Objects.cs:253, 263-264`; peso em gramas convertido para kg |
| Viga | Beam | Comprimento de sistema, ângulo, eixo de referência, Z local | `SysLength`, `Angle`, `RefAxis`, `PhysCSStart` | MCP | `Objects.cs:260-269` |
| Viga | BentBeamBase | Raio | `SystemlineRadius` | MCP | `Objects.cs:254-255` |
| Viga | PolyBeam | Vértices | `GetPolyline(true)` | MCP | `Objects.cs:256-257` |
| Chapa | PlateBase | Espessura, dimensões, peso | `Thickness`, `GetPhysLengthAndWidth`, `GetWeight()` | MCP | `Objects.cs:274-280` |
| Chapa | PlateBase | Normal, área, portioning, retangular | `PlateNormal`, `GetArea()`, `Portioning`, `IsRectangular()` | MCP | `Objects.cs:282-285` |
| Chapa | PlateBase | Contornos | `GetBaseContourPolygon(i, out Point3d[])` | MCP parcial | Só o índice 0 (`Objects.cs:288`); contornos internos não verificados |
| Parafuso | ScrewBoltPattern | Norma, classe, Ø, comprimento, conjunto, quantidade, local | propriedades | MCP | `Objects.cs:295-301` |
| Parafuso | ScrewBoltPattern | Centro, normal, direção X, tolerância de furo, posições | propriedades e `GetMidpoints` | MCP | `Objects.cs:303-311` |
| Parafuso | Countable / CircleScrewBoltPattern | Nx, Ny, Dx, Dy / raio | propriedades | MCP | `Objects.cs:312-320` |
| Chumbador | AnchorPattern | Tipo de chumbador | `AnchorType` | MCP | `Objects.cs:321-322` |
| Solda | WeldPattern | Tipo e espessura (superior e inferior) | `GetWeldType`, `GetSeamThickness` | MCP | `Objects.cs:327-340` |
| Solda | WeldPattern | Comprimento, local, centro, fechada | `Length`, `AssemblyLocation`, `GetCenterPoint()`, `IsClosed` | MCP | `Objects.cs:329-333` |
| Peça especial | SpecialPart | Bloco, peso | `BlockName`, `Weight` | MCP | `Objects.cs:220-221`; peso usado sem conversão, ao contrário de vigas e chapas: unidade a confirmar |
| Ligação | PassiveConstructionObject | Objetos de entrada e criados | `Drivers`, `Drivens` | MCP | `Objects.cs:226-228` |
| Ligação | UserAutoConstructionObject | Nome da ligação | `PrimaryJointName`, `NSAModuleName` | MCP | `Objects.cs:229-230` |
| Ligação | UserAutoConstructionObject | Módulo NSA, norma, aprovação, nome de execução, flags | propriedades | API | XML `ASObjectsMgd` |
| Ligação | IJoint | InputObjects, CreatedObjects, DefaultAttributeStateAccessible | interface | INV | Documentado para criação (Joints NET.docx); leitura externa não comprovada |
| Parâmetros da ligação | IFiler | Itens gravados pela regra | `readItem(nome)` | INV | Persistência interna da regra (Training Guide, `InField`/`OutField`) |
| Parâmetros da ligação | (binário) | Parâmetros e sobrescritas | `GetJointParam`, `GetOvrdJointParam`, `getFilerData` | INV | Só nomes encontrados no binário |
| Concreto | Wall, Slab, FootingIsolated, ConcreteBeam | Tudo além de handle, tipo e layer | — | INV | O MCP só classifica (`Objects.cs:39, 54-55`) |
| Eixos e pontos | Grid, UserDefinedPoint | Tudo além de handle, tipo e layer | — | INV | `Objects.cs:42-43` |
| Descoberta | Todas | Membros públicos declarados | `advsteel_api_help` (reflexão em runtime) | MCP | `ScriptHandlers.cs:235-285`; só membros declarados no tipo, sem herdados |
| Demais | Todas | Membros públicos herdados e específicos | leitura de metadados | INV | XML insuficiente; ver [DIAGNOSTIC-TOOL-PLAN.md](DIAGNOSTIC-TOOL-PLAN.md) |

## 4. Perdas sistemáticas no MCP

1. `Json.Try` descarta exceções e valores nulos (`Infrastructure.cs:194-202`): erro de leitura e campo ausente ficam indistinguíveis.
2. `Blank()` converte texto vazio, `-` e `Not defined` em nulo (`Objects.cs:391`): viola a distinção exigida em [PROPERTY-PRESERVATION.md](PROPERTY-PRESERVATION.md).
3. Valores numéricos são arredondados por `Geo.R` (padrão de 3 casas; vetores com 6) antes de serializar, e valores não finitos (NaN, infinito) viram `0` (`Infrastructure.cs:184-188`): a precisão original não é preservada e um valor inválido fica indistinguível de zero.
4. User Attributes: 10 índices fixos, vazios omitidos, exceções descartadas.

## 5. Próximos passos

- Gerar o inventário completo das assemblies (ferramenta `tools/ApiInventory`, ver plano).
- Reclassificar as linhas INV com base nesse inventário.
- Validar as linhas MCP e API em modelo real.
