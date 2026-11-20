# ApiInventory

Inventário **somente leitura** das assemblies .NET do Advance Steel.

Lê os metadados dos arquivos `AS*Mgd.dll` com `System.Reflection.Metadata`. Nenhuma assembly da Autodesk é carregada no processo e nenhum código dela é executado. Não precisa do Advance Steel aberto.

## Requisitos

- .NET 10 SDK
- Advance Steel 2027 instalado (só para ter as DLLs no disco)

## Uso

Na raiz do repositório:

```powershell
dotnet run --project tools\ApiInventory -c Release -- --out inventory-output
```

Opções:

| Opção | Padrão |
|---|---|
| `--asm-dir` | `C:\Program Files\Autodesk\AutoCAD 2027\ADVS` |
| `--pattern` | `AS*Mgd.dll` |
| `--out` | `inventory-output` |

## Saídas

| Arquivo | Conteúdo |
|---|---|
| `api-2027.json` | Assemblies, tipos públicos, herança, interfaces e membros |
| `api-2027-members.csv` | Uma linha por membro (separador `;`, abre no Excel) |
| `api-2027-key-classes.txt` | Herança e membros das classes centrais |

A pasta `inventory-output/` está no `.gitignore`, porque o conteúdo deriva de arquivos da Autodesk.

## Matriz por classe

Depois de gerar o inventário, na raiz do repositório:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools\ApiInventory\New-ClassMatrix.ps1
```

Gera `docs/API-CLASS-MATRIX.md`. A coluna "MCP" vem de uma lista montada à mão dentro do script, a partir do código do AdvancedSteelMCP. O script precisa ficar salvo em UTF-8 **com BOM**, porque o Windows PowerShell 5.1 lê arquivos sem BOM como ANSI e estraga os acentos.

## Limitações

- Lista membros públicos e protegidos. Não diz se um getter funciona, tem efeito colateral ou depende de transação: isso só o diagnóstico em execução responde.
- Modificadores C++/CLI (`modopt`/`modreq`) são omitidos das assinaturas.
- Parâmetros genéricos aparecem como `!0` (tipo) e `!!0` (método).
- "Documentado" só indica presença no XML de IntelliSense instalado, não a qualidade da descrição.
