# Projeto do diagnóstico de propriedades (dentro do Advance Steel)

Status: projeto. Nada implementado ou executado. Complementa a seção 3 de [DIAGNOSTIC-TOOL-PLAN.md](DIAGNOSTIC-TOOL-PLAN.md).

Data: 2026-11-19

## 1. Objetivo

Medir, num DWG real, **o que a API do Advance Steel 2027 efetivamente devolve** para cada objeto, sem whitelist fixa de propriedades e sem alterar o modelo. O resultado alimenta a matriz [API-CLASS-MATRIX.md](API-CLASS-MATRIX.md) com o estado "executado" e serve de base para o modelo neutro.

Fora do escopo: exportar IFC, corrigir dados, numerar, criar ou alterar objetos.

## 2. Base técnica (com evidência)

| Decisão | Evidência |
|---|---|
| Add-in .NET 10, x64 | SDK, `How to create an AS.NET Addin.docx`; `Samples/Projects/AdvanceSteelSDKSamples.Common.props` (`net10.0-windows`, `x64`) |
| Referências `ASMgd`, `ASCADLinkMgd`, `ASGeometryMgd`, `ASObjectsMgd`, `ASModelerMgd`, `ASProfilesMgd`, `ASNetRuntime`, com `Private=False` | `Samples/Projects/ASDotNetSample/ASDotNetTest.csproj` |
| Abrir objetos com `DatabaseManager.Open(ObjectId)` | Exemplos do SDK (`TestClass.cs`, `BridgeGirder.cs`); MCP `Objects.cs:115` |
| Listar objetos com `DatabaseManager.GetModelObjectIds(out ObjectId[], ClassTypeFilter)` | Inventário de metadados; MCP `Objects.cs:111` |
| Acesso somente leitura com `new DocumentAccess(doc, bReadOnly: true)`, sem `Commit()` | Inventário (`DocumentAccess(Document, bool bReadOnly)`); MCP `Infrastructure.cs:224-229` |
| Descartar qualquer alteração acidental com `DocumentAccess.DiscardAllChanges()` | Inventário; MCP `Infrastructure.cs:241` |
| Execução na thread principal, dentro de um comando registrado | Guia do SDK (`CommandMethodAttribute`); MCP `Loader/MainThread.cs` |

`DatabaseManager.Open` aceita `eOpenMode` (`kNotOpened`, `kNormal`, `kToShadow`), mas **não há modo "somente leitura" por objeto**. A garantia de leitura vem de três barreiras combinadas: transação em modo leitura, nenhuma chamada de escrita (seção 5.4) e `DiscardAllChanges()` ao final.

## 3. Comandos

| Comando | O que faz |
|---|---|
| `PASB_DIAG_SEL` | Diagnostica só os objetos selecionados. É o comando dos primeiros testes |
| `PASB_DIAG_ALL` | Diagnostica todos os objetos do modelo, sem filtro de categoria |

Prefixo `PASB` = Plugin Advance Steel Brunno, para não colidir com comandos da Autodesk ou do MCP. Os dois comandos usam `CommandFlags.Modal` e não usam `Redraw`.

## 4. Fluxo de execução

```text
Comando digitado
  └─ trava o documento (lock)
     └─ DocumentAccess(doc, bReadOnly: true)
        ├─ obtém os ObjectIds (seleção ou GetModelObjectIds sem filtro)
        ├─ para cada objeto:
        │    ├─ DatabaseManager.Open(id)   (erro → registra e segue)
        │    ├─ identidade e classe
        │    ├─ leitura automática de getters (5.2)
        │    ├─ métodos aprovados por classe (5.3)
        │    └─ grava o registro do objeto (streaming em disco)
        ├─ DiscardAllChanges()
        └─ libera a transação, sem Commit
  └─ grava o resumo e o relatório de cobertura
  └─ mostra na linha de comando: total, erros e a pasta de saída
```

