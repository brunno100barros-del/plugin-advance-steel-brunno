using System;
using System.IO;
using System.Text;
using Microsoft.Extensions.Logging;
using Xbim.Common.Configuration;

namespace Pasb.IfcImport;

/// <summary>
/// Liga o log da xBIM (avisos e erros) em %LOCALAPPDATA%\PASB\ifc-import.log, refeito a cada execução.
/// Sem ele, a xBIM só diz "Processing halted due to model error, see logs" e não conta qual peça falhou.
/// </summary>
internal static class XbimLog
{
    private static readonly object Gate = new();
    private static StreamWriter? _w;
    public static string? Path { get; private set; }

    public static void Start()
    {
        try
        {
            string dir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PASB");
            Directory.CreateDirectory(dir);
            Path = System.IO.Path.Combine(dir, "ifc-import.log");
            _w = new StreamWriter(Path, false, new UTF8Encoding(false)) { AutoFlush = true };
            _w.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} Pasb.IfcImport {string.Join(' ', Environment.GetCommandLineArgs()[1..])}");
        }
        catch (Exception) { Path = null; _w = null; }   // sem log, a importação segue igual
        XbimServices.Current.ConfigureServices(s => s.AddXbimToolkit(c => c.AddLoggerFactory(new Factory())));
    }

    private static void Write(string line)
    {
        if (_w is null) return;
        lock (Gate) { try { _w.WriteLine(line); } catch (Exception) { } }
    }

    private sealed class Factory : ILoggerFactory
    {
        public void AddProvider(ILoggerProvider provider) { }
        public ILogger CreateLogger(string categoryName) => new Logger(categoryName);
        public void Dispose() { }
    }

    private sealed class Logger(string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Warning;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;
            Write($"[{logLevel}] {category}: {formatter(state, exception)}" + (exception is null ? "" : $" | {exception.GetType().Name}: {exception.Message}"));
        }
    }
}
