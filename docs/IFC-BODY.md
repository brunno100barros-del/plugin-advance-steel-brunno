# Corpo real e exportação resumida no IFC (versão 0.14.0)

## Problema (0.13.1)

Comparando com o IFC do steelXbim no modelo do SESI (`ESAP-SAP-EMT-PE`), o IFC do plugin:

| Problema | Causa |
|---|---|
| Perfis do usuário (`Usr PERFIL U ENRIJECIDO`, `Usr PERFIL U SIMPLES`...) e tipos sem mapeamento (ex.: `W`) saíam sem corpo | O corpo era **reconstruído** a partir das dimensões do perfil, e só U e I tinham mapeamento |
| Parafusos sem cabeça, arruelas e porca | O parafuso era um cilindro (Ø × comprimento) |
| Cortes, recortes e furos dos perfis não apareciam | Só os cortes de serra das mesas eram aplicados, por planos |
| IFC de 400 MB, 3,57 milhões de propriedades | Todas as propriedades de todos os objetos iam para Psets |

## Solução

### 1. Corpo sólido real do Advance Steel

O plugin lê, de cada peça, o **mesmo sólido que o Advance Steel desenha**: `AtomicElement.GetModeler(kDetailed)` e `ModelerBody.getBrepInfo` (vértices e faces com furos). Só leitura, dentro da mesma transação com escrita descartada e conferência de `DBMOD`.

- Entram cortes, recortes, furos, perfis do usuário, perfis compostos e parafusos com cabeça, arruelas e porca.
- No IFC, cada corpo vira um `IfcPolygonalFaceSet` (IFC4): vértices numa `IfcCartesianPointList3D` e faces com furos em `IfcIndexedPolygonalFaceWithVoids`. Coordenadas em metros com 5 casas (0,01 mm).
- **Parafusos e chumbadores:** o corpo do conjunto é separado por parafuso (cada face vai para o eixo de parafuso mais próximo). Parafusos iguais compartilham uma `IfcRepresentationMap`, repetida por `IfcMappedItem`, como faz o steelXbim. Se a separação não fechar, o conjunto sai com o corpo inteiro.
- **Vigas compostas (ex.: 2U):** as componentes existem no modelo como vigas próprias (conferido: volume da composta = soma das duas componentes). A composta não recebe corpo, para não duplicar.
- Sem corpo (leitura falhou), o elemento usa a geometria paramétrica de reserva da 0.13.

Conferência no modelo do SESI, com o Advance Steel aberto: 8.314 peças com corpo lido em cerca de 10 s, sem falhas. Nos 450 conjuntos de parafusos e chumbadores conferidos, a separação por parafuso fechou em todos.

### 2. Exportação resumida (padrão)

Na janela **Exportar IFC**, em **PROPRIEDADES**:

- **Resumidas (rápido)**, o padrão: lê só as propriedades da lista `SummaryFields` e grava um Pset `PASB_Resumo` com nomes em português e unidades SI:

| Objeto | Propriedades |
|---|---|
| Vigas e pilares | Marca da peça, Marca do conjunto, Tipo de aço, Perfil, Tipo de perfil, Comprimento (m), Peso por metro (kg/m), Peso total (kg), Área de pintura (m²) |
| Chapas | Marcas, Tipo de aço, Espessura, Comprimento, Largura (m), Peso total (kg), Área de pintura (m²) |
| Parafusos | Norma, Classe, Conjunto, Diâmetro, Comprimento (m), Quantidade, Peso total (kg) |
| Chumbadores | Norma, Classe, Tipo, Diâmetro, Comprimento (m), Quantidade, Peso total (kg) |
| Soldas | Tipo, Espessura, Comprimento (m) |
| Peças especiais | Marcas, Tipo de aço, Nome, Peso e Área de pintura (valores do Advance Steel; unidade não comprovada) |

  Todos têm também o `Handle`. Uma propriedade só entra quando o valor existe e a unidade é comprovada.

- **Completas:** todas as propriedades, em Psets `AS_<Classe>`, como antes.

Na resumida, não são chamados `getAttributeStatus`, User Attributes nem os demais getters (`ReadProfile.ExportSummary`). A geometria e os grupos de ligação continuam com o que precisam.

## Medição

Com os dados da exportação de 23/11/2026 do modelo do SESI (35.191 objetos), reprocessados fora do Advance Steel sem os corpos:

| | 0.13.1 (completa) | 0.14.0 resumida |
|---|---|---|
| Propriedades | 3.574.755 | 135.041 |
| Tamanho do IFC | 400 MB | 24 MB (sem os corpos) |

**Pendente:** medir no Advance Steel o tempo total e o tamanho final do IFC com os corpos reais. A leitura dos 8.314 corpos levou cerca de 10 s no modelo aberto.

## Correção na 0.14.1: chapas "transparentes"

No visualizador, as chapas furadas apareciam vazadas: só as bordas ficavam visíveis. No IFC exportado pela 0.14.0 do SESI, 8.979 faces tinham contornos internos. Havia dois problemas:

- Muitos visualizadores não preenchem `IfcIndexedPolygonalFaceWithVoids`. Em 1.365 furos de chapa, o contorno interno vinha no mesmo sentido do externo.
- Os "contornos internos" do `getBrepInfo` nem sempre são furos. Em 713 faces eram **regiões separadas do mesmo plano**, como 5 cabeças de parafuso numa face só, 4 delas como "furos" de mesma área. O visualizador descartava essas regiões.

Agora, para cada face:
1. cada contorno é classificado por inclusão: é furo do menor contorno que o contém, ou então é uma região própria;
2. cada região com furos é **triangulada** (recorte de orelhas, como o earcut) e gravada só com faces simples (`IfcIndexedPolygonalFace`), no sentido do contorno externo;
3. se o volume com sinal do sólido der negativo (faces viradas para dentro, visto em 5 vigas W200), todos os contornos são invertidos.

Conferência nas 8.979 faces do IFC do SESI: todas trianguladas, sem falha. A área triangulada bate com "área externa − furos" com diferença máxima de 0,03%, que vem do arredondamento das coordenadas em 0,01 mm.

## Limitações

- Soldas continuam sem corpo (não são `AtomicElement`).
- O `IfcPolygonalFaceSet` é do IFC4 (Addendum 2). Visualizadores muito antigos, só IFC2x3, não leem.
- Faces com furos viram triângulos: o arquivo fica um pouco maior, e as arestas dos triângulos aparecem em vistas de arame.
- A separação dos parafusos supõe que `BoltPattern.Normal` é o eixo dos parafusos (conferido nos 450 conjuntos). Quando não fecha, o conjunto sai inteiro, sem repetição.
