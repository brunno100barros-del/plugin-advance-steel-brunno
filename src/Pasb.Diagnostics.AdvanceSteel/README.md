# PASB Diagnostics (add-in do Advance Steel 2027)

Diagnóstico **somente leitura** das propriedades dos objetos do Advance Steel. Projeto completo em [docs/DIAGNOSTIC-DESIGN.md](../../docs/DIAGNOSTIC-DESIGN.md).

> Status: compilado; **ainda não executado dentro do Advance Steel**. Os testes automáticos cobrem só o núcleo (`Pasb.Diagnostics.Core`).

## Comandos

| Comando | O que faz |
|---|---|
| `PASB_DIAG_VERSION` | Mostra a versão carregada |
| `PASB_DIAG_SEL` | Diagnostica os objetos selecionados |
| `PASB_DIAG_ALL` | Diagnostica todos os objetos do modelo |

Saída em `Documentos\PASB-Diagnostics\<data-hora>\`: `objects.jsonl`, `run.json` e `coverage.csv`.

## Segurança

1. Leitura dentro de `DocumentAccess(null, bReadOnly: true)`, sem `Commit()`.
2. Nenhum setter nem método de escrita é chamado (lista em `ApprovedMethods.cs`).
3. `DiscardAllChanges()` ao final.
4. `run.json` grava `DBMOD` antes e depois (`dbmodChanged`). Se mudar, a linha de comando mostra um aviso: feche o desenho **sem salvar**.

## Compilar e montar o bundle

Na raiz do repositório (não instala nada):

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File bundle\Build-Bundle.ps1
```

O bundle fica em `artifacts\PASB-Diagnostics.bundle`.

## Instalar (só com autorização e com o Advance Steel fechado)

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File bundle\Install-Bundle.ps1
```

Copia o bundle para `%APPDATA%\Autodesk\ApplicationPlugins\PASB-Diagnostics.bundle`. O manifesto restringe o carregamento ao Advance Steel 2027 e usa carregamento sob demanda: a DLL só entra na memória quando um comando `PASB_DIAG_*` é digitado.

## Remover

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File bundle\Uninstall-Bundle.ps1
```

Apaga só a pasta `PASB-Diagnostics.bundle`. As saídas em `Documentos\PASB-Diagnostics` são mantidas.

## Primeiro teste

Siga o protocolo da seção 10 de [DIAGNOSTIC-DESIGN.md](../../docs/DIAGNOSTIC-DESIGN.md): cópia do `Portal.dwg` do SDK, `DBMOD` antes e depois e fechar sem salvar.
