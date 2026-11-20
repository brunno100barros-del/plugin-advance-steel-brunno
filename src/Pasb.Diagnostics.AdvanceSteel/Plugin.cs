using Autodesk.AutoCAD.Runtime;

[assembly: ExtensionApplication(typeof(Pasb.Diagnostics.AdvanceSteel.Plugin))]
[assembly: CommandClass(typeof(Pasb.Diagnostics.AdvanceSteel.Commands))]

namespace Pasb.Diagnostics.AdvanceSteel;

/// <summary>
/// Ponto de entrada carregado pelo bundle do AutoCAD.
/// Inicialização propositalmente vazia (docs/DIAGNOSTIC-DESIGN.md, seção 9.2):
/// não lê o desenho, não abre arquivos, não registra eventos nem reatores.
/// </summary>
public sealed class Plugin : IExtensionApplication
{
    public void Initialize()
    {
    }

    public void Terminate()
    {
    }
}
