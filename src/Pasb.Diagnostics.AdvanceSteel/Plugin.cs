using System;
using System.IO;
using System.Reflection;
using Autodesk.AutoCAD.Runtime;
using AcAppUi = Autodesk.AutoCAD.ApplicationServices.Application;

[assembly: ExtensionApplication(typeof(Pasb.Diagnostics.AdvanceSteel.Plugin))]
[assembly: CommandClass(typeof(Pasb.Diagnostics.AdvanceSteel.Commands))]

namespace Pasb.Diagnostics.AdvanceSteel;

/// <summary>
/// Ponto de entrada carregado pelo bundle do AutoCAD na inicialização (desde a 0.4.0, para exibir a aba PASB).
/// Initialize não faz nada de interface: só agenda a criação da aba para o primeiro momento ocioso do AutoCAD
/// (0.5.2: ERRO FATAL c015000f ao abrir o programa; criar interface durante o carregamento é a causa suspeita).
/// Não lê o desenho, não abre arquivos, não registra reatores de objetos.
/// </summary>
public sealed class Plugin : IExtensionApplication
{
    public void Initialize()
    {
        StartupLog.Write("Initialize: agendando a aba PASB para o primeiro momento ocioso");
        try { AcAppUi.Idle += OnFirstIdle; }
        catch (System.Exception ex) { StartupLog.Write("Initialize falhou: " + ex); }
    }

    private static void OnFirstIdle(object? sender, EventArgs e)
    {
        AcAppUi.Idle -= OnFirstIdle;
        StartupLog.Write("Primeiro ocioso: criando a aba PASB");
        PasbRibbon.Initialize();
        StartupLog.Write("Aba PASB: concluído");
    }

    public void Terminate()
    {
        // Só fecha a ponte do Conector IA se estiver ligada (apaga o arquivo de descoberta). Nada de interface aqui.
        try { if (AiConnector.IsRunning) AiConnector.StopBridgeOnly(); } catch (System.Exception) { }
    }
}

/// <summary>
/// Registro curto da inicialização em %LOCALAPPDATA%\PASB\inicio.log, para diagnosticar falhas ao abrir o programa.
/// Nunca lança. Recomeça o arquivo quando passa de 256 KB.
/// </summary>
internal static class StartupLog
{
    private static readonly string Path_ =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PASB", "inicio.log");

    private static readonly string Version =
        typeof(StartupLog).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion?.Split('+')[0] ?? "?";

    public static void Write(string message)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path_)!);
            if (File.Exists(Path_) && new FileInfo(Path_).Length > 256 * 1024) File.Delete(Path_);
            File.AppendAllText(Path_, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{Version}] {message}{Environment.NewLine}");
        }
        catch (System.Exception) { }
    }
}
