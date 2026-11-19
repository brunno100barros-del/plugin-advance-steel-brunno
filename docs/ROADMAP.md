# Roadmap do projeto

Este roadmap separa preparação, validação técnica e implementação. Uma etapa só será considerada concluída quando seus critérios puderem ser verificados.

## Etapa 1 — Fundação do projeto

- [x] Criar estrutura modular do repositório.
- [x] Registrar premissas e decisões técnicas ainda pendentes.
- [x] Separar integração MCP, modelo neutro, georreferenciamento e exportação IFC.
- [x] Definir critérios iniciais de validação.
- [ ] Confirmar requisitos oficiais de runtime/API do Advance Steel 2027.
- [ ] Inspecionar o código e o protocolo efetivamente usados pelo AdvancedSteelMCP.

**Critério de saída:** arquitetura e dependências compatíveis documentadas; fluxo de comunicação MCP reproduzível; nenhuma tecnologia crítica escolhida por suposição.

## Etapa 2 — Prova de integração com Advance Steel e MCP

- [ ] Identificar mecanismo de carregamento e referências de API suportadas pelo Advance Steel 2027.
- [ ] Conectar ao servidor MCP existente sem modificá-lo.
- [ ] Executar operações de leitura em um modelo de teste.
- [ ] Registrar campos disponíveis, limitações, unidades e identificadores.
- [ ] Verificar estabilidade, erros e comportamento com modelo vazio e modelo real.

**Critério de saída:** extração reproduzível de um conjunto de elementos de teste, com evidência dos dados realmente retornados.

## Etapa 3 — Modelo neutro de dados

- [ ] Definir entidades, propriedades, unidades e identificadores persistentes.
- [ ] Mapear perfis, chapas, parafusos, soldas, materiais e demais objetos encontrados.
- [ ] Registrar o que pode e o que não pode ser convertido em semântica IFC.
- [ ] Criar testes de integridade e rastreabilidade.

**Critério de saída:** modelo neutro documentado e validado contra amostras reais do Advance Steel.

## Etapa 4 — Georreferenciamento

- [ ] Definir ponto de controle selecionado no modelo.
- [ ] Definir entrada de coordenadas reais E/N/Z e unidade.
- [ ] Definir convenção do ângulo em relação ao Norte (sentido, origem e referencial).
- [ ] Definir zona UTM e datum/projeção, com validação e possibilidade de correção manual.
- [ ] Implementar transformação local-projetada e transformação inversa.
- [ ] Testar pontos de controle, rotação, elevação, escala e tolerâncias.

**Critério de saída:** transformação numérica validada em casos de teste independentes, sem depender da visualização do modelo.

## Etapa 5 — Exportador IFC4

- [ ] Selecionar biblioteca IFC após prova de compatibilidade.
- [ ] Construir estrutura espacial e unidades IFC.
- [ ] Converter geometria e propriedades do modelo neutro.
- [ ] Incorporar georreferenciamento IFC4.
- [ ] Exportar e validar o arquivo em visualizador independente.
- [ ] Comparar coordenadas, dimensões, quantidades e propriedades com o modelo de origem.

**Critério de saída:** IFC4 aberto em pelo menos um visualizador independente, com posição, orientação, unidades e geometria verificadas.

## Etapa 6 — Interface, robustez e distribuição

- [ ] Criar interface de seleção do ponto e parâmetros de georreferenciamento.
- [ ] Exibir validações e relatório de exportação.
- [ ] Tratar erros sem bloquear ou corromper o modelo de origem.
- [ ] Preparar instalação, logs e documentação de uso.
- [ ] Validar em ambiente limpo com Advance Steel 2027.

## Fora do escopo desta etapa

- Não alterar o repositório original AdvancedSteelMCP.
- Não substituir o exportador IFC nativo dentro do Advance Steel.
- Não declarar suporte a objetos ou propriedades antes de testá-los.
- Não prometer compatibilidade IFC2x3 equivalente ao IFC4 sem uma estratégia específica.