Um objeto com erro nunca interrompe os demais. Uma exceção fora do laço (por exemplo, sem desenho aberto) encerra com mensagem clara e sem arquivos parciais marcados como completos.

## 5. Regras de leitura

### 5.1 Identidade

Para cada objeto: `Handle`, `ASId`, `GetUniqueId()`, `Type()` (`eObjectType`), nome completo do tipo .NET em execução e a cadeia de herança obtida por reflexão.

### 5.2 Leitura automática de getters

- Reflexão em execução sobre o tipo real do objeto: todas as propriedades **públicas, de instância, legíveis e sem índice**, incluindo as herdadas.
- Cada getter é chamado isoladamente, dentro de `try/catch`.
- Nenhum setter é chamado.
- Exceção: uma lista de exclusão revisada, para getters com custo alto ou efeito colateral comprovado. Ela começa **vazia** e só cresce com evidência registrada.

### 5.3 Métodos aprovados

Métodos não entram por reflexão automática. Cada um é chamado por código explícito, a partir das linhas "sem parâmetros" e "só out" de [API-CLASS-MATRIX.md](API-CLASS-MATRIX.md), revisadas uma a uma. Lista inicial:

| Classe | Métodos | Argumentos usados |
|---|---|---|
| FilerObject | `GetUniqueId`, `GetObjectId` | — |
| AtomicElement | `GetSinglePartPositionNumber`, `GetMainPartPositionNumber`, `GetNumberingStatus`, `GetBalancepoint`, `GetDSTVName`, `GetName`, `GetExternalNames`, `GetQuantityInModel`, `GetConnection` | sobrecargas sem argumentos |
| AtomicElement | `GetFeatures` | `true` e `false`, registrados separadamente |
| AtomicElement | `GetUserAttribute`, `GetUserAttributeUsedForNumbering` | índices 0 a 9 (`kUserAttr1`..`kUserAttr10`), **todos registrados, inclusive vazios e erros** |
| ConstructionElement | `getAttributeStatus` | cada valor de `eObjectsAttribute` aplicável à classe |
| Beam | `GetLength`, `GetPaintArea`, `GetWeightPerMeter`, `GetPointAtStart`, `GetPointAtEnd`, `GetLengthOffset`, `GetSawInformation`, `GetProfType` | sobrecargas sem argumentos ou só `out` |
| Beam | `GetWeight` | código `1` (o mesmo do MCP); outros códigos só após investigação |
| PlateBase | `GetPhysLengthAndWidth`, `GetArea`, `GetWeight`, `GetCircumference`, `GetPaintArea`, `IsRectangular` | sobrecargas sem argumentos |
| PlateBase | `GetBaseContourPolygon` | `dZPos = 0` (como o MCP); outros valores a investigar |
| BoltPattern | `GetMidpoints`, `GetHoles` | — |
| ScrewBoltPattern / AnchorPattern | `GetWeight` | — |
| WeldPattern | `GetWeldType`, `GetSeamThickness`, `GetFaceShape`, `GetSeamExecuting` | `kUpper` e `kLower` |
| WeldPattern | `GetCenterPoint`, `IsCombiWelding`, `IsLowerWeldingChangeable` | — |
| FeatureObject | `GetOwner` | — |

Geometria sólida (`GetModeler`, `GetModelerBodyList`) fica para uma segunda fase, por volume de dados e custo. Na primeira fase, o diagnóstico registra apenas se a chamada com `eBodyContext.kNormal` retorna corpo, e quantos.

### 5.4 Proibido no diagnóstico

