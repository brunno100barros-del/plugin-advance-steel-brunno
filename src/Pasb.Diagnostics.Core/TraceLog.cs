using System;
using System.IO;
using System.Text;

namespace Pasb.Diagnostics.Core;

/// <summary>
/// Registro passo a passo, gravado ANTES de cada leitura.
/// Motivo: erros fatais do AutoCAD (código 0xE0000001, por exemplo eNotOpenForWrite) encerram o processo
/// sem passar por catch nem finally. Com o trace, a última linha do arquivo indica a leitura que causou a queda.
/// </summary>
public sealed class TraceLog : IDisposable
{
    private readonly FileStream _stream;
    private readonly StreamWriter _writer;
    private readonly bool _durable;

    /// <param name="durable">
    /// true (diagnóstico): cada linha é forçada até o disco físico antes de retornar; sobrevive até a queda do Windows, mas custa
    /// uma gravação física por linha. false (exportação): cada linha vai para o Windows antes de retornar, sem esperar o disco;
    /// sobrevive à queda do AutoCAD (o Windows grava o que já recebeu), não à queda do Windows, e é muito mais rápido.
    /// </param>
    public TraceLog(string path, bool durable = true)
    {
        _durable = durable;
        _stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
        _writer = new StreamWriter(_stream, new UTF8Encoding(false)) { AutoFlush = true };
    }

    /// <summary>Grava a linha e a entrega ao Windows (e ao disco físico, se durável) antes de retornar.</summary>
    public void Write(string line)
    {
        _writer.WriteLine(DateTime.Now.ToString("HH:mm:ss.fff") + " " + line);
        if (_durable) _stream.Flush(flushToDisk: true);
    }

    public void Dispose() => _writer.Dispose();
}
