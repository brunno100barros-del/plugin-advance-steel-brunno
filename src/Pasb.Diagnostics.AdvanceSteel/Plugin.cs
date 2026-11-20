using Autodesk.AutoCAD.Runtime;

[assembly: ExtensionApplication(typeof(Pasb.Diagnostics.AdvanceSteel.Plugin))]
[assembly: CommandClass(typeof(Pasb.Diagnostics.AdvanceSteel.Commands))]

namespace Pasb.Diagnostics.AdvanceSteel;

/// <summary>
/// Ponto de entrada carregado pelo bundle do AutoCAD na inicialização (desde a 0.4.0, para exibir a aba PASB).
/// Initialize só cria a aba da faixa de opções: não lê o desenho, não abre arquivos, não registra reatores de objetos.
/// </summary>
public sealed class Plugin : IExtensionApplication
{
    public void Initialize()
    {
        PasbRibbon.Initialize();
    }

    public void Terminate()
    {
        // Só fecha a ponte do Conector IA se estiver ligada (apaga o arquivo de descoberta). Nada de interface aqui:
        // redesenhar o ícone da faixa de opções durante o fechamento derrubava o AutoCAD (ERRO FATAL c015000f).
        try { if (AiConnector.IsRunning) AiConnector.StopBridgeOnly(); } catch (System.Exception) { }
    }
}
