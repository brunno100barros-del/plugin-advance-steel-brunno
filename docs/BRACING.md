# Macro: contraventamento em X (versão 0.11.0)

A partir da 0.11.0, o contraventamento é uma **ligação nativa do Advance Steel** (interface `IRule` do SDK): aparece como caixa cinza de ligação, edita com **duplo clique** (abas de parâmetros, "Atualizar agora", "Automático") e se recalcula quando as entradas mudam. O código, os textos, as imagens e os blocos de porca e arruela são **próprios do PASB**, sem dependência de outro plugin.

> Status: geometria testada fora do Advance Steel. A **ligação nativa ainda não foi testada dentro do Advance Steel**.

## Registro (uma vez por versão)

Com o Advance Steel **fechado**, rode `Registrar-Macros-PASB.cmd` (vem no pacote de instalação). Ele usa `Contents\Joints\Register-NativeJoints.ps1` e:

1. copia `PasbJoints.dll` para `C:\Program Files\Autodesk\AutoCAD 2027\ADVS` (o Windows pede administrador só para isso);
2. registra a ligação no banco `AstorRules` (`RulesDllSigned` 880000, `HRLDefinition` 880000, nome interno `PasbContraventamento`, Tech 2, Version 31);
3. grava os nomes das abas e os textos dos cliques no banco `AstCrtlDb` (`ErrorMessages` 880000–880022, Note `PASB`).

Antes de alterar os bancos, faz backup completo de cada um em `%LOCALAPPDATA%\PASB\backup-bancos\<data>`. Só mexe nas linhas do PASB (faixa 880000–880999). `Remover-Macros-PASB.cmd` desfaz tudo.

## Uso

1. **PASB > Contraventamento em X** (ou o comando `AstM4CrConByVB PasbContraventamento`).
2. Clique o **1º canto** e o **canto oposto**. O contraventamento fica no **plano XY da UCS atual**, na cota Z do 1º clique. Para contraventamento lateral, mude a UCS.
3. Selecione as **vigas de apoio** (banzos) para soldar, ou ENTER para pular. Em cada canto que cair sobre uma viga, a chapa de base é **recortada na largura do banzo** e soldada na viga (solda h4).
4. Para mudar: **duplo clique na caixa cinza** da ligação.

As entradas da ligação são 2 pontos do usuário (um em cada clique) e as vigas escolhidas. Mover os pontos ou as vigas recalcula a ligação.

## Abas

| Aba | Parâmetros |
|---|---|
| 1 - Geral | Diagonais (X, só a 1, só a 2); posição do esticador: % da diagonal ou mm do clique, até a ponta ou o centro, a partir do 1º ou do 2º clique (padrão: ponta a 300 mm do 1º clique) |
| 2 - Chapa de base | a0 criar, a1 comprimento, a2 largura, a3 espessura, a4 centro a partir do clique, a8 topo abaixo do clique, material (catálogo) |
| 3 - Gusset | b1 comprimento, b2 largura, b3 espessura, b4 recuo do clique, b7 barra apoiada |
| 4 - Parafuso | criar; norma, classe, diâmetro e conjunto (seletor de parafuso do Advance Steel); c1 distância à ponta do gusset |
| 5 - Barras | perfis e materiais **do catálogo** da barra, da barra roscada e das talas; d2 comprimento da rosca, d3 rosca dentro do esticador, e0 talas, e1 comprimento das talas |
| 6 - Esticador | cantoneira e material (catálogo), f1 rotação, f3 comprimento, f4/f5/f6 chapa de topo, f8 recuo da chapa de topo |
| 7 - Porcas e arruelas | g0 criar; medidas da porca e da arruela (blocos PASB) |
| 8 - Soldas | oficina ou montagem; h1 barra x gusset, h2 emenda, h3 cantoneira x chapa de topo, h4 chapa de base x viga de apoio (liga/desliga e espessura) |

Novos contraventamentos começam com a última configuração salva pela janela antiga (`%APPDATA%\PASB\bracing-settings.json`) ou com os padrões.

## Porcas e arruelas

Blocos AutoCAD **criados pelo PASB** no desenho (sólidos 3D com furo), com as medidas no nome (`PASB_PORCA_S22.2_H12.3_D13.5`, `PASB_ARRUELA_D27_d13.5_t3.4`). Entram como peças especiais.

## Versão simples (antes da 0.11.0)

Sem o registro, o botão usa a versão simples (peças soltas, sem caixa de ligação), avisando na linha de comando. Contraventamentos da versão simples editam com **botão direito > PASB: Editar contraventamento**.

## Primeiro teste no Advance Steel (roteiro)

1. Num desenho em milímetros com 2 vigas paralelas, rode **Contraventamento em X**, clique 2 cantos sobre os banzos e selecione as vigas.
2. Confira: caixa cinza de ligação, chapas recortadas no banzo, soldas na viga, esticador a 300 mm.
3. Duplo clique na caixa: confira as 8 abas, as imagens e os seletores de perfil, material e parafuso.
4. Mude um parâmetro e veja a ligação se atualizar. Mova uma viga e veja o recálculo.

## Possíveis ajustes após o primeiro teste

- Orientação da cantoneira (agora ajustável na aba 6, f1).
- Lado das chapas em relação ao plano (`Portioning`).
- Nome do conjunto de parafuso (`NaW`) conforme o catálogo instalado.

## Ligação nativa não registrada (0.14.2)

O botão usa a ligação nativa (caixa cinza, duplo clique para editar) quando `PasbJoints.dll` está na pasta do Advance Steel e é a mesma desta versão do plugin. Até a 0.14.1, sem ela, o comando caía **em silêncio** na versão simples antiga (sem caixa de ligação). Agora:

- **Não registrada:** aparece um aviso explicando como registrar, e a versão simples só é usada se você clicar em **Sim**.
- **De outra versão:** aparece um aviso para registrar de novo, e a ligação nativa registrada é usada.

Os instaladores da 0.14.2 registram as ligações sozinhos (ver INSTALACAO.md, "Macros de ligação").
