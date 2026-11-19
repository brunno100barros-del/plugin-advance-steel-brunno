# Especificação inicial de georreferenciamento

## Objetivo

Permitir que o usuário selecione um ponto identificável no modelo Advance Steel e informe as coordenadas reais desse ponto, além da orientação do modelo em relação ao Norte. O IFC exportado deverá carregar a transformação espacial para ser posicionado corretamente em aplicações BIM compatíveis, sem reposicionamento manual.

## Entradas previstas

- Ponto de controle no sistema local do modelo (X, Y, Z).
- Coordenadas projetadas correspondentes (Easting, Northing, Height).
- Datum e sistema de referência: SIRGAS 2000 / UTM.
- Zona UTM e hemisfério.
- Ângulo de orientação em relação ao Norte.
- Unidades de origem e destino.

A interface deverá indicar claramente unidades e convenção angular. Não assumir que um ângulo informado como “rotação em relação ao Norte” tenha a mesma convenção usada internamente por toda biblioteca ou aplicação.

## Transformação

A transformação deverá considerar translação, rotação no plano, elevação e escala/unidades. A convenção matemática será formalizada antes da implementação, incluindo:

- eixo local considerado como X e Y;
- sentido positivo da rotação;
- referência do ângulo (Norte verdadeiro, Norte de quadrícula ou outro);
- ponto local que corresponde às coordenadas reais;
- conversão de milímetros para metros, quando aplicável.

## IFC

Para IFC4, avaliar e implementar a representação de coordenadas projetadas por meio de `IfcProjectedCRS` e `IfcMapConversion`, conforme o esquema e a biblioteca escolhida. Não basta escrever coordenadas UTM em propriedades ou deslocar a geometria e declarar que o arquivo está georreferenciado.

## Zona e validação

A zona UTM não será fixa. A aplicação deverá permitir informar a zona e validar a coerência com as coordenadas; determinação automática poderá ser oferecida, mas deve permitir confirmação ou substituição pelo usuário.

## Testes obrigatórios

- Ponto de controle deve resultar nas coordenadas informadas.
- Ponto de controle e pontos de teste devem manter distâncias e dimensões.
- Rotação de 0°, 90°, 180° e ângulos intermediários.
- Elevação positiva, negativa e zero.
- Conversão de unidades.
- Testes em limites de zona e hemisfério.
- Abrir o IFC em visualizador independente e conferir posicionamento.

## Limitação de escopo

IFC2x3 não possui o mesmo mecanismo nativo de georreferenciamento do IFC4. Qualquer suporte futuro deverá ter estratégia própria documentada, sem alegar equivalência.
