# Registro de testes do diagnóstico no Advance Steel

Cada execução real do add-in `PASB Diagnostics` é registrada aqui, com evidência e sem dados de cliente.

## Teste 1 — 2026-11-19 — versão 0.1.0 — `Portal.dwg` (cópia, exemplo do SDK)

**Resultado: FALHA. O AutoCAD encerrou com erro fatal durante `PASB_DIAG_SEL`. O modelo não foi alterado.**

### O que foi feito
1. Bundle 0.1.0 instalado em `%APPDATA%\Autodesk\ApplicationPlugins\PASB-Diagnostics.bundle`.
2. Desenvolvedor abriu `Documentos\PASB-Teste\Portal-copia.dwg` no Advance Steel 2027.
3. `PASB_DIAG_VERSION` executou (aparece no histórico de comandos do relatório CER).
4. Seleção de objetos e `PASB_DIAG_SEL`.

### O que aconteceu
- Janela "Erro ao cancelar AutoCAD" com a mensagem `ERRO INTERNO: !dbobji.cpp@8753: eNotOpenForWrite`.
- Relatório CER do AutoCAD (`%LOCALAPPDATA%\Autodesk\CER\...\cer.log`): último comando `PASB_DIAG_SEL` às 23:03:06; código de exceção `0xE0000001` (erro fatal do AutoCAD), módulo `KERNELBASE.dll`.
- Saída do diagnóstico: pasta `PASB-Diagnostics\20261119-230306` criada, `objects.jsonl` com 0 bytes e sem `run.json`. A queda ocorreu durante a leitura, antes de qualquer objeto ser gravado no disco (o arquivo estava em buffer).

### Verificação de integridade
- `Portal-copia.dwg`: SHA-256 (16 primeiros caracteres) `B6F318BA759E14F0` antes e depois, mesma data de modificação. **O arquivo não foi salvo nem alterado.**
- Restaram `Portal-copia.dwl` e `.dwl2`, arquivos de trava deixados pelo encerramento abrupto.

### Interpretação
- `eNotOpenForWrite`: alguma chamada tentou **modificar** um objeto aberto **somente para leitura**. A primeira barreira de segurança (transação `DocumentAccess` em modo leitura) bloqueou a escrita, mas o AutoCAD trata essa situação como erro fatal.
- Hipótese principal: um getter ou método de "leitura" que calcula um valor e o grava em cache no próprio objeto (candidatos: área de pintura, peso, nomes DSTV/externos, corpo sólido, quantidade no modelo, ou algum getter da leitura automática). **Não confirmado.**
- **Lição:** erros fatais do AutoCAD (`0xE0000001`) não passam por `catch` nem `finally`. A proteção "falha isolada" (DIAGNOSTIC-DESIGN.md, seção 9.2, item 3) não cobre esse caso.

### Correção aplicada (versão 0.1.1)
- `trace.log` na pasta de saída: uma linha gravada e enviada ao disco **antes** de cada leitura. Após uma queda, a última linha identifica a leitura culpada.
- `objects.jsonl` gravado no disco a cada objeto, para preservar o que foi lido antes de uma queda.

### Próximo passo
Repetir o teste com a versão 0.1.1 numa cópia limpa do `Portal.dwg` e incluir a leitura identificada pelo `trace.log` na lista de exclusão, com esta evidência.

### Observação não relacionada
Os eventos do Windows mostram a desinstalação do "AutoCAD 2026 - English" às 22:58, antes do teste, com falha no componente "ACAD Private", e um travamento do `acad.exe` no mesmo horário. Não tem relação com o add-in, que só carrega no Advance Steel 2027.
