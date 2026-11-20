# Conector IA (versão 0.5.0)

Com o Conector IA, um assistente de IA (Claude Code) **lê o modelo do Advance Steel e exporta IFC pelo próprio PASB**. Todo o código é próprio, sem bibliotecas externas, e o AdvancedSteelMCP não é usado.

> Status: compilado e testado **fora** do Advance Steel (14 testes, incluindo um de ponta a ponta com o `Pasb.Mcp.exe` real e um Advance Steel simulado). **Ainda não testado dentro do Advance Steel.**

## Como funciona

```
Claude Code ──stdio (JSON-RPC 2.0 / MCP)──► Pasb.Mcp.exe ──TCP 127.0.0.1 + token──► Plugin PASB (dentro do Advance Steel)
                                                                                     └─ comando interno PASB_IA_EXECUTAR
                                                                                        (mesmo caminho validado do PASB_DIAG_ALL)
```

1. No Advance Steel, o botão **PASB > Conector IA** (comando `PASB_CONECTOR_IA`) liga a ponte local:
   - escuta **só em 127.0.0.1** (não aceita conexões de outros computadores), numa porta aleatória;
   - gera um **token aleatório** de 256 bits a cada vez que é ligada;
   - grava o arquivo de descoberta `%LOCALAPPDATA%\PASB\conector-ia.json` (porta + token). O arquivo é apagado ao desligar e ao fechar o Advance Steel. Ele contém o token: não compartilhe nem exiba.
2. O Claude Code inicia o `Pasb.Mcp.exe` (instalado em `Contents\Mcp` do bundle), que lê o arquivo de descoberta a cada chamada e repassa a ferramenta à ponte.
3. O plugin coloca cada leitura numa fila e dispara o comando interno `PASB_IA_EXECUTAR`. Ele roda no contexto de comando do AutoCAD, como o `PASB_DIAG_ALL` (transação descartada, `DBMOD` conferido). Se o usuário estiver no meio de um comando, o pedido **espera**: o comando do usuário nunca é cancelado.
4. Clicar de novo no botão desliga a ponte.

## Ferramentas (lista fechada)

| Ferramenta | O que faz | Altera algo? |
|---|---|---|
| `pasb_status` | Versão do plugin, desenho ativo, unidade | Não |
| `pasb_read_model` | Lê todos os objetos; devolve contagens por classe e categoria e uma lista (handle, classe, categoria, perfil, material...) | Não |
| `pasb_get_object` | Todas as propriedades de um objeto pelo handle, com valor bruto, valor normalizado (SI) e situação de leitura, mais a geometria | Não |
| `pasb_export_ifc` | Exporta o modelo inteiro para IFC4 com georreferência opcional (SIRGAS/UTM ou X,Y,Z; unidade m ou mm) | Só grava o `.ifc` e o `.report.json` |

- O plugin **recusa** qualquer método fora da lista, e o servidor MCP também.
- `pasb_export_ifc` **não sobrescreve** arquivo existente sem `overwrite=true`. Exige caminho completo e recusa parâmetros desconhecidos. Os campos omitidos vêm da última configuração da janela "Exportar IFC", que não é alterada.
- Se a verificação de integridade (`DBMOD`/descarte) falhar, a ferramenta devolve erro e pede para fechar o desenho **sem salvar**, como os comandos manuais.
- Cada leitura grava a saída de auditoria em `Documentos\PASB-Diagnostics\<data-hora>\`.

## Instalação

1. Feche o Advance Steel **e** o Claude Code. O instalador recusa se o `Pasb.Mcp.exe` estiver em uso.
2. `Instalar-PASB.cmd` (monta e instala o bundle 0.5.0).
3. **Uma vez:** `Registrar-Conector-IA.cmd`. Ele grava a entrada `mcpServers.pasb` em `~\.claude.json`, a configuração de usuário do Claude Code:
   - altera **só** a entrada `pasb` e preserva o resto do arquivo;
   - faz antes uma cópia de segurança `~\.claude.json.pasb-backup-<data-hora>`;
   - se o arquivo não for um JSON válido, não altera nada.
4. Abra o Claude Code: o servidor `pasb` aparece com as 4 ferramentas.
5. No Advance Steel, abra o desenho e clique em **PASB > Conector IA**.

Para remover o registro: `Remover-Conector-IA.cmd`.

## Teste dentro do Advance Steel (roteiro)

1. Use uma **cópia** do DWG de teste.
2. Ligue o Conector IA. O botão fica verde e mostra "(ligado)".
3. Peça ao Claude: `pasb_status`, depois `pasb_read_model`, `pasb_get_object` com um handle da lista e `pasb_export_ifc` para uma pasta de teste.
4. Confira `DBMOD` (deve continuar igual) e feche o desenho **sem salvar**.

## Limitações conhecidas

- Uma leitura por vez. Um segundo pedido simultâneo recebe "outro pedido em andamento".
- Tempo máximo por pedido: 10 minutos.
- Exportação por seleção não está disponível pelo conector, porque exigiria clicar no modelo. O conector exporta sempre o modelo inteiro, filtrado por tipo de objeto.
- Ferramentas de escrita (criar ou alterar peças) **não existem**, por decisão de projeto.
