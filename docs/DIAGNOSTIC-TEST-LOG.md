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

## Teste 2 — 2026-11-20 — versão 0.1.1 — `Portal.dwg` (cópia, exemplo do SDK)

**Resultado: FALHA, com causa identificada. O AutoCAD encerrou com `eNotOpenForWrite` ao ler `FilerObject.ASId`. O modelo não foi salvo.**

### Contexto
Entre o Teste 1 e o Teste 2, a instalação do Advance Steel 2027 deste computador parou de abrir ("Problem loading acadres.dll resource file"). A causa foi um registro inconsistente no instalador da Autodesk: o pacote principal "Advance Steel 2027 – Português (Brasil)" constava como instalado no `Install.db` sem estar instalado no Windows. Foi resolvido com desinstalação completa e reinstalação. Não tem relação com o add-in (o erro ocorria antes de qualquer plugin carregar, e o bundle estava removido).

### O que foi feito
Bundle 0.1.1 (com `trace.log`) reinstalado; cópia limpa de `Portal.dwg`; um único objeto selecionado (viga, handle `52A`, tipo `StraightBeam`); `PASB_DIAG_SEL`.

### Evidência (`trace.log`)
```text
09:45:51.452 inicio PASB_DIAG_SEL versao 0.1.1+1523bcc... DBMOD=5
09:45:51.455 DocumentAccess(readOnly) aberto
09:45:51.459 ids: 1, ignorados: 0
09:45:51.463 [1/1] Open handle=52A
09:45:51.465 [1/1] tipo=StraightBeam
09:45:51.468   52A identidade: Handle
09:45:51.469   52A identidade: ASId
```
A linha seguinte (`GetUniqueId`) nunca foi gravada: a queda ocorreu durante a leitura de `ASId`. Mensagem do AutoCAD: `ERRO INTERNO: !dbobji.cpp@8753: eNotOpenForWrite`.

`Portal-copia.dwg` manteve a data de modificação original (20/05/2025): não foi salvo.

### Interpretação
- `FilerObject.ASId` é uma propriedade de leitura na API, mas, para este objeto, provocou uma tentativa de escrita. Hipótese: o ID é criado sob demanda na primeira consulta. **Não confirmado.**
- Consequência para o IFC: `ASId`, e possivelmente `GetUniqueId()`, podem não ser identificadores persistentes até que algo os crie. Não usar como chave de `IfcGlobalId` sem investigação específica.

### Correção aplicada (versão 0.1.2)
`FilerObject.ASId` entra na lista de exclusão (`ObjectReader.ExcludedGetters`), tanto na identidade quanto na leitura automática, com o motivo registrado no JSON.

## Teste 3 — 2026-11-20 — versão 0.1.2 — `Portal.dwg` (cópia, exemplo do SDK)

**Resultado: SUCESSO. Primeira leitura completa de um objeto, sem queda e sem alteração do modelo.**

### Execução
- Objeto: viga handle `52A`, `StraightBeam`, perfil U100 (Channels nach DIN), material S235JRG2.
- 241 leituras em 206 ms: 207 `Read`, 30 `Empty`, 2 `Null`, 1 `Unavailable` (ASId, excluído), 1 `ComplexNotSerialized`, **0 `ReadError`**.
- Segurança: `DBMOD` 5 antes e 5 depois (`dbmodChanged: false`); `DiscardAllChanges` executado com sucesso.

