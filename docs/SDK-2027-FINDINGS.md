# Achados do SDK e das assemblies do Advance Steel 2027

Status: investigação documental e estática. Nenhum código foi executado dentro do Advance Steel e nenhuma leitura de modelo real foi feita.

Data: 2026-11-19

## 1. Ambiente verificado

| Item | Valor observado | Evidência |
|---|---|---|
| Produto | Advance Steel 2027 31.0.260.0 (inclui Hotfix 2027.0.1) | Registro de desinstalação do Windows |
| Pasta das assemblies | `C:\Program Files\Autodesk\AutoCAD 2027\ADVS\` | Listagem de arquivos |
| .NET para compilação | .NET SDK 10.0.404 | `dotnet --list-sdks` |

## 2. Pacote SDK oficial

- Fonte: artigo Autodesk "Advance Steel 2027 Software Development Kit", publicado em 2026-04-02.
  https://www.autodesk.com/support/technical/article/caas/tsarticles/ts/4zenYDjCdtwInyFwn2YbbH.html
- Arquivo: `advance-steel-2027-sdk.zip`, 4,29 MB, zip comum (assinatura `PK`), sem executáveis.
- SHA-256: `419FDDCF66B20E43BCCC2C2B7504D197D3AA229C40A852EB82C0C60CF1F476CD`

Conteúdo:

| Tipo | Arquivos |
|---|---|
| API COM | `Advance Steel COM API Developer Training Guide.docx`, `Advance Steel COM API Reference Guide.doc` |
| API .NET | `How to create an AS.NET Addin.docx`, `Joints NET.docx` |
| Ligações / regras | `Advance Steel joints technology and philosophy.docx`, `AstorRules set up for SteelConnection Project.docx`, `DatabasePatching.docx` |
| Exemplos C# | `Samples/Projects/AdvanceSteelSamples.sln` com ASDotNetSample, ASNetJoints, CreateBeams, SampleDesign, SampleJoint, StructuralElement |
| Modelos de exemplo | `Samples/Models/CreatePlate.dwg`, `Samples/Models/Portal.dwg` |
| Bancos de exemplo | `Samples/Databases/AstCrtlDb.mdf`, `AstorJointsCalculation.mdf`, `AstorRules.mdf` |

O SDK **não** contém `.chm`, `.pdf`, `.dll`, `.tlb` nem `.xml`. A referência .NET precisa ser obtida das assemblies instaladas.

## 3. Assemblies .NET instaladas

| Assembly | Versão do arquivo | Versão do assembly | XML de documentação |
|---|---|---|---|
| ASCADLinkMgd.dll | 31.0.260.0 | 0.0.0.0 | sim |
| ASExchangeMgd.dll | 31.0.260.0 | 1.0.9629.14645 | não |
| ASGeometryMgd.dll | 31.0.260.0 | 0.0.0.0 | sim |
| ASMgd.dll | 31.0.260.0 | 1.0.9629.15011 | sim |
| ASModelerMgd.dll | 31.0.260.0 | 0.0.0.0 | sim |
| ASObjectsMgd.dll | 31.0.260.0 | 1.0.9629.14956 | sim |
| ASProfilesMgd.dll | 31.0.260.0 | 0.0.0.0 | sim |
| ASSettingsMgd.dll | 31.0.260.0 | 1.0.9629.14646 | não |

Biblioteca de tipos COM: `AstorMain5.tlb` (mesma pasta).

## 4. Cobertura dos XML de documentação

Os arquivos `AS*Mgd.xml` somam 3.475 membros documentados, mas a maior parte descreve tipos do AutoCAD (por exemplo `AcString`, `AcDbSurface`). Para as classes centrais do Advance Steel a cobertura é mínima:

| Classe | Membros documentados no XML |
|---|---|
| `ConstructionTypes.AtomicElement` | 6 |
| `ConstructionTypes.UserAutoConstructionObject` | 11 |
| `CADAccess.FilerObject` | 1 (`markChanged`) |
| `Modelling.Beam`, `PlateBase`, `ScrewBoltPattern`, `WeldPattern`, `SpecialPart` | 0 |

Membros documentados relevantes:
- `AtomicElement.HolesUsedForNumbering`, `GetFeatures(bool)`, `GetModeler(eBodyContext)`, `GetModelerBodyList(eBodyContext, out ModelerBody[])`, `CreateACIS`, `ExportToACIS(string)`.
- `UserAutoConstructionObject.NSAModuleName`, `NSAModuleInternalName`, `NSAModuleStandard`, `NewRuleTypeRunName`, `ApprovalStatus`, `UseDetailedTorsors`, `UseLoadCasesIsAvailable`, `ExportIsAvailable`, `ImportIsAvailable`, `PresizeIsAvailable`, `CheckIsAvailable`.

**Conclusão:** os XML não servem como inventário. É necessário ler os metadados das assemblies.

## 5. Nomes presentes em `ASObjectsMgd.dll`

Busca textual no binário (não prova visibilidade pública nem assinatura):

- Presentes: `GetUserAttribute`, `SetUserAttribute`, `IFiler`, `GetItems`, `ReadItem`, `IJoint`, `InputObjects`, `CreatedObjects`, `GetFilerObjectByHandle`, `GetFeatures`.
- Nomes a investigar: `getUserAttributeUsedForNumbering`, `GetJointParam`, `GetOvrdJointParam`, `GetOvrdJointParamAttr`, `getFilerData`, `GetJointNSAModuleSpecificData`, `GetJointRelation`.
- Não encontrados: `GetProperty`, `GetPropertyNames`, `PropertyNames`. Não há indício de API genérica de "lista de propriedades" com esses nomes.

## 6. O que os guias do SDK afirmam

1. **User Attributes (COM):** o Training Guide mostra `beam.get_UserAttribute(0)` e `beam.set_UserAttribute(0, "value")`, com índice a partir de 0 (seção "Attributes and categories").
2. **IFiler:** documentado como persistência da própria ligação. Em `InField`, a regra lê seus dados com `pFiler.readItem("PlateThickness")`; em `OutField`, grava com `writeItem`. Os nomes dos itens são definidos por quem programou cada ligação. Não há, nos guias, leitura de `IFiler` a partir de um objeto qualquer.
3. **IJoint:** usado na criação de ligações (`SetInputObjects`, `CreatedObjects`, `CreateJointTransfer`, `CreateRole`).
4. **Atributos de objetos controlados por ligações:** desde o AS 2020 ficam acessíveis por padrão, salvo se a ligação definir `IJoint.DefaultAttributeStateAccessible = false` (Joints NET.docx e Training Guide).

## 7. Inventário de metadados (ferramenta `tools/ApiInventory`)

Executado em 2026-11-19 sobre as 8 assemblies `AS*Mgd.dll` (31.0.260.0). A ferramenta lê só os metadados; nenhuma assembly foi carregada ou executada.

Totais: 474 tipos públicos somando as 8 assemblies (473 nomes distintos: 263 classes, 125 enums, 53 structs, 18 interfaces, 14 delegates) e 5.548 membros públicos ou protegidos, dos quais só 536 aparecem no XML de documentação. `ASObjectsMgd.dll` concentra 265 tipos e 3.356 membros.

### 7.1 Herança confirmada

```text
FilerObject
└─ ConstructionElement
   ├─ ActiveConstructionElement
   │  ├─ AtomicElement
   │  │  ├─ MainAlias
   │  │  │  ├─ Beam ─ StraightBeam, PolyBeam, ...
   │  │  │  └─ PlateBase ─ Plate, ...
   │  │  ├─ BoltPattern ─ ScrewBoltPattern ─ AnchorPattern
   │  │  ├─ WeldPattern
   │  │  └─ SpecialPart
   │  └─ FeatureObject
   └─ PassiveConstructionObject
      └─ AutoConstructionObject ─ UserAutoConstructionObject (implementa IJoint)
