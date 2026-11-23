using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Xml;

namespace Pasb.Installer;

/// <summary>
/// PASB-Instalador-&lt;versão&gt;.exe: instala o plugin a partir do pacote embutido, com o mesmo resultado do Instalar-PASB.ps1
/// (pasta PASB-Diagnostics.bundle em %APPDATA%\Autodesk\ApplicationPlugins, só do usuário atual, sem administrador).
/// - Recusa instalar com o Advance Steel aberto (oferece tentar de novo).
/// - Se o servidor do Conector IA (Pasb.Mcp) estiver em uso, mantém Contents\Mcp, como o instalador em PowerShell.
/// - Não altera nenhum outro plugin nem configuração do AutoCAD.
/// </summary>
internal static class Program
{
    private const string Title = "PASB — Instalador";

    [STAThread]
    private static int Main()
    {
        ApplicationConfiguration.Initialize();
        try
        {
            using Stream? payload = Assembly.GetExecutingAssembly().GetManifestResourceStream("PASB.payload.zip");
            if (payload is null) { Error("Este instalador foi gerado sem o pacote do plugin. Gere de novo com bundle\\Build-Release.ps1."); return 2; }
            using var zip = new ZipArchive(payload, ZipArchiveMode.Read);

            // Pacote: PASB-<versão>/PASB-Diagnostics.bundle/...
            ZipArchiveEntry? contents = zip.Entries.FirstOrDefault(e => e.FullName.Replace('\\', '/').EndsWith("PASB-Diagnostics.bundle/PackageContents.xml", StringComparison.OrdinalIgnoreCase));
            if (contents is null) { Error("Pacote do plugin incompleto (PackageContents.xml não encontrado)."); return 2; }
            string prefix = contents.FullName.Replace('\\', '/')[..^"PackageContents.xml".Length];
            string version = ReadVersion(contents);

            string target = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Autodesk", "ApplicationPlugins", "PASB-Diagnostics.bundle");
            string? installed = File.Exists(Path.Combine(target, "PackageContents.xml")) ? TryReadVersion(Path.Combine(target, "PackageContents.xml")) : null;

            string question = $"Instalar o PASB {version} (Coordix · BIMOFFICE) no Advance Steel 2027?\n\n" +
                              (installed is null ? "" : $"Versão instalada agora: {installed} (será substituída; as configurações são mantidas).\n\n") +
                              $"Destino (só para o usuário {Environment.UserName}, sem administrador):\n{target}";
            if (MessageBox.Show(question, Title, MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK) return 1;

            while (Process.GetProcessesByName("acad").Length > 0)
            {
                if (MessageBox.Show("O AutoCAD / Advance Steel está aberto.\n\nSalve o trabalho, feche o programa e clique em Repetir.",
                        Title, MessageBoxButtons.RetryCancel, MessageBoxIcon.Warning) != DialogResult.Retry) return 1;
            }

            bool mcpInUse = Process.GetProcessesByName("Pasb.Mcp").Length > 0 && File.Exists(Path.Combine(target, "Contents", "Mcp", "Pasb.Mcp.exe"));
            Install(zip, prefix, target, keepMcp: mcpInUse);

            string done = $"PASB {version} instalado.\n\n" +
                          "Abra o Advance Steel 2027. No aviso de segurança do PASB, clique em \"Sempre carregar\".\n" +
                          "A aba PASB aparece na faixa de opções logo depois que o programa termina de abrir." +
                          (mcpInUse ? "\n\nO servidor do Conector IA estava em uso pelo Claude Code e foi mantido. Feche o Claude Code e instale de novo para atualizá-lo." : "");
            MessageBox.Show(done, Title, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return 0;
        }
        catch (Exception ex)
        {
            Error("A instalação falhou:\n" + ex.Message + "\n\nNada foi alterado no Advance Steel. Se o problema continuar, use o pacote .zip (INSTALACAO.md).");
            return 3;
        }
    }

    /// <summary>Copia o bundle. Sem o Conector IA em uso: apaga a pasta antiga inteira. Em uso: mantém Contents\Mcp.</summary>
    private static void Install(ZipArchive zip, string prefix, string target, bool keepMcp)
    {
        string mcpDir = Path.Combine(target, "Contents", "Mcp");
        if (Directory.Exists(target))
        {
            if (!keepMcp) Directory.Delete(target, recursive: true);
            else
            {
                foreach (string f in Directory.GetFiles(target)) File.Delete(f);
                foreach (string d in Directory.GetDirectories(target))
                {
                    if (string.Equals(Path.GetFileName(d), "Contents", StringComparison.OrdinalIgnoreCase))
                    {
                        foreach (string f in Directory.GetFiles(d)) File.Delete(f);
                        foreach (string sub in Directory.GetDirectories(d))
                            if (!string.Equals(sub.TrimEnd('\\'), mcpDir, StringComparison.OrdinalIgnoreCase)) Directory.Delete(sub, true);
                    }
                    else Directory.Delete(d, true);
                }
            }
        }
        Directory.CreateDirectory(target);
        string fullTarget = Path.GetFullPath(target) + Path.DirectorySeparatorChar;
        foreach (ZipArchiveEntry e in zip.Entries)
        {
            string name = e.FullName.Replace('\\', '/');
            if (!name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
            string rel = name[prefix.Length..];
            if (rel.Length == 0) continue;
            string dest = Path.GetFullPath(Path.Combine(target, rel.Replace('/', Path.DirectorySeparatorChar)));
            if (!dest.StartsWith(fullTarget, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Caminho inválido no pacote: " + name);
            if (keepMcp && dest.StartsWith(mcpDir + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) continue;
            if (name.EndsWith('/')) { Directory.CreateDirectory(dest); continue; }
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            e.ExtractToFile(dest, overwrite: true);
        }
    }

    private static string ReadVersion(ZipArchiveEntry e)
    {
        using Stream s = e.Open();
        var doc = new XmlDocument();
        doc.Load(s);
        return doc.DocumentElement?.GetAttribute("AppVersion") is { Length: > 0 } v ? v : "?";
    }

    private static string? TryReadVersion(string path)
    {
        try
        {
            var doc = new XmlDocument();
            doc.Load(path);
            return doc.DocumentElement?.GetAttribute("AppVersion") is { Length: > 0 } v ? v : null;
        }
        catch (Exception) { return null; }
    }

    private static void Error(string m) => MessageBox.Show(m, Title, MessageBoxButtons.OK, MessageBoxIcon.Error);
}