### Achados
1. **Identidade:** `GetUniqueId()` retornou `8acce620-b3c0-4c40-9dd0-2cb000b8bcbf` sem derrubar o AutoCAD. Diferente do `ASId`, foi lido em transação somente leitura. Persistência entre sessões ainda não verificada.
2. **Unidades (confirmadas por consistência):** `GetWeight(1)` = 15168,57 e `GetWeightPerMeter` = 10,6 com `GetLength` = 1430,997 mm → 10,6 kg/m × 1,431 m = 15,17 kg, logo `GetWeight(1)` está em **gramas**, como o MCP assume (`Objects.cs:174-175`). `SpecificGravity` = 7,85e-06 (kg/mm³); `Volume` em mm³.
3. **Comprimentos:** `SysLength` = 1500 (eixo) e `GetLength` = 1430,997 (físico); `GetLengthOffset` = 34,5 mm em cada ponta. As features `4EF` e `4F3` só aparecem com `GetFeatures(true)`.
4. **Ligação:** a viga é controlada por um `CompoundConstructionObject` (`DrivingPassiveConstructionObject` = `4E4`, `IsGeometryNotEditable` = true). `getAttributeStatus` retornou 1 para 26 atributos e 0 para 80: provável indicação dos atributos controlados pela ligação (a confirmar).
5. **Texto localizado:** números de peça não definidos vêm como `"indefinido"` (instalação pt-BR). O MCP só trata `"Not defined"` (`Objects.cs:391`), então em instalações em português ele não reconhece o marcador. O diagnóstico guarda o texto bruto.
6. **Marcadores preservados:** `Coating` = `"-"` e `PureRole` = `"-"` foram mantidos como texto; o MCP os converte em nulo.
7. **User Attributes:** os 10 índices foram lidos e registrados, todos vazios neste objeto.
8. **Fabricação/logística (MainAlias):** os 20 getters foram lidos (vazios ou valores padrão neste modelo de exemplo).
9. **Pendências de serialização:** `GetProfType` (`ProfileTypeU`) ficou como `ComplexNotSerialized`; `StraightBeam.Camber` e `GetConnection` retornaram nulo.

### Próximos passos
- Rodar em chapa, parafuso, chumbador, solda e ligação, ou `PASB_DIAG_ALL` no modelo inteiro.
- Conferir os valores com a janela de propriedades do Advance Steel.
- Serializar `ProfileType`.

## Teste 4 — 2026-11-20 — versão 0.1.2 — `Portal.dwg` inteiro (`PASB_DIAG_ALL`)

**Resultado: FALHA, com causa identificada. O AutoCAD caiu no primeiro objeto ao chamar `UserAutoConstructionObject.getPrimaryJoint`.**

### Evidência (`trace.log`)
- 26 objetos no modelo; o primeiro foi a ligação `305` (`UserAutoConstructionObject`).
- Leituras concluídas antes da queda: todos os 38 getters da ligação (inclusive `InputObjects`, `CreatedObjects`, `Drivers`, `Drivens`, `ParameterSet`, `Rule`, `NSAModuleName`, `NSAModuleInternalName`, `NSAModuleStandard`, `PrimaryJointName`, `CheckStatus`, `ApprovalStatus`) e todos os `getAttributeStatus`.
- Última linha: `305 UserAutoConstructionObject.getPrimaryJoint [method]`. Nenhum objeto chegou a ser gravado.

### Interpretação
`getPrimaryJoint()` provoca escrita no objeto, como o `ASId`. Os getters de leitura da ligação são seguros em transação somente leitura.

### Correção aplicada (versão 0.1.3)
`getPrimaryJoint` removido dos métodos aprovados e registrado como `Unavailable`, com o motivo, em cada ligação.

## Teste 5 — 2026-11-20 — versão 0.1.3 — `Portal.dwg` inteiro (`PASB_DIAG_ALL`)

**Resultado: FALHA parcial. 6 de 26 objetos gravados; queda numa viga composta.**

### Evidência
- Lidos e gravados: ligações `305`, `308` e `16A` (85 leituras cada), `RuleParameterSet` `307` e `30D`, e uma falha de abertura registrada sem queda (`30B`: `DatabaseManager.Open` retornou nulo).
- A ligação `308` ("createbeams") tem `Drivers` = `30B` e `CreatedObjects`/`Drivens` = `4CC`, `4D7`, `4E2`, `4ED`...; parâmetros em `RuleParameterSet` `507`.
- Queda no objeto `4CC` (`CompoundStraightBeam`), no getter `AtomicElement.AssemblyUsedForNumbering`, que **funcionou** no `StraightBeam` do Teste 3.

