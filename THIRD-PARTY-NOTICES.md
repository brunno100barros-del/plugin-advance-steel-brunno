# Componentes de terceiros

O PASB é código próprio. Só a **importação de IFC** (`Contents\IfcImport\Pasb.IfcImport.exe`, um programa separado que roda fora do AutoCAD) usa bibliotecas de terceiros, distribuídas **sem modificação**:

| Componente | Versão | Licença | Código-fonte |
|---|---|---|---|
| xBIM Essentials (Xbim.Common, Xbim.Ifc, Xbim.Ifc2x3, Xbim.Ifc4, Xbim.Ifc4x3, Xbim.IO.*) | 6.1.605 | CDDL-1.0 | <https://github.com/xBimTeam/XbimEssentials> |
| xBIM Geometry (Xbim.Geometry.*, Xbim.ModelGeometry.Scene, Xbim.Tessellator) | 6.3.891-netcore | CDDL-1.0 | <https://github.com/xBimTeam/XbimGeometry> |
| Open CASCADE Technology (Xbim.Geometry.Occt) | 7.8.1 | LGPL-2.1 com a exceção OCCT 1.0 | <https://dev.opencascade.org/> |
| Dependências Microsoft.Extensions.* trazidas pela xBIM | conforme o pacote | MIT | <https://github.com/dotnet/runtime> |

- **CDDL-1.0**: <https://opensource.org/license/cddl-1-0>. Os arquivos da xBIM não foram alterados. O código-fonte está nos links acima.
- **LGPL-2.1 com exceção OCCT**: <https://dev.opencascade.org/resources/licensing>. A biblioteca é usada como DLL dinâmica, sem modificação, e pode ser substituída por outra versão compatível na pasta `Contents\IfcImport`.

O restante do plugin (exportação IFC, leitura do Advance Steel, listas, Conector IA, janelas) não usa bibliotecas de terceiros.
