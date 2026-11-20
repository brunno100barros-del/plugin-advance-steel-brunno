using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Pasb.Mcp;

/// <summary>
/// Registra (ou remove) o servidor "pasb" na configuração de usuário do Claude Code (~/.claude.json, chave mcpServers).
/// Só mexe na entrada "pasb"; o resto do arquivo é preservado. Faz cópia de segurança antes de gravar.
/// Nunca imprime o conteúdo do arquivo (ele pode conter credenciais).
/// </summary>
public static class Registration
{
    public const string ServerName = "pasb";

    public static string DefaultConfigPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude.json");

    /// <returns>Mensagem para o usuário.</returns>
    public static string Register(string configPath, string exePath)
    {
        JsonObject root = Load(configPath);
        JsonObject servers = root["mcpServers"] as JsonObject ?? new JsonObject();
        root["mcpServers"] = servers;
        bool existed = servers.ContainsKey(ServerName);
        servers[ServerName] = new JsonObject
        {
            ["type"] = "stdio",
            ["command"] = exePath,
            ["args"] = new JsonArray(),
            ["env"] = new JsonObject(),
        };
        string backup = Save(configPath, root);
        return (existed ? "Registro do servidor \"pasb\" atualizado" : "Servidor \"pasb\" registrado") +
               " na configuração do Claude Code." + (backup.Length > 0 ? " Cópia de segurança: " + Path.GetFileName(backup) + "." : "");
    }

    public static string Unregister(string configPath)
    {
        if (!File.Exists(configPath)) return "Configuração do Claude Code não encontrada; nada a remover.";
        JsonObject root = Load(configPath);
        if (root["mcpServers"] is not JsonObject servers || !servers.Remove(ServerName))
            return "O servidor \"pasb\" não estava registrado.";
        string backup = Save(configPath, root);
        return "Servidor \"pasb\" removido da configuração do Claude Code. Cópia de segurança: " + Path.GetFileName(backup) + ".";
    }

    private static JsonObject Load(string path)
    {
        if (!File.Exists(path)) return new JsonObject();
        string text = File.ReadAllText(path);
        if (text.Trim().Length == 0) return new JsonObject();
        try
        {
            return JsonNode.Parse(text, documentOptions: new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip }) as JsonObject
                ?? throw new InvalidDataException();
        }
        catch (Exception)
        {
            throw new InvalidDataException("A configuração do Claude Code não é um JSON válido; nada foi alterado.");
        }
    }

    private static string Save(string path, JsonObject root)
    {
        string backup = "";
        if (File.Exists(path))
        {
            backup = path + ".pasb-backup-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff");
            for (int n = 2; File.Exists(backup); n++) backup = path + ".pasb-backup-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") + "-" + n;
            File.Copy(path, backup, overwrite: false);
        }
        string tmp = path + ".pasb-tmp";
        File.WriteAllText(tmp, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));
        File.Move(tmp, path, overwrite: true);
        return backup;
    }
}
