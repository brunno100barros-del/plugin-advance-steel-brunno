# Matriz detalhada por classe: API Advance Steel 2027 × AdvancedSteelMCP

> Arquivo gerado por `tools/ApiInventory/New-ClassMatrix.ps1`. Não edite à mão: rode o script de novo.

Fonte da API: inventário de metadados das assemblies 31.0.260.0. Fonte do MCP: AdvancedSteelMCP commit `b1a627e`, `plugin/Core/Objects.cs`. Gerado em 2026-11-19.

Nenhuma linha foi validada em modelo real. "Sim" na coluna MCP só significa que o MCP chama esse membro, não que o valor esteja correto ou completo.

## Legenda da coluna "Leitura"

| Valor | Significado |
|---|---|
| getter | Propriedade legível sem argumentos: entra na enumeração automática do diagnóstico |
| sem parâmetros / só out | Método de leitura chamável sem escolher argumentos: candidato à enumeração automática, após revisão |
| com parâmetros | Exige escolher argumentos (índice, contexto, nível): precisa de regra específica |
| indexada | Propriedade com índice |
| escrita/efeito | Pelo nome, altera o modelo, grava arquivo ou dispara cálculo: excluído do diagnóstico |
| infraestrutura | Métodos .NET (`Equals`, `Dispose`...) e de visitante: não são dados do objeto |
| ponteiro nativo | Retorna ponteiro C++ interno: excluído |

A classificação é heurística, feita pelo nome e pela assinatura. Todo método candidato será revisado antes de entrar no diagnóstico.
| só escrita | Propriedade sem getter |

## Resumo