- Qualquer setter de propriedade.
- Métodos classificados como "escrita/efeito" na matriz: `Set*`, `Add*`, `Del*`, `Remove*`, `Move*`, `Change*`, `Connect`, `TransformBy`, `Update*`, `WriteToDb`, `Save*`, `Load*`, `Export*`, `Create*`, `Check*`, `Presize*`, `markChanged` e semelhantes.
- `ExportToACIS` e `CreateACIS` (gravam arquivo ou criam entidades).
- Execução de código vindo de fora (scripts, C# dinâmico, comandos de texto).
- `Commit()` da transação.

### 5.5 Parâmetros de ligações (`Save()` + `IFiler.GetItems()`)

É o caminho candidato para ler todos os parâmetros de uma ligação, mas **`Save()` está na lista proibida até prova em contrário**. Plano em duas etapas:

1. Teste isolado, num comando separado `PASB_DIAG_JOINT_FILER`, sobre **uma** ligação de um DWG de exemplo do SDK copiado, com verificação de `DBMOD` antes e depois e reabertura do arquivo sem salvar.
2. Só se o teste mostrar que não há alteração, a leitura entra no diagnóstico, atrás de uma opção explícita, desligada por padrão.

## 6. Serialização dos valores

| Tipo de origem | Como é registrado |
|---|---|
| `string` | Texto exato, sem `Trim`. `""` e `null` são estados diferentes |
| `bool`, inteiros | Valor exato |
| `double` | Valor exato com `"R"` (ida e volta sem perda), sem arredondamento. `NaN` e infinito registrados como texto, com estado `Read` |
| `enum` | Nome e valor inteiro |
| `Point3d`, `Vector3d` | `[x, y, z]` com precisão total |
| `Matrix3d` | 16 valores |
| `Plane`, `Extents`, `Polyline3d` | Estrutura com os pontos e vetores componentes |
| `ObjectId` | Handle do objeto referenciado (e erro, se não abrir) |
| `IEnumerable<ObjectId>` / `IEnumerable<FilerObject>` | Lista de handles |
| Outros tipos complexos | Nome do tipo + `ToString()`, com estado `ComplexNotSerialized`, para revisão |

Unidades: nenhuma conversão. O registro guarda o valor como a API devolve; a unidade é anotada só quando comprovada (por exemplo, peso de viga em gramas, conforme o MCP `Objects.cs:174-175`, ainda a confirmar).

## 7. Formato de saída

Pasta: `Documentos\PASB-Diagnostics\<data-hora>\`. O nome do DWG não entra no nome da pasta.

### 7.1 `objects.jsonl`

Um objeto por linha (JSON Lines), gravado à medida que é lido, para não estourar memória em modelos grandes.

```json
{
  "handle": "2A7F",
  "asId": 123456,
  "uniqueId": "a3c1...-...",
  "objectType": { "name": "kStraightBeam", "value": 0 },
  "clrType": "Autodesk.AdvanceSteel.Modelling.StraightBeam",
  "inheritance": ["StraightBeam", "Beam", "MainAlias", "AtomicElement", "ActiveConstructionElement", "ConstructionElement", "FilerObject"],
  "properties": [
    {
      "technicalName": "Material",
      "declaringType": "AtomicElement",
      "source": "getter",
      "dataType": "System.String",
      "status": "Read",
      "rawValue": "S355JR"
    },
    {
      "technicalName": "GetUserAttribute",
      "argument": 3,
      "declaringType": "AtomicElement",
      "source": "method",
      "dataType": "System.String",
      "status": "Empty",
      "rawValue": ""
    },
    {
      "technicalName": "HeatNumber",
      "declaringType": "MainAlias",
      "source": "getter",
      "dataType": "System.String",
      "status": "ReadError",
      "error": { "type": "System.InvalidOperationException", "message": "..." }
    }
  ]
}
```

Estados: `Read`, `Empty`, `Null`, `NotApplicable`, `Unavailable`, `ReadError` (iguais a [NEUTRAL-MODEL.md](NEUTRAL-MODEL.md)) e `ComplexNotSerialized`, exclusivo do diagnóstico.

### 7.2 `run.json`

Metadados da execução: versão do Advance Steel (`VersionInformation`), versão do diagnóstico, comando usado, data e hora, quantidade de objetos, unidades do desenho (`INSUNITS`), duração, se `DiscardAllChanges()` foi chamado, e um resumo de erros. Sem caminhos de usuário nem nome de cliente.

### 7.3 `coverage.csv`

Uma linha por classe × membro: quantos objetos tiveram `Read`, `Empty`, `Null`, `ReadError` e `ComplexNotSerialized`. É a base para atualizar a matriz com o estado "executado".

## 8. Estrutura do código

```text
src/
  Pasb.Diagnostics.Core/      # sem referências Autodesk
    PropertyRecord.cs         # modelo do registro e estados
    ValueSerializer.cs        # regras da seção 6 para tipos .NET comuns
    CoverageReport.cs
  Pasb.Diagnostics.AdvanceSteel/   # add-in; único projeto com referências Autodesk
    Commands.cs               # PASB_DIAG_SEL, PASB_DIAG_ALL
    ObjectReader.cs           # seções 5.1 a 5.3
    GeometrySerializer.cs     # Point3d, Matrix3d, Plane...
    ApprovedMethods.cs        # lista da seção 5.3, explícita
tests/
  Pasb.Diagnostics.Core.Tests/    # testes sem Advance Steel
```

Isso segue a regra de [ARCHITECTURE.md](../ARCHITECTURE.md): só o adapter Autodesk depende das assemblies da Autodesk.

## 9. Carregamento no Advance Steel

| Opção | Como | Prós | Contras |
|---|---|---|---|
| A. `NETLOAD` manual (recomendada para a fase de diagnóstico) | Digitar `NETLOAD` e escolher a DLL a cada sessão | Nada é instalado; nada fica permanente | Comandos registrados pelo mecanismo do AutoCAD; exige referenciar `accoremgd`/`acdbmgd`/`acmgd` (como faz o Loader do MCP) |
| B. `ThirdPartySettings` (oficial do SDK) | XML em `ADVS\ThirdPartySettings` | Mecanismo documentado pela Autodesk para add-ins do Advance Steel | Escreve em `Program Files` (exige administrador) e fica permanente |
| C. Bundle em `ApplicationPlugins` | Pasta `.bundle` em `%APPDATA%\Autodesk\ApplicationPlugins` | Sem administrador; usado pelo MCP e pelo SteelX | Carrega sempre que o AutoCAD abre |

A escolha é decisão do usuário antes da implementação.

## 10. Protocolo do primeiro teste

1. Copiar `Portal.dwg` do SDK para uma pasta de teste. Nunca usar o original nem um DWG de cliente.
2. Abrir a cópia no Advance Steel 2027 e anotar o valor de `DBMOD` (digitar `DBMOD` na linha de comando; `0` = sem alterações).
3. Carregar o add-in e selecionar uma viga, uma chapa e um parafuso.
4. Executar `PASB_DIAG_SEL`.
5. Conferir que `DBMOD` continua igual. Se mudou, parar e registrar: é falha de segurança do diagnóstico.
6. Fechar o desenho **sem salvar**.
7. Comparar o `objects.jsonl` com a janela de propriedades do Advance Steel para os três objetos.
8. Registrar o resultado em `docs/`, sem dados de cliente.

Só depois de `Portal.dwg` e `CreatePlate.dwg` passarem, o diagnóstico roda no DWG de teste do desenvolvedor.

## 11. Critérios de pronto

- Compila sem avisos contra as assemblies 31.0.260.0.
- `PASB_DIAG_SEL` roda nos DWGs de exemplo sem alterar `DBMOD`.
- Cada objeto selecionado aparece no `objects.jsonl` com identidade, todos os getters e os métodos aprovados, e cada leitura com um estado.
- Nenhum valor é arredondado ou convertido.
- `coverage.csv` gerado e conferido com o painel de propriedades em pelo menos um objeto por categoria.

## 12. Decisões pendentes

1. Opção de carregamento (seção 9).
2. Pasta de saída padrão (`Documentos\PASB-Diagnostics` ou outra).
3. Prefixo dos comandos (`PASB_`).
4. Quando fazer o teste isolado de `Save()` + `GetItems()` (seção 5.5).
