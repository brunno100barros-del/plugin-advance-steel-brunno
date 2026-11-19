# Matriz inicial de mapeamento IFC4

Status: proposta de mapeamento. Não significa que a geometria ou semântica de todas as categorias já foi validada.

## 1. Regras gerais
- Alvo inicial: IFC4.
- O exportador é próprio e não chama o exportador IFC nativo do Advance Steel.
- O exportador recebe o modelo neutro, nunca respostas MCP diretamente.
- Cada entidade IFC recebe GlobalId válido gerado ou preservado somente quando houver evidência de identidade persistente.
- Geometria simplificada deve ser identificada no relatório; não declarar equivalência com o sólido de fabricação.
- Propriedades sem mapeamento padrão podem ser preservadas em conjuntos de propriedades customizados, com nome, tipo, unidade e proveniência.

## 2. Estrutura espacial mínima proposta
- IfcProject
- IfcSite
- IfcBuilding
- IfcBuildingStorey quando fizer sentido para o modelo
- IfcRelAggregates para a hierarquia espacial
- IfcGeometricRepresentationContext e subcontextos necessários
- IfcUnitAssignment com unidades explícitas
- IfcProjectedCRS e IfcMapConversion para georreferenciamento IFC4, após validação da convenção e da biblioteca

A hierarquia espacial não deve inventar pavimentos. Se o modelo não tiver informação de pavimento, usar uma estrutura mínima documentada.

## 3. Mapeamento por categoria

| Categoria de origem | Entidade IFC candidata | Requisitos e ressalvas |
|---|---|---|
| beam | IfcBeam | Eixo, perfil/seção, material e orientação; cortes e furos exigem geometria adicional |
| plate | IfcPlate | Espessura, contorno, posição e furos; dimensões nominais não bastam para contorno real |
| bolts | IfcMechanicalFastener | Diâmetro, comprimento, classe, quantidade e geometria/posição; conjuntos podem precisar ser individualizados |
| anchors | IfcMechanicalFastener | Distinguir chumbador de parafuso por tipo/atributos; não inferir geometria ausente |
| weld | IfcWeld quando aplicável ao esquema e à semântica disponível | Tipo, tamanho, comprimento, posição e relação entre peças precisam estar disponíveis |
| special_part | Depende da classificação; IfcBuildingElementProxy apenas como fallback explícito | Não classificar semanticamente sem evidência |
| connector | Depende da natureza; possível IfcElementAssembly, IfcMechanicalFastener ou proxy | Inspecionar o significado real da categoria antes de escolher |

O tipo IFC final deverá ser conferido contra o schema IFC4 efetivamente utilizado pela biblioteca selecionada.

## 4. Geometria e representações
A implementação deverá priorizar a representação mais fiel disponível:
1. Sólido/BRep válido, se obtido da origem.
2. Geometria paramétrica reconstruída a partir de parâmetros suficientes e verificados.
3. Tesselação validada, com tolerância conhecida.
4. Sem geometria, apenas se o caso de uso permitir e o relatório indicar claramente a limitação.

Não reconstruir uma viga apenas por comprimento e perfil quando houver cortes, furos, recortes ou orientação não representados pelos dados disponíveis.

## 5. Propriedades
Preservar, quando disponíveis:
- identificação de origem e handle;
- nome/denominação, descrição, layer e role;
- material e revestimento;
- perfil, dimensões e espessura;
- peso, área de pintura e quantidades;
- números de peça e conjunto;
- propriedades específicas de parafusos, chumbadores e soldas.

Usar propriedades IFC padrão quando o significado e o tipo forem compatíveis. Caso contrário, criar um Pset próprio com prefixo do produto, por exemplo Pset_BrunnoAdvanceSteel_*, evitando sobrescrever conjuntos padronizados.

## 6. Relacionamentos
Relações entre peças, conjuntos, parafusos, chumbadores e soldas só serão criadas quando a origem fornecer vínculo verificável. Proximidade geométrica não é prova suficiente de conexão.

## 7. Relatório de exportação
O relatório deverá apresentar:
- total lido por categoria;
- total exportado por entidade IFC;
- elementos parciais, ignorados e não suportados;
- propriedades preservadas e não mapeadas;
- tipo de representação geométrica usada;
- unidades;
- sistema de referência, zona, hemisfério e parâmetros de transformação;
- GlobalIds gerados e correspondência com os handles de origem.

## 8. Critérios de validação
- Arquivo abre em pelo menos um visualizador IFC independente.
- Schema declarado e efetivo é IFC4.
- Todas as entidades têm GlobalId válido.
- Contagens por categoria batem com o relatório.
- Unidades e dimensões são conferidas contra o modelo de origem.
- Pontos de controle e pontos de verificação conferem após georreferenciamento.
- Elementos simplificados/incompletos são identificáveis no relatório.