| Classe | Getters | Lidos pelo MCP | Métodos de leitura sem argumentos | Métodos com parâmetros | Excluídos (escrita/efeito) |
|---|---|---|---|---|---|
| [FilerObject](#filerobject) | 4 | 3 | 3 | 2 | 6 |
| [ConstructionElement](#constructionelement) | 12 | 3 | 6 | 3 | 11 |
| [ActiveConstructionElement](#activeconstructionelement) | 2 | 0 | 1 | 2 | 1 |
| [AtomicElement](#atomicelement) | 38 | 14 | 11 | 10 | 8 |
| [MainAlias](#mainalias) | 20 | 0 | 2 | 1 | 0 |
| [Beam](#beam) | 17 | 13 | 15 | 6 | 8 |
| [PlateBase](#platebase) | 15 | 8 | 10 | 6 | 6 |
| [BoltPattern](#boltpattern) | 8 | 5 | 3 | 2 | 4 |
| [ScrewBoltPattern](#screwboltpattern) | 27 | 7 | 2 | 2 | 2 |
| [AnchorPattern](#anchorpattern) | 23 | 1 | 3 | 1 | 3 |
| [WeldPattern](#weldpattern) | 17 | 6 | 4 | 5 | 8 |
| [SpecialPart](#specialpart) | 8 | 2 | 1 | 1 | 2 |
| [FeatureObject](#featureobject) | 5 | 0 | 2 | 1 | 1 |
| [PassiveConstructionObject](#passiveconstructionobject) | 8 | 2 | 1 | 2 | 2 |
| [UserAutoConstructionObject](#userautoconstructionobject) | 17 | 2 | 4 | 7 | 19 |

Os números contam só membros **declarados na própria classe**; os herdados aparecem na seção da classe-base.

## FilerObject

`Autodesk.AdvanceSteel.CADAccess.FilerObject`

| Membro | Tipo | Assinatura | Leitura | Gravável | MCP |
|---|---|---|---|---|---|
| `Accept` | método | `void Accept(Modelling.IVisitor visitor)` | infraestrutura |  |  |
| `DelFromDb` | método | `void DelFromDb()` | escrita/efeito |  |  |
| `Dispose` | método | `void Dispose()` | infraestrutura |  |  |
| `Equals` | método | `bool Equals(CADAccess.FilerObject obj)`<br>`bool Equals(object obj)` | infraestrutura |  |  |
| `GetFilerObject` | método | `ASObjectsAPI.FilerObject* GetFilerObject()` | ponteiro nativo |  |  |
| `GetHashCode` | método | `int GetHashCode()` | infraestrutura |  |  |
| `GetObjectId` | método | `ObjectId GetObjectId()` | sem parâmetros |  |  |
| `GetUniqueId` | método | `System.Guid GetUniqueId()` | sem parâmetros |  |  |
| `getXData` | método | `byte[] getXData(string xdataName)` | com parâmetros |  |  |
| `IsKindOf` | método | `bool IsKindOf(CADAccess.FilerObject+eObjectType objType)` | com parâmetros |  |  |
| `markChanged` | método | `void markChanged(bool bPropagateChanges)` | escrita/efeito |  |  |
| `removeXData` | método | `void removeXData(string xdataName)` | escrita/efeito |  |  |
| `SetLayer` | método | `int SetLayer(ObjectId newVal, bool doSubents)`<br>`int SetLayer(string strNewVal, bool doSubents)` | escrita/efeito |  |  |
| `setXData` | método | `void setXData(string xdataName, byte[] data)` | escrita/efeito |  |  |
| `Type` | método | `CADAccess.FilerObject+eObjectType Type()` | sem parâmetros |  | Sim |
| `WriteToDb` | método | `ObjectId WriteToDb()` | escrita/efeito |  |  |
| `ASId` | propriedade | `long` | getter |  |  |
| `Handle` | propriedade | `string` | getter |  | Sim |
| `Layer` | propriedade | `string` | getter | sim | Sim |
| `SkipEraseEnvironment` | propriedade | `bool` | getter | sim |  |

## ConstructionElement

`Autodesk.AdvanceSteel.ConstructionTypes.ConstructionElement`

| Membro | Tipo | Assinatura | Leitura | Gravável | MCP |
|---|---|---|---|---|---|
| `Accept` | método | `void Accept(Modelling.IVisitor visitor)` | infraestrutura |  |  |
| `AdjustGeometryToWorkingPlanes` | método | `int AdjustGeometryToWorkingPlanes()`<br>`int AdjustGeometryToWorkingPlanes(bool bUnlinkIfInvalidGeometry)` | escrita/efeito |  |  |
| `DelReference` | método | `void DelReference()` | escrita/efeito |  |  |
| `EmptyWorkingPlaneLinkInfo` | método | `int EmptyWorkingPlaneLinkInfo()` | escrita/efeito |  |  |
| `getAttributeStatus` | método | `int getAttributeStatus(Modelling.eObjectsAttribute attribute)` | com parâmetros |  |  |
| `GetLevel` | método | `BuildingStructure.LevelObject GetLevel()` | sem parâmetros |  |  |
| `GetNextVisibleReprMode` | método | `int GetNextVisibleReprMode(int nCurrentMode)` | com parâmetros |  |  |
| `GetNumberOfGeometryIDs` | método | `int GetNumberOfGeometryIDs()` | sem parâmetros |  |  |
| `GetWorkingPlanesRelations` | método | `int GetWorkingPlanesRelations(IEnumerable<ObjectId>& out workingPlanes, IEnumerable<int>& out geomIDs)` | só out |  |  |
| `IsKindOf` | método | `bool IsKindOf(CADAccess.FilerObject+eObjectType objType)` | com parâmetros |  |  |
| `IsPartialCOInWorkingDb` | método | `bool IsPartialCOInWorkingDb()` | sem parâmetros |  |  |
| `IsReadOnlyInWorkingDb` | método | `bool IsReadOnlyInWorkingDb()` | sem parâmetros |  |  |
| `RemWorkingPlaneLink` | método | `int RemWorkingPlaneLink(BuildingStructure.WorkingPlane pWP)` | escrita/efeito |  |  |
| `RemWorkingPlaneLinkInfo` | método | `int RemWorkingPlaneLinkInfo(int nGeomId)` | escrita/efeito |  |  |
| `SetLevel` | método | `void SetLevel(BuildingStructure.LevelObject pLevel)` | escrita/efeito |  |  |
| `SetNextVisibleReprMode` | método | `void SetNextVisibleReprMode()` | escrita/efeito |  |  |
| `SetUniqueId` | método | `void SetUniqueId(System.Guid id)` | escrita/efeito |  |  |
| `SetWorkingPlaneLinkInfo` | método | `int SetWorkingPlaneLinkInfo(int nGeomId, BuildingStructure.WorkingPlane pWP)` | escrita/efeito |  |  |
| `TransformBy` | método | `void TransformBy(Matrix3d mat)` | escrita/efeito |  |  |
| `Type` | método | `CADAccess.FilerObject+eObjectType Type()` | sem parâmetros |  |  |
| `UpdateDrivenConstruction` | método | `void UpdateDrivenConstruction()` | escrita/efeito |  |  |
| `CenterPoint` | propriedade | `Point3d` | getter |  | Sim |
| `CompoundReprMode` | propriedade | `int` | getter | sim |  |
| `DrivingPassiveConstructionObject` | propriedade | `ConstructionTypes.PassiveConstructionObject` | getter | sim |  |
| `GeomExtents` | propriedade | `Extents` | getter |  | Sim |
| `HasDrivingPassiveConstructionObject` | propriedade | `bool` | getter |  |  |
| `IsGeometryNotEditable` | propriedade | `bool` | getter |  |  |
| `JointTransferID` | propriedade | `string` | getter | sim |  |
| `NumberOfReprModes` | propriedade | `int` | getter |  |  |
| `PureRole` | propriedade | `string` | getter |  |  |
| `ReprMode` | propriedade | `int` | getter | sim |  |
| `Role` | propriedade | `string` | getter | sim | Sim |
| `RoleDescription` | propriedade | `string` | getter |  |  |

## ActiveConstructionElement

`Autodesk.AdvanceSteel.ConstructionTypes.ActiveConstructionElement`

| Membro | Tipo | Assinatura | Leitura | Gravável | MCP |
|---|---|---|---|---|---|
| `Accept` | método | `void Accept(Modelling.IVisitor visitor)` | infraestrutura |  |  |
| `AddDrivenConObj` | método | `void AddDrivenConObj(ConstructionTypes.PassiveConstructionObject pConObj)` | escrita/efeito |  |  |
| `GetDrivenConObj` | método | `ConstructionTypes.PassiveConstructionObject GetDrivenConObj(int n)` | com parâmetros |  |  |
| `IsKindOf` | método | `bool IsKindOf(CADAccess.FilerObject+eObjectType objType)` | com parâmetros |  |  |
| `Type` | método | `CADAccess.FilerObject+eObjectType Type()` | sem parâmetros |  |  |
| `CS` | propriedade | `Matrix3d` | getter |  |  |
| `NumberOfDrivenConObj` | propriedade | `int` | getter |  |  |

## AtomicElement

`Autodesk.AdvanceSteel.ConstructionTypes.AtomicElement`

| Membro | Tipo | Assinatura | Leitura | Gravável | MCP |
|---|---|---|---|---|---|
| `Accept` | método | `void Accept(Modelling.IVisitor visitor)` | infraestrutura |  |  |
| `AddFeature` | método | `int AddFeature(ConstructionTypes.FeatureObject pFeat)` | escrita/efeito |  |  |
| `CreateACIS` | método | `IEnumerable<ObjectId> CreateACIS()` | escrita/efeito |  |  |
| `DelAllFeatures` | método | `int DelAllFeatures()` | escrita/efeito |  |  |
| `DelFeature` | método | `int DelFeature(ConstructionTypes.FeatureObject pFeat)` | escrita/efeito |  |  |
| `ExportToACIS` | método | `int ExportToACIS(string fileName)` | escrita/efeito |  |  |
| `GetBalancepoint` | método | `bool GetBalancepoint(Point3d& out ptCenter, double& out dWeight)` | só out |  |  |
| `GetConnectedElements` | método | `void GetConnectedElements(IEnumerable<ObjectId>& out connElements, int nAddAdditionalObjectsForStructure)` | com parâmetros |  |  |
| `GetConnectedObjects` | método | `void GetConnectedObjects(IEnumerable<ObjectId>& out elements, ConstructionTypes.AtomicElement+eAssemblyLocation nLevel)` | com parâmetros |  |  |
| `GetConnection` | método | `Connection.Connection GetConnection()` | sem parâmetros |  |  |
| `GetDSTVName` | método | `string GetDSTVName(int& out alternativSearched)` | só out |  |  |
| `GetExternalName` | método | `string GetExternalName(int num, int& out found)` | com parâmetros |  |  |
| `GetExternalNames` | método | `int GetExternalNames(IEnumerable<string>& out externalNames, IEnumerable<int>& out found, IEnumerable<int>& out alternativeSearching)` | só out |  |  |
| `GetFeatures` | método | `IEnumerable<ObjectId> GetFeatures(bool bIncludeParentsFeatures)` | com parâmetros |  | Sim |
| `GetMainPartPositionNumber` | método | `string GetMainPartPositionNumber()`<br>`string GetMainPartPositionNumber(ConstructionTypes.AtomicElement+eAssemblyLocation level)`<br>`string GetMainPartPositionNumber(ConstructionTypes.AtomicElement+eAssemblyLocation level, string strLocation)` | com parâmetros, sem parâmetros |  | Sim |
| `GetModeler` | método | `Modeler.ModelerBody GetModeler(Modeler.BodyContext+eBodyContext bodyCtx)` | com parâmetros |  |  |
| `GetModelerBodyList` | método | `bool GetModelerBodyList(Modeler.BodyContext+eBodyContext bodyCtx, Modeler.ModelerBody[]& out bodies)` | com parâmetros |  |  |
| `GetName` | método | `string GetName(int& out alternativSearched)` | só out |  |  |
| `GetNumberingStatus` | método | `void GetNumberingStatus(bool& out hasValidSPNumber, bool& out hasValidMPNumber)` | só out |  |  |
| `GetQuantityInModel` | método | `int GetQuantityInModel()`<br>`int GetQuantityInModel(ConstructionTypes.AtomicElement+eSingpartState singlePartState)`<br>`int GetQuantityInModel(ConstructionTypes.AtomicElement+eSingpartState singlePartState, ConstructionTypes.AtomicElement+eAssemblyLocation level)`<br>`int GetQuantityInModel(ConstructionTypes.AtomicElement+eSingpartState singlePartState, ConstructionTypes.AtomicElement+eAssemblyLocation level, string location)` | com parâmetros, sem parâmetros |  |  |
| `GetSinglePartPositionNumber` | método | `string GetSinglePartPositionNumber()` | sem parâmetros |  | Sim |
| `GetUserAttribute` | método | `string GetUserAttribute(int idx)` | com parâmetros |  | Sim |
| `GetUserAttributeUsedForNumbering` | método | `int GetUserAttributeUsedForNumbering(int attributeIdx)` | com parâmetros |  |  |
| `HasFeature` | método | `int HasFeature(ConstructionTypes.FeatureObject feat)` | com parâmetros |  |  |
| `IsKindOf` | método | `bool IsKindOf(CADAccess.FilerObject+eObjectType objType)` | com parâmetros |  |  |
| `NumFeatures` | método | `short NumFeatures()` | sem parâmetros |  |  |
| `SetUserAttribute` | método | `void SetUserAttribute(int idx, string strNewVal)` | escrita/efeito |  |  |
| `SetUserAttributeUsedForNumbering` | método | `void SetUserAttributeUsedForNumbering(int attributeIdx, int val)` | escrita/efeito |  |  |
| `Type` | método | `CADAccess.FilerObject+eObjectType Type()` | sem parâmetros |  |  |
| `UpdateYourselfAsConnectionMeans` | método | `bool UpdateYourselfAsConnectionMeans()` | escrita/efeito |  |  |
| `Assembly` | propriedade | `string` | getter | sim | Sim |
| `AssemblyUsedForNumbering` | propriedade | `int` | getter | sim |  |
| `Coating` | propriedade | `string` | getter | sim | Sim |
| `CoatingDescription` | propriedade | `string` | getter |  |  |
| `CoatingUsedForNumbering` | propriedade | `int` | getter | sim |  |
| `DennotationUsedForNumbering` | propriedade | `int` | getter | sim |  |
| `Denotation` | propriedade | `string` | getter | sim | Sim |
| `ExplicitQuantity` | propriedade | `int` | getter | sim |  |
| `HolesUsedForNumbering` | propriedade | `int` | getter | sim |  |
| `IsAttachedPart` | propriedade | `bool` | getter |  |  |
| `IsMainPart` | propriedade | `bool` | getter | sim | Sim |
| `ItemNumber` | propriedade | `string` | getter | sim | Sim |
| `ItemNumberUsedForNumbering` | propriedade | `int` | getter | sim |  |
| `MainPartNumber` | propriedade | `string` | getter | sim |  |
| `MainPartPrefix` | propriedade | `string` | getter | sim | Sim |
| `MainPartUsedForBOM` | propriedade | `int` | getter | sim |  |
| `MainPartUsedForCollisionCheck` | propriedade | `int` | getter | sim |  |
| `MainPartUsedForNumbering` | propriedade | `int` | getter | sim |  |
| `Material` | propriedade | `string` | getter | sim | Sim |
| `MaterialDescription` | propriedade | `string` | getter |  |  |
| `MaterialUsedForNumbering` | propriedade | `int` | getter | sim |  |
| `Note` | propriedade | `string` | getter | sim | Sim |
| `NoteUsedForNumbering` | propriedade | `int` | getter | sim |  |
| `NumberOfHoles` | propriedade | `int` | getter |  |  |
| `PreliminaryPartNumber` | propriedade | `string` | getter | sim |  |
| `PreliminaryPartPositionNumber` | propriedade | `string` | getter |  |  |
| `PreliminaryPartPrefix` | propriedade | `string` | getter | sim |  |
| `RoleUsedForNumbering` | propriedade | `int` | getter | sim |  |
| `SinglePartNumber` | propriedade | `string` | getter | sim |  |
| `SinglePartPrefix` | propriedade | `string` | getter | sim | Sim |
| `SinglePartUsedForBOM` | propriedade | `int` | getter | sim |  |
| `SinglePartUsedForCollisionCheck` | propriedade | `int` | getter | sim |  |
| `SinglePartUsedForNumbering` | propriedade | `int` | getter | sim |  |
| `SpecificGravity` | propriedade | `double` | getter |  |  |
| `StructuralMember` | propriedade | `int` | getter | sim |  |
| `Unwind` | propriedade | `bool` | getter | sim |  |
| `UnwindStartFactor` | propriedade | `double` | getter | sim |  |
| `Volume` | propriedade | `double` | getter |  | Sim |

## MainAlias

`Autodesk.AdvanceSteel.ConstructionTypes.MainAlias`

| Membro | Tipo | Assinatura | Leitura | Gravável | MCP |
|---|---|---|---|---|---|
| `Accept` | método | `void Accept(Modelling.IVisitor visitor)` | infraestrutura |  |  |
| `GetStandardWeight` | método | `double GetStandardWeight()`<br>`double GetStandardWeight(int calcMethod)`<br>`double GetStandardWeight(int calcMethod, double dDensity)` | com parâmetros, sem parâmetros |  |  |
| `IsKindOf` | método | `bool IsKindOf(CADAccess.FilerObject+eObjectType objType)` | com parâmetros |  |  |
| `Type` | método | `CADAccess.FilerObject+eObjectType Type()` | sem parâmetros |  |  |
| `ApprovalComment` | propriedade | `string` | getter | sim |  |
| `ApprovalStatus` | propriedade | `ConstructionTypes.ConstructionElement+eApprovalStatus` | getter | sim |  |
| `ApprovalStatusCode` | propriedade | `string` | getter | sim |  |
| `Carrier` | propriedade | `string` | getter | sim |  |
| `DeliveryDate` | propriedade | `string` | getter | sim |  |
| `FabricationStation` | propriedade | `string` | getter | sim |  |
| `FabricationStationUsedForNumbering` | propriedade | `bool` | getter | sim |  |
| `GalvanizingConfidence` | propriedade | `ConstructionTypes.MainAlias+eGalvanizingConfidence` | getter | sim |  |
| `GalvanizingConstructionClass` | propriedade | `ConstructionTypes.MainAlias+eGalvanizingConstructionClass` | getter | sim |  |
| `GalvanizingDetailClass` | propriedade | `ConstructionTypes.MainAlias+eGalvanizingDetailClass` | getter | sim |  |
| `HeatNumber` | propriedade | `string` | getter | sim |  |
| `HeatNumberUsedForNumbering` | propriedade | `bool` | getter | sim |  |
| `LoadNumber` | propriedade | `string` | getter | sim |  |
| `PONumber` | propriedade | `string` | getter | sim |  |
| `PONumberUsedForNumbering` | propriedade | `bool` | getter | sim |  |
| `RequisitionNumber` | propriedade | `string` | getter | sim |  |
| `RequisitionNumberUsedForNumbering` | propriedade | `bool` | getter | sim |  |
| `ShippedDate` | propriedade | `string` | getter | sim |  |
| `Supplier` | propriedade | `string` | getter | sim |  |
| `SupplierUsedForNumbering` | propriedade | `bool` | getter | sim |  |

## Beam

`Autodesk.AdvanceSteel.Modelling.Beam`

| Membro | Tipo | Assinatura | Leitura | Gravável | MCP |
|---|---|---|---|---|---|
| `Accept` | método | `void Accept(Modelling.IVisitor visitor)` | infraestrutura |  |  |
| `ChangeProfile` | método | `int ChangeProfile(string newSectionType, string newSectionName)` | escrita/efeito |  |  |
| `GetBodyLengthDeltas` | método | `void GetBodyLengthDeltas(double& out dStartLength, double& out dEndLength)` | só out |  |  |
| `GetClosestPointToSystemline` | método | `Point3d GetClosestPointToSystemline(Point3d pPickPoint)`<br>`Point3d GetClosestPointToSystemline(Point3d pPickPoint, bool bFinite)` | com parâmetros |  |  |
| `GetCSAtPoint` | método | `Matrix3d GetCSAtPoint(Point3d pPnt)` | com parâmetros |  |  |
| `GetLength` | método | `double GetLength()`<br>`double GetLength(Modeler.BodyContext+eBodyContext context)` | com parâmetros, sem parâmetros |  | Sim |
| `GetLengthOffset` | método | `void GetLengthOffset(double& out dStart, double& out dEnd)`<br>`void GetLengthOffset(double& out dStart, double& out dEnd, Modeler.BodyContext+eBodyContext bodyContext)` | com parâmetros, só out |  |  |
| `GetMatProfToWCS` | método | `Matrix3d GetMatProfToWCS()` | sem parâmetros |  |  |
| `GetPaintArea` | método | `double GetPaintArea()` | sem parâmetros |  | Sim |
| `GetPointAtEnd` | método | `Point3d GetPointAtEnd()`<br>`Point3d GetPointAtEnd(double length)` | com parâmetros, sem parâmetros |  | Sim |
| `GetPointAtStart` | método | `Point3d GetPointAtStart()`<br>`Point3d GetPointAtStart(double length)` | com parâmetros, sem parâmetros |  | Sim |
| `GetProfType` | método | `Profiles.ProfileType GetProfType()` | sem parâmetros |  |  |
| `GetSawInformation` | método | `int GetSawInformation(double& out sawLength, double& out flangeAngleAtStart, double& out webAngleAtStart, double& out flangeAngleAtEnd, double& out webAngleAtEnd)` | só out |  |  |
| `GetSystemlineCurve` | método | `void GetSystemlineCurve(Curve3d& out sysCurve)`<br>`void GetSystemlineCurve(Curve3d& out sysCurve, bool bInWCS)`<br>`void GetSystemlineCurve(Curve3d& out sysCurve, bool bInWCS, bool bFinite)` | com parâmetros, só out |  |  |
| `GetTaggedUpDown` | método | `Modelling.Beam+eSide GetTaggedUpDown(Modelling.Beam+eTag tag)` | com parâmetros |  |  |
| `GetUnnotchedGeomExtents` | método | `void GetUnnotchedGeomExtents(Extents& out extents)` | só out |  |  |
| `GetUnwindedBeam` | método | `Modelling.UnwindedBeam GetUnwindedBeam()` | sem parâmetros |  |  |
| `GetWeight` | método | `double GetWeight(int code)` | com parâmetros |  | Sim |
| `GetWeightPerMeter` | método | `double GetWeightPerMeter()` | sem parâmetros |  | Sim |
| `HasUnwindedState` | método | `bool HasUnwindedState()` | sem parâmetros |  |  |
| `IsKindOf` | método | `bool IsKindOf(CADAccess.FilerObject+eObjectType objType)` | com parâmetros |  |  |
| `PhysCSAt` | método | `Matrix3d PhysCSAt(Modelling.Beam+eEnd end)` | com parâmetros |  |  |
| `SetCrossSectionMirrored` | método | `void SetCrossSectionMirrored(bool mirrored, System.Nullable<bool> ignoreFeatures)` | escrita/efeito |  |  |
| `SetLengthOffset` | método | `void SetLengthOffset(double dStart, double dEnd)` | escrita/efeito |  |  |
| `SetStartEndPoints` | método | `void SetStartEndPoints(Point3d ptStart, Point3d ptEnd)` | escrita/efeito |  |  |
| `SetSysEnd` | método | `bool SetSysEnd(Point3d ptEnd)` | escrita/efeito |  |  |
| `SetSysStart` | método | `bool SetSysStart(Point3d ptStart)` | escrita/efeito |  |  |
| `SetUpDownTag` | método | `void SetUpDownTag(Point3d pPnt, Modelling.Beam+eTag tag)` | escrita/efeito |  |  |
| `SetXRotation` | método | `int SetXRotation(double dAngleInDegrees)` | escrita/efeito |  |  |
| `Type` | método | `CADAccess.FilerObject+eObjectType Type()` | sem parâmetros |  |  |
| `Angle` | propriedade | `double` | getter | sim | Sim |
| `Deviation` | propriedade | `double` | getter | sim |  |
| `IsCrossSectionMirrored` | propriedade | `bool` | getter |  |  |
| `Offsets` | propriedade | `Vector2d` | getter | sim |  |
| `PhysCSEnd` | propriedade | `Matrix3d` | getter |  |  |
| `PhysCSMid` | propriedade | `Matrix3d` | getter |  |  |
| `PhysCSStart` | propriedade | `Matrix3d` | getter |  | Sim |
| `ProfName` | propriedade | `string` | getter | sim | Sim |
| `ProfSectionName` | propriedade | `string` | getter |  | Sim |
| `ProfSectionType` | propriedade | `string` | getter |  | Sim |
| `RefAxis` | propriedade | `Modelling.Beam+eRefAxis` | getter | sim | Sim |
| `Runname` | propriedade | `string` | getter |  |  |
| `ShrinkValue` | propriedade | `double` | getter |  |  |
| `SysCSEnd` | propriedade | `Matrix3d` | getter |  |  |
| `SysCSMid` | propriedade | `Matrix3d` | getter |  |  |
| `SysCSStart` | propriedade | `Matrix3d` | getter |  |  |
| `SysLength` | propriedade | `double` | getter |  | Sim |

## PlateBase

`Autodesk.AdvanceSteel.Modelling.PlateBase`

| Membro | Tipo | Assinatura | Leitura | Gravável | MCP |
|---|---|---|---|---|---|
| `Accept` | método | `void Accept(Modelling.IVisitor visitor)` | infraestrutura |  |  |
| `GetArea` | método | `double GetArea()`<br>`double GetArea(int mode)` | com parâmetros, sem parâmetros |  | Sim |
| `GetBaseContourPolygon` | método | `void GetBaseContourPolygon(double dZPos, Point3d[]& out ptsContour)`<br>`void GetBaseContourPolygon(double dZPos, Polyline3d& out polyContour)` | com parâmetros |  | Sim |
| `GetCircumference` | método | `double GetCircumference()` | sem parâmetros |  |  |
| `GetDefinitionPlaneCS` | método | `Matrix3d GetDefinitionPlaneCS()` | sem parâmetros |  |  |
| `GetOuterContour` | método | `Polyline3d GetOuterContour(Matrix3d inCS, bool bGetFromBody, bool bReinit, Modeler.BodyContext+eBodyContext bodyContext)` | com parâmetros |  |  |
| `GetPaintArea` | método | `double GetPaintArea()` | sem parâmetros |  |  |
| `GetPhysLengthAndWidth` | método | `void GetPhysLengthAndWidth(double& out dLength, double& out dWidth)` | só out |  | Sim |
| `GetSysCSAt` | método | `Matrix3d GetSysCSAt(int iVertex)` | com parâmetros |  |  |
| `GetUpperAndLowerPlane` | método | `void GetUpperAndLowerPlane(Plane& out plnUpper, Plane& out plnLower)` | só out |  |  |
| `GetVertexNear` | método | `bool GetVertexNear(Point3d ptWCS, int& out iVertex)` | com parâmetros |  |  |
| `GetWeight` | método | `double GetWeight()`<br>`double GetWeight(int code)` | com parâmetros, sem parâmetros |  | Sim |
| `IsKindOf` | método | `bool IsKindOf(CADAccess.FilerObject+eObjectType objType)` | com parâmetros |  |  |
| `IsPntInSys` | método | `bool IsPntInSys(Point3d ptCheck)` | com parâmetros |  |  |
| `IsRectangular` | método | `bool IsRectangular()` | sem parâmetros |  | Sim |
| `IsVertexManipulationPossible` | método | `bool IsVertexManipulationPossible()` | sem parâmetros |  |  |
| `RemoveVertexNear` | método | `bool RemoveVertexNear(Point3d ptWCS)` | escrita/efeito |  |  |
| `SetLength` | método | `void SetLength(double dLength, bool bIgnoreRaster)` | escrita/efeito |  |  |
| `SetOuterContour` | método | `int SetOuterContour(Polyline3d contour, IEnumerable<ObjectId>& out deletedFeaturesIds, IEnumerable<ObjectId>& out newFeaturesIds)` | escrita/efeito |  |  |
| `setRadius` | método | `void setRadius(double dRadius, bool bIgnoreRaster)` | escrita/efeito |  |  |
| `SetWidth` | método | `void SetWidth(double dWidth, bool bIgnoreRaster)` | escrita/efeito |  |  |
| `SplitEdgeNear` | método | `bool SplitEdgeNear(Point3d ptWCS)` | escrita/efeito |  |  |
| `Type` | método | `CADAccess.FilerObject+eObjectType Type()` | sem parâmetros |  |  |
| `DefinitionPlane` | propriedade | `Plane` | getter | sim |  |
| `Length` | propriedade | `double` | getter |  |  |
| `LengthIncrement` | propriedade | `double` | getter | sim |  |
| `LowerPlane` | propriedade | `Plane` | getter |  |  |
| `LowerZPos` | propriedade | `double` | getter |  |  |
| `Offset` | propriedade | `Vector2d` | getter | sim |  |
| `PlateNormal` | propriedade | `Vector3d` | getter |  | Sim |
| `Portioning` | propriedade | `double` | getter | sim | Sim |
| `RadIncrement` | propriedade | `double` | getter | sim |  |
| `Radius` | propriedade | `double` | getter |  |  |
| `Thickness` | propriedade | `double` | getter | sim | Sim |
| `TopIsZPositive` | propriedade | `bool` | getter | sim |  |
| `UpperPlane` | propriedade | `Plane` | getter |  |  |
| `UpperZPos` | propriedade | `double` | getter |  |  |
| `Width` | propriedade | `double` | getter |  |  |

## BoltPattern

`Autodesk.AdvanceSteel.Modelling.BoltPattern`

| Membro | Tipo | Assinatura | Leitura | Gravável | MCP |
|---|---|---|---|---|---|
| `Accept` | método | `void Accept(Modelling.IVisitor visitor)` | infraestrutura |  |  |
| `Connect` | método | `void Connect(CADAccess.FilerObject[] elems, ConstructionTypes.AtomicElement+eAssemblyLocation location)` | escrita/efeito |  |  |
| `GetHoles` | método | `void GetHoles(Connection.ConnectionHoleFeature[]& out arrHoles)` | só out |  |  |
| `GetMidpoints` | método | `void GetMidpoints(Point3d[]& out ptsMid)` | só out |  | Sim |
| `GetUsedMidpoints` | método | `void GetUsedMidpoints(CADAccess.FilerObject[] arrRelatedObjects, Point3d[]& out ptsMid)` | com parâmetros |  |  |
| `IsKindOf` | método | `bool IsKindOf(CADAccess.FilerObject+eObjectType objType)` | com parâmetros |  |  |
| `MoveRefPoint` | método | `void MoveRefPoint(Vector3d vOffset)` | escrita/efeito |  |  |
| `RemoveConnectedObjects` | método | `void RemoveConnectedObjects(CADAccess.FilerObject[] elems)` | escrita/efeito |  |  |
| `SetCS` | método | `void SetCS(Matrix3d csNew)` | escrita/efeito |  |  |
| `Type` | método | `CADAccess.FilerObject+eObjectType Type()` | sem parâmetros |  |  |
| `BoltNormal` | propriedade | `Vector3d` | getter |  |  |
| `Center` | propriedade | `Point3d` | getter |  | Sim |
| `IsInverted` | propriedade | `bool` | getter | sim |  |
| `Normal` | propriedade | `Vector3d` | getter |  | Sim |
| `NumberOfScrews` | propriedade | `int` | getter |  | Sim |
| `RefPoint` | propriedade | `Point3d` | getter | sim |  |
| `XDirection` | propriedade | `Vector3d` | getter | sim | Sim |
| `YDirection` | propriedade | `Vector3d` | getter | sim |  |

## ScrewBoltPattern

`Autodesk.AdvanceSteel.Modelling.ScrewBoltPattern`

| Membro | Tipo | Assinatura | Leitura | Gravável | MCP |
|---|---|---|---|---|---|
| `Accept` | método | `void Accept(Modelling.IVisitor visitor)` | infraestrutura |  |  |
| `GetHoleDefinition` | método | `Modelling.HoleDefinition GetHoleDefinition(int index)` | com parâmetros |  |  |
| `GetWeight` | método | `double GetWeight()` | sem parâmetros |  |  |
| `IsKindOf` | método | `bool IsKindOf(CADAccess.FilerObject+eObjectType objType)` | com parâmetros |  |  |
| `SetHoleDefinition` | método | `void SetHoleDefinition(int index, Modelling.HoleDefinition holeDef)` | escrita/efeito |  |  |
| `setHoleTolerance` | método | `void setHoleTolerance(double dTol, bool bForwardToAllHoles)` | escrita/efeito |  |  |
| `Type` | método | `CADAccess.FilerObject+eObjectType Type()` | sem parâmetros |  |  |
| `Annotation` | propriedade | `string` | getter |  |  |
| `AssemblyLocation` | propriedade | `ConstructionTypes.AtomicElement+eAssemblyLocation` | getter | sim | Sim |
| `BindingLength` | propriedade | `double` | getter | sim |  |
| `BindingLengthAddition` | propriedade | `double` | getter | sim |  |
| `BoltAssembly` | propriedade | `string` | getter | sim | Sim |
| `BoltHeadDiameter` | propriedade | `double` | getter |  |  |
| `BoltHeadHeight` | propriedade | `double` | getter |  |  |
| `BoltHeadNumEdges` | propriedade | `int` | getter |  |  |
| `BottomToolDiameter` | propriedade | `double` | getter |  |  |
| `BottomToolHeight` | propriedade | `double` | getter |  |  |
| `Grade` | propriedade | `string` | getter | sim | Sim |
| `HoleTolerance` | propriedade | `double` | getter | sim | Sim |
| `IgnoreMaxGap` | propriedade | `bool` | getter | sim |  |
| `MaxBottomDiameter` | propriedade | `double` | getter |  |  |
| `MaxTopDiameter` | propriedade | `double` | getter |  |  |
| `NutDiameter` | propriedade | `double` | getter |  |  |
| `NutHeight` | propriedade | `double` | getter |  |  |
| `ScrewBoltType` | propriedade | `Modelling.ScrewBoltPattern+eScrewBoltType` | getter |  |  |
| `ScrewDiameter` | propriedade | `double` | getter | sim | Sim |
| `ScrewLength` | propriedade | `double` | getter | sim | Sim |
| `Standard` | propriedade | `string` | getter | sim | Sim |
| `SumBottomHeight` | propriedade | `double` | getter |  |  |
| `SumBottomSetHeight` | propriedade | `double` | getter |  |  |
| `SumTopHeight` | propriedade | `double` | getter |  |  |
| `SumTopSetHeight` | propriedade | `double` | getter |  |  |
| `TopToolDiameter` | propriedade | `double` | getter |  |  |
| `TopToolHeight` | propriedade | `double` | getter |  |  |

## AnchorPattern

`Autodesk.AdvanceSteel.Modelling.AnchorPattern`

| Membro | Tipo | Assinatura | Leitura | Gravável | MCP |
|---|---|---|---|---|---|
| `Accept` | método | `void Accept(Modelling.IVisitor visitor)` | infraestrutura |  |  |
| `GetAnchorAxis` | método | `void GetAnchorAxis(Polyline3d[]& out lines)` | só out |  |  |
| `GetWeight` | método | `double GetWeight()` | sem parâmetros |  |  |
| `IsKindOf` | método | `bool IsKindOf(CADAccess.FilerObject+eObjectType objType)` | com parâmetros |  |  |
| `SetArrangerHeight` | método | `void SetArrangerHeight(double dVal)`<br>`void SetArrangerHeight(double dVal, int nDirection)` | escrita/efeito |  |  |
| `SetArrangerLength` | método | `void SetArrangerLength(double dVal)`<br>`void SetArrangerLength(double dVal, int nDirection)` | escrita/efeito |  |  |
| `SetNumberOfScrews` | método | `void SetNumberOfScrews(int nVal)` | escrita/efeito |  |  |
| `Type` | método | `CADAccess.FilerObject+eObjectType Type()` | sem parâmetros |  |  |
| `AnchorPartName` | propriedade | `string` | getter |  |  |
| `AnchorType` | propriedade | `Modelling.AnchorPattern+eAnchorType` | getter |  | Sim |
| `AnchorUnfoldedLength` | propriedade | `double` | getter |  |  |
| `ArrangerType` | propriedade | `Arrangement.Arranger+eArrangerType` | getter | sim |  |
| `BoundedSide` | propriedade | `Modelling.AnchorPattern+eBoundedSide` | getter | sim |  |
| `Dx` | propriedade | `double` | getter | sim |  |
| `Dy` | propriedade | `double` | getter | sim |  |
| `Height` | propriedade | `double` | getter |  |  |
| `Length` | propriedade | `double` | getter |  |  |
| `MaxBottomDiameter` | propriedade | `double` | getter |  |  |
| `MaxTopDiameter` | propriedade | `double` | getter |  |  |
| `MidpointOnLowerLeft` | propriedade | `Point3d` | getter |  |  |
| `MidpointOnLowerRight` | propriedade | `Point3d` | getter |  |  |
| `MidpointOnUpperLeft` | propriedade | `Point3d` | getter |  |  |
| `MidpointOnUpperRight` | propriedade | `Point3d` | getter |  |  |
| `NumberOfScrews` | propriedade | `int` | getter | sim |  |
| `Nx` | propriedade | `int` | getter | sim |  |
| `Ny` | propriedade | `int` | getter | sim |  |
| `OrientationType` | propriedade | `Modelling.AnchorPattern+eOrientationType` | getter | sim |  |
| `Radius` | propriedade | `double` | getter | sim |  |
| `ThreadLength` | propriedade | `double` | getter |  |  |
| `Wx` | propriedade | `double` | getter | sim |  |
| `Wy` | propriedade | `double` | getter | sim |  |

## WeldPattern

`Autodesk.AdvanceSteel.Modelling.WeldPattern`

| Membro | Tipo | Assinatura | Leitura | Gravável | MCP |
|---|---|---|---|---|---|
| `Accept` | método | `void Accept(Modelling.IVisitor visitor)` | infraestrutura |  |  |
| `Connect` | método | `void Connect(CADAccess.FilerObject[] elems, ConstructionTypes.AtomicElement+eAssemblyLocation location)` | escrita/efeito |  |  |
| `GetCenterPoint` | método | `Point3d GetCenterPoint()` | sem parâmetros |  | Sim |
| `GetFaceShape` | método | `Modelling.WeldPattern+eWeldSurface GetFaceShape(Modelling.WeldPattern+eSeamPosition seamPos)` | com parâmetros |  |  |
| `GetSeamExecuting` | método | `Modelling.WeldPattern+eExecType GetSeamExecuting(Modelling.WeldPattern+eSeamPosition seamPos)` | com parâmetros |  |  |
| `GetSeamThickness` | método | `double GetSeamThickness(Modelling.WeldPattern+eSeamPosition seamPos)` | com parâmetros |  | Sim |
| `GetWeldType` | método | `Modelling.WeldPattern+eWeldType GetWeldType(Modelling.WeldPattern+eSeamPosition seamPos)` | com parâmetros |  | Sim |
| `IsCombiWelding` | método | `int IsCombiWelding()` | sem parâmetros |  |  |
| `IsKindOf` | método | `bool IsKindOf(CADAccess.FilerObject+eObjectType objType)` | com parâmetros |  |  |
| `IsLowerWeldingChangeable` | método | `int IsLowerWeldingChangeable()` | sem parâmetros |  |  |
| `RemoveConnectedObjects` | método | `void RemoveConnectedObjects(CADAccess.FilerObject[] elems)` | escrita/efeito |  |  |
| `SetCS` | método | `void SetCS(Matrix3d newCs)` | escrita/efeito |  |  |
| `SetFaceShape` | método | `void SetFaceShape(Modelling.WeldPattern+eSeamPosition seamPos, Modelling.WeldPattern+eWeldSurface weldSurface)` | escrita/efeito |  |  |
| `SetSeamExecuting` | método | `void SetSeamExecuting(Modelling.WeldPattern+eSeamPosition seamPos, Modelling.WeldPattern+eExecType execType)` | escrita/efeito |  |  |
| `SetSeamThickness` | método | `void SetSeamThickness(Modelling.WeldPattern+eSeamPosition seamPos, double dThick)` | escrita/efeito |  |  |
| `SetWeldType` | método | `void SetWeldType(Modelling.WeldPattern+eSeamPosition seamPos, Modelling.WeldPattern+eWeldType weldType)` | escrita/efeito |  |  |
| `TransformBy` | método | `void TransformBy(Matrix3d mat)` | escrita/efeito |  |  |
| `Type` | método | `CADAccess.FilerObject+eObjectType Type()` | sem parâmetros |  |  |
| `AdditionalData` | propriedade | `string` | getter | sim |  |
| `AssemblyLocation` | propriedade | `ConstructionTypes.AtomicElement+eAssemblyLocation` | getter | sim | Sim |
| `DoubleEffectiveThroat` | propriedade | `double` | getter | sim |  |
| `DoublePreparationDepth` | propriedade | `double` | getter | sim |  |
| `DoubleRootOpening` | propriedade | `double` | getter | sim |  |
| `DoubleWeldText` | propriedade | `string` | getter | sim |  |
| `IsClosed` | propriedade | `bool` | getter | sim | Sim |
| `Length` | propriedade | `double` | getter |  | Sim |
| `MainEffectiveThroat` | propriedade | `double` | getter | sim |  |
| `MainPreparationDepth` | propriedade | `double` | getter | sim |  |
| `MainRootOpening` | propriedade | `double` | getter | sim |  |
| `MainWeldText` | propriedade | `string` | getter | sim |  |
| `Pitch` | propriedade | `double` | getter | sim |  |
| `Prefix` | propriedade | `string` | getter | sim |  |
| `SeamDistance` | propriedade | `double` | getter | sim |  |
| `SingleSeamLength` | propriedade | `double` | getter | sim |  |
| `Thickness` | propriedade | `double` | getter | sim |  |

## SpecialPart

`Autodesk.AdvanceSteel.Modelling.SpecialPart`

| Membro | Tipo | Assinatura | Leitura | Gravável | MCP |
|---|---|---|---|---|---|
| `Accept` | método | `void Accept(Modelling.IVisitor visitor)` | infraestrutura |  |  |
| `IsKindOf` | método | `bool IsKindOf(CADAccess.FilerObject+eObjectType objType)` | com parâmetros |  |  |
| `SetBlock` | método | `void SetBlock(ObjectId blockId, double scale)`<br>`void SetBlock(string blockName, double scale)` | escrita/efeito |  |  |
| `SetFrom` | método | `void SetFrom(DocumentManagement.OpenDatabase sourceDatabase, string blockName, bool bOverrideIfExist, double dScale)` | escrita/efeito |  |  |
| `Type` | método | `CADAccess.FilerObject+eObjectType Type()` | sem parâmetros |  |  |
| `BlockName` | propriedade | `string` | getter |  | Sim |
| `Depth` | propriedade | `double` | getter |  |  |
| `Length` | propriedade | `double` | getter |  |  |
| `PaintArea` | propriedade | `double` | getter | sim |  |
| `ReferredBlockId` | propriedade | `ObjectId` | getter |  |  |
| `Scale` | propriedade | `double` | getter | sim |  |
| `Weight` | propriedade | `double` | getter | sim | Sim |
| `Width` | propriedade | `double` | getter |  |  |

## FeatureObject

`Autodesk.AdvanceSteel.ConstructionTypes.FeatureObject`

| Membro | Tipo | Assinatura | Leitura | Gravável | MCP |
|---|---|---|---|---|---|
| `Accept` | método | `void Accept(Modelling.IVisitor visitor)` | infraestrutura |  |  |
| `GetOwner` | método | `CADAccess.FilerObject GetOwner()` | sem parâmetros |  |  |
| `IsKindOf` | método | `bool IsKindOf(CADAccess.FilerObject+eObjectType objType)` | com parâmetros |  |  |
| `Type` | método | `CADAccess.FilerObject+eObjectType Type()` | sem parâmetros |  |  |
| `updateYourself` | método | `void updateYourself()` | escrita/efeito |  |  |
| `CS` | propriedade | `Matrix3d` | getter | sim |  |
| `IsFromFitter` | propriedade | `bool` | getter |  |  |
| `ObjectIndex` | propriedade | `int` | getter | sim |  |
| `Recipe` | propriedade | `Modelling.IBodyRecipe` | getter |  |  |
| `UseGap` | propriedade | `bool` | getter | sim |  |

## PassiveConstructionObject

`Autodesk.AdvanceSteel.ConstructionTypes.PassiveConstructionObject`

| Membro | Tipo | Assinatura | Leitura | Gravável | MCP |
|---|---|---|---|---|---|
| `Accept` | método | `void Accept(Modelling.IVisitor visitor)` | infraestrutura |  |  |
| `GetDriverRealIndex` | método | `int GetDriverRealIndex(ObjectId driverId)` | com parâmetros |  |  |
| `IsKindOf` | método | `bool IsKindOf(CADAccess.FilerObject+eObjectType objType)` | com parâmetros |  |  |
| `ReplaceDrivers` | método | `void ReplaceDrivers(Dictionary<ConstructionTypes.ConstructionElement, ConstructionTypes.ConstructionElement> mapOldNewDrivers, bool updateConstruction)` | escrita/efeito |  |  |
| `Type` | método | `CADAccess.FilerObject+eObjectType Type()` | sem parâmetros |  |  |
| `Update` | método | `void Update()` | escrita/efeito |  |  |
| `AutoUpdate` | propriedade | `bool` | getter | sim |  |
| `Drivens` | propriedade | `IEnumerable<ObjectId>` | getter |  | Sim |
| `Drivers` | propriedade | `IEnumerable<ObjectId>` | getter |  | Sim |
| `DrivingPassiveConstructionObject` | propriedade | `ConstructionTypes.PassiveConstructionObject` | getter |  |  |
| `HasDrivingPassiveConstructionObject` | propriedade | `bool` | getter |  |  |
| `ParameterSet` | propriedade | `ConstructionTypes.RuleParameterSet` | getter |  |  |
| `Rule` | propriedade | `ConstructionTypes.ConstructionRule` | getter |  |  |
| `ShouldDeleteDrivenWhenDeleteJoint` | propriedade | `bool` | getter | sim |  |

## UserAutoConstructionObject

`Autodesk.AdvanceSteel.ConstructionTypes.UserAutoConstructionObject`

| Membro | Tipo | Assinatura | Leitura | Gravável | MCP |
|---|---|---|---|---|---|
| `Accept` | método | `void Accept(Modelling.IVisitor visitor)` | infraestrutura |  |  |
| `AddNSAModuleEffort` | método | `void AddNSAModuleEffort(string moduleId, string standardId, StructuralAnalysis.Effort effort)` | escrita/efeito |  |  |
| `CheckJoint` | método | `ConstructionTypes.UserAutoConstructionObject+NodeStatus CheckJoint(ConstructionTypes.IStressModuleJoint SMJointHandler, string& out reportFileName, string& out shortReportFileName)` | escrita/efeito |  |  |
| `DetectNode` | método | `int DetectNode()` | escrita/efeito |  |  |
| `FreeConnectionPages` | método | `void FreeConnectionPages(System.ValueType rule)` | escrita/efeito |  |  |
| `GetAvailableDesignModules` | método | `void GetAvailableDesignModules(Dictionary<string, string>& out modulesMap, string& out defaultModule)` | com parâmetros |  |  |
| `GetConnectionDesignPages` | método | `bool GetConnectionDesignPages(ConstructionTypes.IStressModuleJoint SMJointHandler, nint[]& out hwnds, string[]& out pagesNames)` | com parâmetros |  |  |
| `GetConnectionPages` | método | `bool GetConnectionPages(System.ValueType& rule, ConstructionTypes.IJoint jointHandler, nint[]& out hwnds, string[]& out pagesNames, int[][]& out pagesGroups, string[]& out groupsNames, string& out sheetName, ConstructionTypes.UserAutoConstructionObject+GUIDimension& out pagesSize)` | com parâmetros |  |  |
| `GetInputObjectGroupId` | método | `int GetInputObjectGroupId(ObjectId objId)` | com parâmetros |  |  |
| `GetLastReportFilename` | método | `string GetLastReportFilename(bool bQuickReportFilename)` | com parâmetros |  |  |
| `GetNSAModuleEfforts` | método | `StructuralAnalysis.Effort[] GetNSAModuleEfforts(string moduleId, string standardId)` | com parâmetros |  |  |
| `getPrimaryJoint` | método | `ConstructionTypes.UserAutoConstructionObject getPrimaryJoint()` | sem parâmetros |  |  |
| `IsKindOf` | método | `bool IsKindOf(CADAccess.FilerObject+eObjectType objType)` | com parâmetros |  |  |
| `isPrimaryOrPartOfPrimaryJoint` | método | `bool isPrimaryOrPartOfPrimaryJoint()` | sem parâmetros |  |  |
| `isSecondaryOrPartOfSecondaryJoint` | método | `bool isSecondaryOrPartOfSecondaryJoint()` | sem parâmetros |  |  |
| `Load` | método | `void Load(ConstructionTypes.IFiler filer)`<br>`void Load(ConstructionTypes.IFiler filer, ConstructionTypes.IJoint+FilerLoadOption fo)` | escrita/efeito |  |  |
| `LoadNSAModuleFiler` | método | `void LoadNSAModuleFiler(string moduleNormId, ConstructionTypes.IFiler filer)` | escrita/efeito |  |  |
| `PresizeJoint` | método | `ConstructionTypes.UserAutoConstructionObject+NodeStatus PresizeJoint(ConstructionTypes.IStressModuleJoint SMJointHandler, string& out reportFileName, string& out shortReportFileName)` | escrita/efeito |  |  |
| `registSecondaryJoint` | método | `int registSecondaryJoint(ConstructionTypes.AutoConstructionObject pObj)` | escrita/efeito |  |  |
| `RemoveNSAModuleEfforts` | método | `void RemoveNSAModuleEfforts(string moduleId, string standardId)` | escrita/efeito |  |  |
| `Save` | método | `ConstructionTypes.IFiler Save()` | escrita/efeito |  |  |
| `SaveData` | método | `void SaveData()` | escrita/efeito |  |  |
| `SaveNSAModuleFiler` | método | `ConstructionTypes.IFiler SaveNSAModuleFiler(string moduleNormId)` | escrita/efeito |  |  |
| `SetCreatedObjects` | método | `void SetCreatedObjects(IEnumerable<ConstructionTypes.CreatedObjectInformation> objects, bool bCreationStatus)` | escrita/efeito |  |  |
| `SetInputObjects` | método | `void SetInputObjects(IEnumerable<CADAccess.FilerObject> objects, IEnumerable<int> groupIds)` | escrita/efeito |  |  |
| `SetOutputReportId` | método | `void SetOutputReportId(int nId)` | escrita/efeito |  |  |
| `SetOutputReportPath` | método | `void SetOutputReportPath(string path)` | escrita/efeito |  |  |
| `Type` | método | `CADAccess.FilerObject+eObjectType Type()` | sem parâmetros |  |  |
| `unregistSecondaryJoint` | método | `int unregistSecondaryJoint(ConstructionTypes.AutoConstructionObject pObj)` | escrita/efeito |  |  |
| `UpdateDrivenConstruction` | método | `void UpdateDrivenConstruction()` | escrita/efeito |  |  |
| `upgradeToPrimary` | método | `void upgradeToPrimary()` | escrita/efeito |  |  |
| `ApprovalStatus` | propriedade | `ConstructionTypes.ConstructionElement+eApprovalStatus` | getter | sim |  |
| `CheckIsAvailable` | propriedade | `bool` | getter |  |  |
| `CheckStatus` | propriedade | `ConstructionTypes.UserAutoConstructionObject+NodeStatus` | getter |  |  |
| `CreatedObjects` | propriedade | `IEnumerable<CADAccess.FilerObject>` | getter |  |  |
| `DefaultAttributeStateAccessible` | propriedade | `bool` | só escrita | sim |  |
| `ExportIsAvailable` | propriedade | `bool` | getter |  |  |
| `Id` | propriedade | `ObjectId` | getter |  |  |
| `ImportIsAvailable` | propriedade | `bool` | getter |  |  |
| `InputObjects` | propriedade | `IEnumerable<CADAccess.FilerObject>` | getter |  |  |
| `NewRuleTypeRunName` | propriedade | `string` | getter |  |  |
| `NSAModuleInternalName` | propriedade | `string` | getter | sim |  |
| `NSAModuleName` | propriedade | `string` | getter |  | Sim |
| `NSAModuleStandard` | propriedade | `string` | getter |  |  |
| `PresizeIsAvailable` | propriedade | `bool` | getter |  |  |
| `PrimaryJointName` | propriedade | `string` | getter | sim | Sim |
| `UpdateWhenDriverFeatChanged` | propriedade | `bool` | getter |  |  |
| `UseDetailedTorsors` | propriedade | `bool` | getter | sim |  |
| `UseLoadCasesIsAvailable` | propriedade | `bool` | getter |  |  |

