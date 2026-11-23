# Importação de IFC (versão 0.8.0)

O botão **PASB > Importar IFC** (comando `PASB_IMPORTAR_IFC`) traz para o desenho a geometria de **qualquer IFC** (Revit, Tekla, ArchiCAD, TQS, Eberick...), separada por **nível**, com uma **cor para cada nível**.

> Status: leitor e janela testados fora do Advance Steel (7 testes de importação com um IFC sintético de 3 níveis, mais o teste da janela). O desenho dentro do Advance Steel (sólidos, malhas, blocos, layers) **ainda não foi testado no programa**.

## Como funciona

```
Janela (níveis, tipos, opções) ──► Pasb.IfcImport.exe (xBIM + OpenCascade, FORA do AutoCAD) ──► malhas trianguladas ──► plugin desenha
```

- A leitura e a triangulação são feitas pela biblioteca **xBIM** num **programa separado**. Se o motor geométrico falhar num IFC problemático, o Advance Steel não cai, e as bibliotecas da xBIM não entram em conflito com as do AutoCAD.
- O plugin só desenha: `SubDMesh` (malha) ou `Solid3d`, que é a malha convertida em sólido.
- A importação inteira é **uma única operação**: **Ctrl+Z** desfaz tudo.
- Cada objeto importado guarda, em XData `PASB_IFC`, o tipo IFC, o GlobalId, o nome e o nível de origem.

## Janela

| Área | Opções |
|---|---|
| **Conteúdo do arquivo** | Resumo (schema, projeto, nº de níveis e elementos) |
| **Níveis** | Lista dos `IfcBuildingStorey` em ordem de cota, com cota (m), nº de elementos e **cor**. Clique na cor para trocar (abre a paleta do AutoCAD). As cores ficam guardadas pelo nome do nível. Elementos fora de pavimentos aparecem em "Sem nível" |
| **Tipos** | Classes IFC com nome em português e quantidade; desmarque o que não quiser |
| **Geometria** | **Solid3d** (plota e cota, mais lento e pesado) ou **Mesh** (muito mais rápido, só referência). "Importar falhas como mesh": o que não virar sólido entra como malha |
| **Blocos** | Peças com a mesma geometria (montantes, esquadrias, parafusos) viram **um bloco** com várias inserções: o DWG fica menor. Para detalhar no Advance Steel, explodir antes (`EXPLODIR`) |
| **Layers** | Por tipo IFC (`IFC_Pilares`...), por layer do IFC (`IFC_S-COLUMN`...; sem layer, usa o tipo), **por nível** (`IFC_Terreo`...; padrão) ou tudo numa layer |
| **Cores** | Originais do IFC (RGB e transparência), **por nível** (padrão) ou cor única (ACI). Com "por nível" + layers por nível, a cor vai na layer e os objetos ficam "ByLayer" |
| **Avançado** | Incluir aberturas (`IfcOpeningElement`) e espaços (`IfcSpace`), desligados por padrão |

As opções ficam guardadas em `%APPDATA%\PASB\import-settings.json`.

## Progresso (0.8.1)

- **Leitura do arquivo** (ao clicar em Abrir...): a janela mostra uma barra com porcentagem, etapa e tempo decorrido. O relógio continua andando mesmo quando a porcentagem para numa etapa demorada, então dá para ver que não travou. Cancelar na janela encerra a leitura.
- **Importação**: uma janela de progresso mostra a etapa (lendo o arquivo, gerando a geometria das peças, peças repetidas, aberturas e recortes, posicionando, desenhando N de M), a porcentagem e o tempo. O botão **Cancelar** encerra o leitor ou descarta a transação, e **nada** é desenhado.
- O IFC é sempre lido **em memória**. O banco temporário em disco que a xBIM usa para arquivos grandes é muito mais lento.
- As etapas internas da xBIM, que reiniciam em 0% a cada fase, são combinadas numa porcentagem que **só avança**; há um teste automático que garante isso.

## Como o nível de cada elemento é encontrado

Para cada elemento, o leitor sobe pela hierarquia do IFC até achar um `IfcBuildingStorey`. Ele segue, nesta ordem: a contenção espacial (`IfcRelContainedInSpatialStructure`, que no IFC4 vale para qualquer produto, inclusive `IfcSpace`), o hospedeiro de uma abertura (`IfcRelVoidsElement`) e a agregação, no caso de peças de um conjunto (`IfcRelAggregates`). Se não encontrar nenhum, o elemento vai para "Sem nível".

## Unidades e posição

- O IFC é convertido para metros pelas unidades declaradas no próprio IFC, e depois para as unidades do desenho (`INSUNITS`).
- A geometria é colocada nas **coordenadas do IFC**, sem deslocamento. Num IFC com coordenadas UTM, os objetos ficam longe da origem do desenho.

## Limitações conhecidas

- **Solid3d** a partir de malha: o AutoCAD só converte malhas fechadas. Peças abertas ou com faces soltas entram como malha (opção ligada por padrão) e são contadas no resumo da linha de comando.
- Os objetos importados são **objetos do AutoCAD**, não peças do Advance Steel: não entram na numeração nem nas listas.
- Arquivos muito grandes (centenas de MB) podem levar minutos no modo Solid3d. Prefira Mesh para referência.
- A xBIM usada (`Xbim.Geometry 6.3.891-netcore`) é uma versão de pré-lançamento.

## Componentes de terceiros

xBIM (CDDL-1.0) e OpenCascade (LGPL-2.1 com exceção OCCT), sem modificação, apenas no `Contents\IfcImport`. Ver [THIRD-PARTY-NOTICES.md](../THIRD-PARTY-NOTICES.md). O plugin montado passou de ~0,5 MB para ~60 MB.

## Testes

`tests\Pasb.IfcImport.Tests` roda o `Pasb.IfcImport.exe` compilado no IFC sintético `fixtures\niveis.ifc`, gerado por `fixtures\gera_ifc_niveis.py`: 3 níveis, pilares, vigas, lajes, montantes repetidos (`IfcMappedItem`), aberturas e espaços. O teste confere:
- os níveis e as cotas;
- os tipos;
- as layers do IFC;
- as posições em metros;
- as cores;
- o compartilhamento de geometria;
- os filtros por nível e por tipo;
- o formato binário.