```

### 7.2 Membros declarados nas classes centrais

Propriedades legíveis de instância (sem índice) e métodos declarados na própria classe, sem contar os herdados:

| Classe | Propriedades legíveis | Métodos | No XML |
|---|---|---|---|
| FilerObject | 4 | 25 | 1 |
| ConstructionElement | 12 | 23 | 0 |
| AtomicElement | 38 | 36 | 6 |
| MainAlias | 20 | 7 | 0 |
| Beam | 17 | 38 | 0 |
| PlateBase | 15 | 27 | 1 |
| BoltPattern | 8 | 11 | 0 |
| ScrewBoltPattern | 27 | 8 | 0 |
| AnchorPattern | 23 | 11 | 0 |
| WeldPattern | 17 | 19 | 0 |
| SpecialPart | 8 | 7 | 0 |
| PassiveConstructionObject | 8 | 7 | 0 |
| UserAutoConstructionObject | 17 | 33 | 11 |

Para comparação, o MCP lê cerca de 13 membros de `AtomicElement` (ver [API-MCP-COVERAGE-MATRIX.md](API-MCP-COVERAGE-MATRIX.md)).

### 7.3 Achados relevantes

1. **Ligações:** `UserAutoConstructionObject` implementa `IJoint`. Portanto `InputObjects` e `CreatedObjects` (somente leitura) existem em objetos de ligação do modelo, não apenas durante a criação.
2. **Parâmetros de ligação (candidato):** `UserAutoConstructionObject.Save()` e `IJoint.Save()` retornam `IFiler`, e `IFiler.GetItems()` retorna `Dictionary<string, object>`. Pode ser o caminho para ler todos os parâmetros de uma ligação. **Não validado:** a semântica de `Save()` (efeitos colaterais, transação exigida) é desconhecida e precisa de teste controlado em cópia do DWG.
3. **Identidade:** `FilerObject.GetUniqueId()` retorna `System.Guid`; `ConstructionElement.SetUniqueId(Guid)` também existe. É candidato a chave estável para o `IfcGlobalId`, a confirmar se persiste entre sessões e cópias.
4. **XData:** `FilerObject.getXData(string)` retorna `byte[]`, mas exige conhecer o nome do registro.
5. **User Attributes:** `GetUserAttribute(int)` e `GetUserAttributeUsedForNumbering(int)` são públicos. Não há membro que informe a quantidade ou os nomes, mas o enum `Modelling.eObjectsAttribute` define exatamente `kUserAttr1` a `kUserAttr10`, o que indica 10 atributos. A quantidade e os rótulos exibidos ainda serão confirmados em execução.
6. **AtomicElement além do MCP:** `MaterialDescription`, `CoatingDescription`, `SpecificGravity`, `NumberOfHoles`, `ExplicitQuantity`, `StructuralMember`, `PreliminaryPartNumber`, `PreliminaryPartPrefix`, `PreliminaryPartPositionNumber`, `SinglePartNumber`, `MainPartNumber`, `IsAttachedPart`, `Unwind`, `UnwindStartFactor`, as flags `*UsedForNumbering`, `*UsedForBOM` e `*UsedForCollisionCheck`, e os métodos `GetBalancepoint`, `GetQuantityInModel`, `GetNumberingStatus`, `GetDSTVName`, `GetExternalNames`, `GetConnection`, `GetConnectedObjects`.
7. **Nomes não públicos:** `GetJointParam`, `GetOvrdJointParam` e `getFilerData`, encontrados no binário, **não** aparecem entre os membros públicos. Não serão usados.

## 8. Pendências

- Confirmar se `IFiler`/`GetJointParam` permitem leitura externa dos parâmetros de uma ligação existente. Atualização: `GetJointParam` não é público; o caminho candidato é `Save()` + `GetItems()` (seção 7.3).
- Confirmar quantos User Attributes existem em 2027 e como obter seus nomes.
- Abrir o `COM API Reference Guide.doc` (formato .doc binário) e cruzar com a API .NET.
