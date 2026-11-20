# Macro: contraventamento em X (versão 0.10.0)

Os botões **PASB > Macros > Contraventamento em X** e **Editar contravent.** (comandos `PASB_CONTRAVENTAMENTO` e `PASB_EDITAR_CONTRAVENTAMENTO`) criam um contraventamento em X com barra redonda e esticador. O detalhe foi reproduzido a partir do **resultado** de uma macro de terceiros que o usuário criou no desenho e o PASB leu pelo Conector IA. O código e os blocos são **próprios do PASB**, sem nenhuma dependência de outro plugin.

> Status: geometria testada fora do Advance Steel (reproduz as medidas do exemplo com 0,15 mm de tolerância; 62 peças, como no exemplo). A **criação das peças dentro do Advance Steel ainda não foi testada**.

## Uso

1. Clique o **ponto 1** e o **ponto 2**, que são cantos opostos do retângulo. Tudo é criado no **plano XY, na cota Z do ponto 1**. Para contraventamento lateral, mude o Z (UCS) do projeto.
2. Na janela, ajuste qualquer parâmetro. A prévia em planta e a validação são atualizadas na hora.
3. **Gerar**. Para mudar depois: **Editar contravent.**, selecione qualquer peça, ajuste e clique em **Regerar** (as peças antigas são apagadas e geradas de novo).

## Montagem de cada diagonal

| Trecho | Peças (valores padrão) |
|---|---|
| Canto | Chapa de base 8 × 150 × 150 (topo no Z do ponto), gusset 6,35 × 160 × 50 sobre ela, alinhado com a diagonal, a 54,5 mm do ponto. 1 parafuso Ø12,7 (ASTM A325, 10.9) a 30 mm do início do gusset |
| Barra | RB 1/2" (`AISC RoundBar#@§@#RB 1/2`), ASTM A36, apoiada 100 mm sobre o gusset e soldada (cordão de 5 mm) |
| Emenda | Barra lisa ↔ barra roscada (mesma seção, 125 mm), com 2 talas de 60 mm lado a lado, soldadas (cordão de 4 mm) |
| Esticador | Cantoneira `AISC 15.0 Angle equal#@§@#L2X2X1/8` × 140 mm com 2 chapas de topo 4,75 × 47 × 45 (recuo de 10 mm), soldadas (cordão de 4 mm). As roscas entram 35 mm além da chapa de topo. **Porca e arruela dos dois lados** de cada chapa |
| Posição | Centro do esticador a **65 %** da diagonal (ou em mm a partir do canto 1) |

Em **X**, a diagonal 2 liga os outros dois cantos do retângulo. A validação recusa:
- esticadores que se chocariam no cruzamento;
- esticador em cima de gusset;
- parafuso sem chapa de base;
- pontos alinhados em X ou em Y.

## Porcas e arruelas

São blocos AutoCAD **criados pelo PASB** no próprio desenho (sólidos 3D com furo), com nomes que trazem as medidas, por exemplo `PASB_PORCA_S22.2_H12.3_D13.5` e `PASB_ARRUELA_D27_d13.5_t3.4`. Entram como **peças especiais** do Advance Steel. Medidas padrão (1/2"):
- porca A194-2H: entre faces 22,2 e altura 12,3;
- arruela F436: Ø 27 / 13,5 × 3,4.

## Parâmetros guardados

- Cada peça recebe XData `PASB_CV` com o identificador do contraventamento.
- Os parâmetros, os 2 pontos e a lista de peças ficam em `PASB_CONTRAVENTAMENTOS` (dicionário do desenho). É isso que permite **editar e regerar**.
- A última configuração usada fica em `%APPDATA%\PASB\bracing-settings.json`.

## Segurança

- Se uma peça principal falhar, por exemplo um perfil que não existe no catálogo, **nada é gravado** e o erro é mostrado. Peças principais são barras, roscas, cantoneira e gussets.
- Se falhar uma peça secundária (talas, solda, parafuso, porca), ela é pulada, as outras são criadas e um aviso é mostrado.
- O desenho precisa estar em **milímetros**.

## Primeiro teste no Advance Steel (roteiro)

1. Num desenho **novo** em milímetros, rode **Contraventamento em X** com 2 pontos a ~1.000 × 1.300 mm, para comparar com o exemplo.
2. Confira: barras, gussets, chapas de base, parafusos, esticadores, porcas e arruelas, soldas (no gerenciador de conexões do Advance Steel).
3. Teste **Editar contravent.**: mude a posição do esticador e regere.
4. Teste o **Ctrl+Z**.

## Possíveis ajustes após o primeiro teste

- Orientação da cantoneira do esticador em relação à barra (depende do ponto de inserção do perfil no Advance Steel).
- Lado em que as chapas ficam em relação ao plano (`Portioning`).
- Comprimento do parafuso: o Advance Steel calcula pela espessura das chapas ligadas.