### Interpretação
As escritas internas dependem do tipo do objeto, não só do membro. Excluir membro a membro faria perder a leitura também nos objetos em que ela funciona.

### Decisão (desenvolvedor)
Passar a **transação com escrita permitida e descarte**: `DocumentAccess(doc, bReadOnly: false)`, nunca `Commit()`, `DiscardAllChanges()` no final, como o modo dry-run do AdvancedSteelMCP (`Infrastructure.cs:234-244`). Mantidos: nenhum setter ou método de escrita na lista aprovada, verificação de `DBMOD`, testes só em cópias, aviso na tela se o descarte falhar. `run.json` e cada objeto registram `transactionMode: "write-discarded"`, porque valores criados sob demanda (como IDs) podem não existir no modelo salvo.

Versão 0.2.0: `ASId`, `getPrimaryJoint` e `AssemblyUsedForNumbering` voltam a ser lidos nesse modo; continuam excluídos se o modo somente leitura for reativado.

## Teste 6 — 2026-11-20 — versão 0.2.0 (escrita com descarte) — `Portal.dwg` inteiro

**Resultado: FALHA no mesmo ponto do Teste 4. O modelo não foi alterado.**

### Evidência
- `trace.log`: `DocumentAccess aberto (modo write-discarded)`; ligação `305`: identidade (incluindo `ASId`, agora lido sem queda neste objeto), todos os getters e `getAttributeStatus` concluídos; última linha `UserAutoConstructionObject.getPrimaryJoint`.
- `Portal-copia.dwg`: mesmo SHA-256 do original e data de 20/05/2025 depois da queda.

### Interpretação
- `getPrimaryJoint()` derruba o AutoCAD **também com escrita permitida**. A causa não é só a transação somente leitura; o método não é seguro para este uso. Excluído em qualquer modo.
- O `ASId` foi lido sem queda numa ligação, mas no Teste 2 a queda foi numa viga. Ainda não há evidência de que o modo com descarte resolva o caso da viga.

### Correção aplicada (versão 0.2.1)
`getPrimaryJoint` excluído em qualquer modo, registrado como `Unavailable`.

## Teste 7 — 2026-11-20 — versão 0.2.1 (escrita com descarte) — `Portal.dwg` inteiro

**Resultado: SUCESSO. Modelo inteiro lido sem queda e sem alteração.**

### Execução
- 26 objetos solicitados: 22 gravados, 4 falhas de abertura (sem queda), 0 falhas de leitura de objeto.
- 2.716 leituras em 1,4 s: 2.365 `Read`, 309 `Empty`, 27 `Null`, 6 `Unavailable` (`getPrimaryJoint` excluído), 9 `ComplexNotSerialized`, **0 `ReadError`**.
- Segurança: `DBMOD` 5 antes e 5 depois; `DiscardAllChanges` = ok; depois de fechar sem salvar, `Portal-copia.dwg` com **o mesmo SHA-256 do original** e data de 20/05/2025.

### Cobertura por tipo
| Tipo | Objetos | Leituras |
|---|---|---|
| `StraightBeam` | 6 | 1.446 |
| `CompoundStraightBeam` | 3 | 732 |
| `UserAutoConstructionObject` | 3 | 255 |
| `CompoundConstructionObject` | 3 | 255 |
| `RuleParameterSet` | 7 | 28 |

- A viga composta `4CC`, que derrubou o Teste 5 em `AssemblyUsedForNumbering`, foi lida por completo: **o modo com descarte resolveu esse caso**.
- `ASId` lido sem queda em todos os objetos neste modo.
- Falhas de abertura: `30B`, `564`, `566` e `114` (`DatabaseManager.Open` retornou nulo). Tipo ainda não identificado; não derrubaram o AutoCAD.
- `ComplexNotSerialized`: só `Beam.GetProfType` (`ProfileType` e `ProfileTypeU`).

