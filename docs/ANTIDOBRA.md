# Macro: sistema antidobra (correntes de terça) — referência

Anotações da ligação **"SteelX - Sistema Antidobra"** criada pelo usuário no desenho de teste, em 2026-11-21, para reproduzir no PASB como ligação nativa própria (sem código, textos ou imagens do SteelX). Os valores abaixo são os que estavam na janela da ligação.

## Peças criadas pelo SteelX (lidas do modelo)

Uma ligação (`UserAutoConstructionObject`, status Partial) com 52 sub-ligações e:

| Qtde | Peça | Perfil | Papel (Role) |
|---|---|---|---|
| 3 | barra lisa da agulha / tirante | AISC RoundBar RB 3/8 | Cold_Rolled_Strut |
| 4 | barra lisa do tirante diagonal | AISC RoundBar RB 3/8 | Cold_Rolled_Diag_Tie |
| 14 | pontas roscadas | SteelX Threaded Rod RB 3/8 | ThreadedRod |
| 3 | barra rígida | L2X2X1/8 | Cold_Rolled_Strut |
| 6 | cantoneiras de ligação / do tirante | L2X2X1/8 | Cold_Rolled_Acc |
| 56 | porcas e arruelas (peça especial) | — | Nut |
| 6 | parafusos | A325 1/2" 10.9 NaW | Bolt |
| 20 | soldas pontuais 4 mm | — | Weld |

Material de tudo: A36.

## Parâmetros (abas)

### 1. Filas
1. Distribuição das filas: **Número de fileiras** (ou espaçamento)
2. Nº de fileiras: **3**
3. Espaçamento: **1500**
4. Base do cálculo: **Vão entre eixos dos pórticos**
5. Vãos centrais (0 = não): **0**
6. Vão central: **1500**

### 2. Entreterças
1. Tipo das entreterças: **Personalizado**
2. Rígidas (ex.: 1 6 T): **T** (T = todas? / topo)
3. Sem agulha (ex.: 5 8): vazio

### 3. Barra da agulha
1. Perfil da barra lisa: **RB 3/8** (catálogo)
2. Material da lisa: **A36**
3. Perfil da barra roscada: **Br. Rosc. 3/8"** (catálogo)
4. Material da roscada: **A36**
5. Rosca em cada ponta: **100**
6. Sobra antes (0 = auto): **0**
7. Sobra depois (0 = auto): **0**
8. Emenda lisa/roscada: **De topo** (ou por traspasse)
9. Cordão (0 = 5φ): **0**
10. Espaço cordões (0 = 5φ): **0**
11. Folga da lisa até a alma: **50**

### 4. Porcas e arruelas
1. Lado da porca: **Dos dois lados** (antes / depois / dos dois lados)
2. Lançar porca: **sim**
3. Porca: **Porca 3/8" A194-2H**
4. Porca: outro .dwg: vazio
5. Lançar arruela: **sim**
6. Arruela: **Arruela 3/8" F436**
7. Arruela: outro .dwg: vazio

### 5. Agulha dupla
1. Agulha dupla cruzada: **não**
2. Cota da mesa superior: **50**
3. Cota da mesa inferior: **50**

### 6. Barra rígida
1. Perfil da rígida: **L2X2X1/8**
2. Material: **A36**
3. Folga até a alma: **2**
4. Altura da face (0 = h/2): **0**
5. Posição da L: **aba em cima, abre p/ lado padrão**
6. Desloc. no suporte (+ B): **0**

### 7. Cantoneira de ligação
1. Cantoneira: **L2X2X1/8**
2. Material: **A36**
3. Comprimento: **100**
4. Costas do suporte ao paraf.: **28**
5. Cantoneira na rígida: **Por fora da rígida**
6. Ligação na rígida: **Soldada na rígida** (ou parafusada)
7. Ponta ao parafuso: **35**
8. Tipo de solda: **Solda de canto**
9. Espessura da solda: **4**
10. Suporte em chapa: **não**
11. Espessura da chapa: **6.35**
12. Largura da chapa: **70**
13. Comprimento da chapa: **140**

### 8. Onde vai o tirante
1. Vãos (ex.: 1 3): **1 2 3 4**
2. Entreterças (ex.: 1 5 T): **T**
3. Direção (todos): **Subindo** (subindo / descendo / em X)
4. Direção por tirante (S D X): **S S D D**
5. Distância da fila: **100**
6. Recuo do pórtico: **100**

### 9. Barra e ponta do tirante
1. Fixação da ponta: **Furo na alma + barra roscada e porca** (ou cantoneira com furo oblongo)
2. Perfil do tirante (lisa): **RB 3/8**
3. Material da lisa: **A36**
4. Perfil da ponta roscada: **Br. Rosc. 3/8"**
5. Material da roscada: **A36**
6. Rosca da ponta: **75**
7. Rosca atrás da alma: **20**

### 10. Cantoneira e emenda
1. Cantoneira do tirante: **L2X2X1/8**
2. Material: **A36**
3. Comprimento: **75**
4. Emenda lisa/roscada: **De topo**
5. Cordão (0 = 5φ): **0**
6. Espaço cordões (0 = 5φ): **0**
7. Folga da lisa até a alma: **50**

