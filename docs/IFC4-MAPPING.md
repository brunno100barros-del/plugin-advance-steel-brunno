# Matriz inicial de mapeamento IFC4

Status: proposta de mapeamento. Não significa que a geometria ou semântica de todas as categorias já foi validada.

## 1. Regras gerais
- Alvo inicial: IFC4; exportador próprio, sem usar o exportador nativo Advance Steel.
- Receber somente o modelo neutro.
- Preservar todas as propriedades acessíveis na origem; não limitar a exportação a uma whitelist.
- Mapear propriedades para semântica IFC padrão quando houver equivalência correta.
- Exportar todas as propriedades restantes em Property Sets customizados.
- Preservar nomes técnicos, valores, tipos, unidades e proveniência.
- Relatar propriedades não acessíveis, com erro ou não aplicáveis; não alegar completude sem evidência.

## 2. Estrutura espacial
- IfcProject, IfcSite, IfcBuilding e IfcBuildingStorey quando aplicável.
- IfcRelAggregates, IfcGeometricRepresentationContext e IfcUnitAssignment.
- IfcProjectedCRS e IfcMapConversion para georreferenciamento IFC4, após validação.

## 3. Mapeamento por categoria

| Categoria | Entidade IFC candidata | Ressalvas |
|---|---|---|
| beam | IfcBeam | Geometria e cortes precisam de dados suficientes |
| plate | IfcPlate | Contorno, furos, espessura e posição |
| bolts | IfcMechanicalFastener | Individualização e geometria conforme dados |
| anchors | IfcMechanicalFastener | Distinguir de parafusos por atributos |
| weld | IfcWeld quando aplicável | Relações e atributos devem ser comprovados |
| special_part | Tipo específico ou IfcBuildingElementProxy como fallback | Não inferir semântica |
| connector | Depende da natureza | Inspecionar antes de mapear |

## 4. Estratégia de propriedades: preservação integral

### 4.1 Mapeamento IFC padrão
Quando o significado e o tipo coincidirem com o schema, usar atributos IFC, materiais, classificações, relações, quantidades e Psets padronizados.

### 4.2 Propriedades específicas do Advance Steel
Criar Psets customizados por categoria/classe e grupo de origem, preservando:
- nome técnico original;
- rótulo amigável, quando disponível;
- valor;
- tipo de dado;
- unidade;
- origem/método de leitura.

User Attributes e atributos customizados também devem ser exportados. Propriedades desconhecidas não podem ser descartadas só por não haver mapeamento padrão.

### 4.3 Tipagem e valores especiais
Distinguir texto, inteiro, real, booleano, enumeração, lista, referência e demais tipos que a API disponibilizar. Distinguir zero, falso, string vazia, nulo, não aplicável, indisponível e erro de leitura. Valores complexos devem ser serializados de modo reversível ou registrados como estrutura complementar, sem conversão silenciosa para texto simplificado.

## 5. Geometria
Prioridade: BRep/sólido válido; geometria paramétrica reconstruída com parâmetros suficientes; tesselação validada. Representações simplificadas devem ser identificadas. Propriedades não substituem geometria.

## 6. Relacionamentos
Criar relações entre peças, conjuntos, parafusos, chumbadores e soldas apenas com vínculo verificável. Proximidade não prova conexão.

## 7. Relatório de exportação
Apresentar:
- totais lidos/exportados por categoria;
- inventário de propriedades descobertas e lidas;
- propriedades exportadas em IFC padrão e em Psets customizados;
- propriedades indisponíveis, com erro ou não aplicáveis;
- elementos parciais, ignorados e não suportados;
- representação geométrica usada;
- unidades e parâmetros de georreferenciamento;
- correspondência entre handles e GlobalIds.

## 8. Validação
- IFC4 abre em visualizador independente.
- Todas as entidades têm GlobalId válido.
- Comparar propriedades lidas no IFC com valores de origem por amostragem e por testes automatizados.
- Verificar contagens, dimensões, unidades e coordenadas.
- Relatar toda perda conhecida.
