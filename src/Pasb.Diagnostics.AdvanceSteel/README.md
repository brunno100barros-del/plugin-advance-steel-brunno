# PASB Diagnostics (add-in do Advance Steel 2027)

Diagnóstico **somente leitura** das propriedades dos objetos do Advance Steel. Projeto completo em [docs/DIAGNOSTIC-DESIGN.md](../../docs/DIAGNOSTIC-DESIGN.md).

> Status: compilado; **ainda não executado dentro do Advance Steel**. Os testes automáticos cobrem só o núcleo (`Pasb.Diagnostics.Core`).

## Comandos

| Comando | O que faz |
|---|---|
| `PASB_DIAG_VERSION` | Mostra a versão carregada |
| `PASB_DIAG_SEL` | Diagnostica os objetos selecionados |
| `PASB_DIAG_ALL` | Diagnostica todos os objetos do modelo |
| `PASB_EXPORTAR_IFC` | Exporta o modelo inteiro para IFC4 georreferenciado (versão 0.3.0) |
| `PASB_IMPORTAR_IFC` | Importa qualquer IFC (xBIM, fora do AutoCAD) como sólidos ou malhas, com níveis, cor por nível, layers e blocos (0.8.0). Ver [docs/IFC-IMPORT.md](../../docs/IFC-IMPORT.md) |
| `PASB_CONTRAVENTAMENTO` / `PASB_EDITAR_CONTRAVENTAMENTO` | Macro de contraventamento em X com barra redonda e esticador, todos os parâmetros editáveis e regeráveis (0.10.0). Ver [docs/BRACING.md](../../docs/BRACING.md) |
| `PASB_NUMERAR_ITENS` | Numeração itemizada (peças iguais = mesma marca) com prévia, filtros e confirmação; grava só o campo escolhido e um CSV de auditoria (0.9.0). Ver [docs/NUMBERING.md](../../docs/NUMBERING.md) |
| `PASB_LISTA_MATERIAL` | Lê o modelo (sem alterá-lo) e abre a janela de opções e insere no desenho as **tabelas de lista de material** escolhidas, lado a lado a partir do ponto clicado: lista detalhada (por comprimento), resumo de perfis (um item por perfil), parafusos, chumbadores e chapas por espessura (0.7.0). Único comando que grava no desenho, e só a tabela (Ctrl+Z desfaz). Versão 0.6.0 |
| `PASB_CONECTOR_IA` | Liga/desliga o Conector IA (versão 0.5.0): ver [docs/AI-CONNECTOR.md](../../docs/AI-CONNECTOR.md) |
| `PASB_IA_EXECUTAR` | Interno: executa os pedidos do Conector IA (não digitar) |

### Aba PASB (versão 0.4.0)
Ao abrir o Advance Steel 2027, aparece a aba **PASB** na faixa de opções com os botões **Exportar IFC**, **Diagnóstico seleção**, **Diagnóstico modelo**, **Conector IA** (desde a 0.5.0) e **Sobre**. Por isso o plugin carrega na inicialização. Ao abrir, ele só cria a aba e não lê o desenho.

### `PASB_EXPORTAR_IFC` (botão "Exportar IFC")
Abre a janela de exportação:

| Área | Opções |
|---|---|
| Formato | IFC4 (IFC2x3 aparece desabilitado: ainda não implementado) |
| Escopo | Modelo inteiro ou Seleção (pede a seleção depois de confirmar) |
| Dados do projeto | Nome do projeto, Site, Edifício |
| Objetos | Vigas e pilares, Chapas, Parafusos, Chumbadores, Soldas, Peças especiais, Ligações, Outros |
| Georreferência | Sem georreferência, SIRGAS 2000 / UTM (zona, hemisfério) ou Coordenadas X, Y, Z da obra (nome do sistema) |
| Ponto base | Botão **Selecionar...**: a janela some, você clica no ponto no modelo e a janela volta com as coordenadas (UCS convertida para globais) |
| Coordenadas reais | X/E, Y/N e Z, em **metros ou milímetros** (seletor de unidade, desde a 0.5.0; aceita vírgula ou ponto) |
| Ângulo | Rotação (anti-horária, do X do modelo até o Leste) ou Azimute (horário, do Norte até o +Y do modelo) |
| Arquivo de saída | Caminho do `.ifc`; o relatório `.report.json` é gravado ao lado |

- Erros (por exemplo, coordenada UTM implausível) aparecem no rodapé e impedem a exportação.
- A janela **lembra a última configuração** em `%APPDATA%\PASB\export-settings.json`.
- A leitura usa o mesmo caminho validado do `PASB_DIAG_ALL`: escrita descartada e `DBMOD` conferido. Se a verificação falhar, a exportação é interrompida.
- A saída do diagnóstico fica em `Documentos\PASB-Diagnostics\<data-hora>\`, para auditoria.

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