### 11. Furos da agulha
1. Forma: **Redondo** · 2. Diâmetro: **10.82** · 3. Comprimento: **30** · 4. Direção: **Ao longo da terça**
5. Posição na alma: **Centro da alma** · 6. Cota até a mesa: **50**
7. Desloc. entreterça par: **-40** · 8. Desloc. entreterça ímpar: **40**
9. Dupla: forma **Oblongo** · 10. diâmetro **10.82** · 11. comprimento **30** · 12. direção **Na altura da alma**

### 12. Furos do tirante
1–4. Furo e porca: **Redondo**, 10.82, 30, ao longo da terça
5–8. Alma: **Oblongo**, 10.82, 30, ao longo da terça
9–12. Aba: **Oblongo**, 10.82, 30, ao longo da aba

### 13. Parafusos na terça
Norma **ASTM A325**, grupo **10.9**, diâmetro **1/2 inch**, conjunto **NaW**
1. Nº de parafusos: **1** · 2. Espaçamento: **40** · 3. Folga do furo: **1.3** · 4. Forma: **Redondo** · 5. Comprimento do oblongo: **30**

### 14. Parafusos na rígida
Norma **ASTM A325**, **10.9**, **1/2 inch**, **NaW**
1. Nº de parafusos: **1** · 2. Espaçamento: **40** · 3. Furos na rígida: **Vertical (na altura)** · 4. Folga: **1.3** · 5. Forma: **Redondo** · 6. Comprimento do oblongo: **30**

### 15. Biblioteca
(não mostrada)

## Medidas da ligação de referência (lidas do IFC exportado pelo PASB)

Água com 3 terças 75x40x15x2 (alma de 2 mm, a 751 e 1667 mm uma da outra) entre pórticos a 3285 mm:

- **Filas** em 1/4, 2/4 e 3/4 do vão entre eixos (821,25 mm).
- **Agulha** (entreterça 1, ímpar): 40 mm da fila para o pórtico B; no centro da alma; lisa começa ~25 mm depois da face da alma; rosca de 100 mm atravessa a alma e sobra ~75 mm do lado de fora. Solda entre a lisa e cada rosca (de topo).
- **Rígida** (entreterça de cima = T, par): parafuso a 40 mm da fila para o pórtico A; costas da cantoneira de ligação a 28 mm do parafuso (lado B); rígida encostada por fora, aba de cima na metade da altura da alma, 2 mm da alma em cada ponta; solda rígida x cantoneira; 1 parafuso 1/2" na alma.
- **Tirantes** (entreterça T, vãos 1 a 4, S S D D): ponta de baixo a 100 mm do pórtico/fila, ponta de cima a 100 mm da fila/pórtico do outro lado do vão; ponta roscada de 75 mm **perpendicular à alma**, começando 20 mm atrás da alma, soldada na diagonal lisa.
- **Porcas e arruelas** dos dois lados de cada alma (56 no total); **20 soldas**; **14 furos**.

## Implementação no PASB (versão 0.12.0)

Ligação nativa **"PASB - Sistema antidobra"** (`PasbAntidobra`, `Pasb.Joints.SagRodJoint`), geometria em `src/Pasb.Connector/SagRods.cs`, com teste que reproduz as quantidades da referência (3 agulhas, 3 rígidas, 6 cantoneiras, 4 tirantes, 14 roscas, 56 porcas/arruelas, 20 soldas, 6 parafusos, 14 furos).

**Uso (0.12.1):** PASB > Sistema antidobra (ou PASB > Menu de ligações). **Clique no 1º banzo** (pórtico A), **clique no 2º banzo** (pórtico B) e **selecione as terças** entre eles (ENTER). O plano do telhado vem dos banzos: Y ao longo do 1º banzo, subindo; X do banzo A para o B. Vigas selecionadas que não estão ao longo das terças (diagonais, montantes) são ignoradas. Ao terminar, a janela da ligação abre sozinha (comando `AstM4ChangeUserConstructionObject` com a ligação nova selecionada; se não abrir, duplo clique na caixa cinza). A última configuração vira o padrão das próximas (`%APPDATA%\PASB\antidobra-settings.json`). Registro do cálculo: `%LOCALAPPDATA%\PASB\antidobra.log`.

**Abas:** as mesmas 13 da referência (Filas, Entreterças, Barra da agulha, Porcas e arruelas, Agulha dupla, Barra rígida, Cantoneira de ligação, Onde vai o tirante, Barra e ponta do tirante, Furos da agulha, Furos do tirante, Parafusos na terça, Parafusos na rígida), com perfis, materiais e parafusos do catálogo do Advance Steel. Porcas e arruelas são blocos próprios do PASB (medidas na aba 4).

**Ainda não feito (fica para depois do primeiro teste):**
- vãos centrais (aba Filas, itens 5 e 6);
- ponta do tirante por cantoneira com furo oblongo (só "furo na alma + barra roscada e porca");
- suporte em chapa (aba Cantoneira de ligação, itens 10 a 13);
- furo oblongo nos parafusos e furos na rígida na horizontal;
- biblioteca de porcas/arruelas em .dwg (usa os blocos PASB).

## Ligação nativa não registrada (0.14.2)

Sem `PasbJoints.dll` na pasta do Advance Steel, o Sistema antidobra não funciona: até a 0.14.1 só aparecia uma linha na linha de comando. Agora aparece um aviso com o passo a passo para registrar. Os instaladores da 0.14.2 registram as ligações sozinhos (ver INSTALACAO.md, "Macros de ligação").
