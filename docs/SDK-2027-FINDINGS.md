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

## 7. Pendências

- Inventário completo de tipos, herança, propriedades e métodos públicos por leitura de metadados.
- Confirmar se `IFiler`/`GetJointParam` permitem leitura externa dos parâmetros de uma ligação existente.
- Confirmar quantos User Attributes existem em 2027 e como obter seus nomes.
- Abrir o `COM API Reference Guide.doc` (formato .doc binário) e cruzar com a API .NET.
