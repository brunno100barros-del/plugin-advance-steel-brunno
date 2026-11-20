using System;
using System.IO;
using System.Text;

namespace Pasb.Diagnostics.Core;

/// <summary>
/// Registro passo a passo, gravado no disco ANTES de cada leitura.
/// Motivo: erros fatais do AutoCAD (código 0xE0000001, por exemplo eNotOpenForWrite) encerram o processo
/// sem passar por catch nem finally. Com o trace, a última linha do arquivo indica a leitura que causou a queda.
/// </summary>
public sealed class TraceLog : IDisposable
{
    private readonly FileStream _stream;
    private readonly StreamWriter _writer;

    public TraceLog(string path)
    {
        _stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
        _writer = new StreamWriter(_stream, new UTF8Encoding(false)) { AutoFlush = true };
    }

    /// <summary>Grava a linha e força a ida ao disco antes de retornar.</summary>
    public void Write(string line)
    {
        _writer.WriteLine(DateTime.Now.ToString("HH:mm:ss.fff") + " " + line);
        _stream.Flush(flushToDisk: true);
    }

    public void Dispose() => _writer.Dispose();
}
