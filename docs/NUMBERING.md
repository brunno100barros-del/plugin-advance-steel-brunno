# Numeração itemizada (versão 0.9.0)

O botão **PASB > Numerar itens** (comando `PASB_NUMERAR_ITENS`) faz uma numeração parecida com a do Revit: **peças iguais recebem a mesma marca**.

> Status: lógica e janela testadas fora do Advance Steel (com as leituras reais do Portal e do DWG de teste). A **gravação no modelo ainda não foi testada dentro do Advance Steel**.

## Regras de item

| Categoria | Peças iguais = mesmo |
|---|---|
| Chapa | Espessura (`PlateBase.Thickness`), por exemplo `4.75` |
| Viga | Perfil (`ProfSectionName`), por exemplo `U100`. Vigas compostas com seção "Default" aparecem pelo tipo do composto, por exemplo `2U (composto)` |
| Parafuso | Diâmetro × comprimento + classe, por exemplo `Ø12 x 35 10.9` |
| Chumbador | Diâmetro × comprimento + classe (não validado em modelo real; desmarcado por padrão) |

- **Agrupar vigas curvas e chapas dobradas**: ligado, elas entram no mesmo item das retas/planas de mesmo perfil/espessura. Desligado, viram itens separados, por exemplo `U100 (curva)` e `4.75 (dobrada)`.
- A **ordem das categorias** (setas ↑ ↓) define a sequência das marcas. Dentro de cada categoria, a ordenação é por **nome** ou por **quantidade**, crescente ou decrescente, em ordem "natural" (`4.75 < 12.5`, `U80 < U100`).
- **Prefixo** e **Começar em** formam a marca, por exemplo `IT-1`, `IT-2`...

## Onde gravar

| Opção | Campo do Advance Steel |
|---|---|
| Numeração preliminar (nativo), **padrão** | `PreliminaryPartPrefix` = prefixo e `PreliminaryPartNumber` = número |
| Número do item | `ItemNumber` = marca completa |
| Nota | `Note` = marca completa |
| Atributo do usuário 1 a 10 | `SetUserAttribute(0..9)` = marca completa |

Os números oficiais de peça e de conjunto (`SinglePartNumber`, `MainPartNumber`) **não são alterados**: eles pertencem à numeração do próprio Advance Steel.

## Filtros

Campos: perfil/tipo, material, nota, papel (role), denotação, conjunto, atributos do usuário 1 a 10. Operadores: contém, não contém, igual a, diferente de, começa com, está vazio, não está vazio. Nenhum diferencia maiúsculas de minúsculas.

Só são numeradas as peças que passam em **todos** os filtros. Peça que fica de fora **não é alterada**. O botão "Mostrar o que saiu" lista as peças de fora e o motivo de cada uma.

## Segurança

1. A leitura usa o caminho validado do diagnóstico: transação descartada e `DBMOD` conferido. Se a verificação falhar, nada é gravado.
2. A janela mostra a prévia ao vivo. Nada é gravado antes de **Numerar** e da **confirmação**, que informa quantas peças e qual campo serão alterados.
3. A gravação acontece numa transação do Advance Steel (`DocumentAccess` com escrita e `Commit`) e altera **só o campo escolhido** das peças numeradas.
4. Antes de gravar, o valor anterior de cada peça vai para `Documentos\PASB-Numeracao\<data-hora>-<desenho>.csv` (handle, tipo, campo, valor anterior, valor novo, resultado). Esse arquivo é a auditoria e a referência para desfazer manualmente.
5. **Ctrl+Z** desfaz a numeração. Isso ainda precisa ser confirmado no primeiro teste no Advance Steel.

As opções ficam guardadas em `%APPDATA%\PASB\numbering-settings.json`.

## Primeiro teste no Advance Steel (roteiro)

1. Use uma **cópia** do DWG de teste.
2. Abra **PASB > Numerar itens**, confira a prévia e numere em "Atributo do usuário 10". Esse é o campo menos usado, e o mais seguro para o primeiro teste.
3. Confira algumas peças nas propriedades do Advance Steel e o CSV em `Documentos\PASB-Numeracao`.
4. Teste o **Ctrl+Z**.
5. Só depois use "Numeração preliminar".
