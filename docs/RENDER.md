# Render e vista realista na prancha (versão 0.13.0)

Usa o motor gráfico do próprio AutoCAD em que o Advance Steel roda. Não instala nenhum programa.

## PASB > Renderizar

1. Deixe na tela a vista que quer renderizar (aba Modelo, ou dentro de uma janela de vista do layout).
2. Clique **PASB > Renderizar**. Abre a janela **PASB — Renderizar**, que já renderiza a vista atual.
3. Na janela:
   - **Modo:** "Realista (rápido)" usa o estilo visual Realista; "Render completo" usa materiais e luzes do desenho (se falhar, a janela mostra a vista realista e avisa).
   - **Tamanho** (1280x720 a 3840x2160) e **Fundo** (branco, cinza ou preto), depois **Renderizar** de novo.
   - **Salvar imagem...** (PNG ou JPG), **Copiar** (área de transferência) e **Inserir na prancha...** (pede onde salvar, fecha a janela e pede dois cantos para colocar a imagem no espaço atual).

O render é feito na memória: **nada é gravado** até você clicar em Salvar ou Inserir.

## PASB > Vista realista na prancha

1. Ajuste a vista na aba Modelo.
2. Vá para a aba de **layout** (prancha) e clique **PASB > Vista realista na prancha**.
3. Clique dois cantos. É criada uma janela de vista com a mesma vista do modelo, **estilo visual Realista** e **impressão "Como exibido"**: sai realista na impressão e no PDF. Para ajustar o enquadramento, dê duplo clique dentro dela.

Funciona nos layouts do arquivo do modelo. As pranchas geradas pelo Gerenciador de Documentos do Advance Steel são desenhos 2D de linhas; nelas, use **Inserir na prancha...** da janela de render.

> Status: compilado, **ainda não testado dentro do Advance Steel**.