### Limite deste teste
`Portal.dwg` só tem vigas e ligações. **Não há chapas, parafusos, chumbadores nem soldas**; essas categorias ainda não foram testadas.

### Conclusão
A combinação escrita permitida + descarte + lista aprovada sem métodos de escrita + verificação de `DBMOD` foi validada neste modelo: leu tudo o que existia sem alterar o arquivo.

## Teste 8 — 2026-11-20 — versão 0.2.1 (escrita com descarte) — DWG de teste criado pelo desenvolvedor

**Resultado: SUCESSO. Chapa, parafusos, solda e ligações lidos sem queda e sem alteração do arquivo.**

### Modelo
DWG criado pelo desenvolvedor para teste (sem dados de cliente): uma chapa com 4 furos, um grupo de 4 parafusos e um cordão de solda.

### Execução
- 15 objetos solicitados: 10 gravados, 5 falhas de abertura (`83`, `CA`, `CC`, `CE`, `CF`; `DatabaseManager.Open` retornou nulo), 0 falhas de leitura.
- 903 leituras em 0,48 s: 813 `Read`, 82 `Empty`, 8 `Null`, **0 `ReadError`**.
- Segurança: `DBMOD` 0 antes e 0 depois; `DiscardAllChanges` = ok; arquivo gravado às 10:17:32, diagnóstico às 10:17:50, sem gravação posterior depois de fechar sem salvar.

### Cobertura por tipo
| Tipo | Objetos | Leituras |
|---|---|---|
| `Plate` | 1 | 221 |
| `FinitRectScrewBoltPattern` | 1 | 233 |
| `WeldLine` | 1 | 183 |
| `Connection` | 2 | 124 |
| `ConnectionConstructionObject` | 2 | 130 |
| `RuleParameterSet` | 2 | 8 |
| `ConstructionFeatureRule` | 1 | 4 |

### Achados
1. **Peso em gramas também em chapas e parafusos.** Chapa 196 × 100 × 4,75 mm = 93.100 mm³ × 7,85e-06 kg/mm³ = 0,731 kg, e `GetWeight()` retornou 730,83. Parafusos: `GetWeight()` = 149,44 para 4 conjuntos M12×35 (cerca de 37 g cada).
2. **Parafusos:** norma `14399-4`, classe `10.9`, Ø 12, comprimento 35, conjunto `Mu2S`, cabeça e porca Ø 23,91, comprimento de aperto 4,75, tolerância de furo 1, `Annotation` = `"Bohrbild"`, montagem `kOnSite`. `GetHoles` aponta para o objeto de furos `DB` (`ConnectionHolePlate`).
3. **Solda:** `kTWeld`, espessura 4, comprimento 91,06, montagem `kInShop`. Para o lado inferior, `GetWeldType(kLower)` retornou o valor 0 (não definido no enum, ou seja, sem cordão inferior), mas `GetSeamThickness(kLower)` retornou 4. **A espessura inferior não deve ser usada quando o tipo inferior for 0.**
4. **Ligações de chapa/parafuso/solda:** objetos `Connection` (`kWelded`, `kBolted`) e `ConnectionConstructionObject`, cujos `Drivers` apontam para a chapa e a `Connection`, e cujos `Drivens` apontam para os furos. Esses tipos não estavam na matriz por classe.
5. Falhas de abertura recorrentes (9 objetos em dois modelos): tipo ainda não identificado.

### Ainda não testado
Chumbadores, peças especiais, vigas poligonais/curvas, chapas dobradas, grades, concreto e eixos.

### Observação não relacionada
Os eventos do Windows mostram a desinstalação do "AutoCAD 2026 - English" às 22:58, antes do teste, com falha no componente "ACAD Private", e um travamento do `acad.exe` no mesmo horário. Não tem relação com o add-in, que só carrega no Advance Steel 2027.
