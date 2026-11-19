# Especificação inicial do exportador IFC

## Requisito principal

Gerar um arquivo IFC próprio a partir do modelo neutro alimentado pela integração com Advance Steel. Não utilizar o exportador IFC nativo do Advance Steel como mecanismo de geração.

## Estratégia

1. Receber elementos e propriedades do modelo neutro.
2. Validar unidades, identificadores e dados mínimos.
3. Criar projeto, unidades, contexto geométrico e estrutura espacial IFC.
4. Converter geometria e semântica dos elementos suportados.
5. Aplicar georreferenciamento.
6. Gravar arquivo e relatório de validação.

## Preservação de dados

A conversão deverá buscar preservar, quando disponíveis na origem:

- identificador de origem e GlobalId IFC;
- classificação/tipo de elemento;
- geometria e posicionamento;
- perfil e dimensões;
- material;
- propriedades e quantidades;
- ligações e componentes, incluindo chapas, parafusos e soldas, quando a extração fornecer dados suficientes.

Não declarar preservação completa até testar cada categoria. Objetos sem mapeamento semântico confiável deverão ser identificados como não suportados ou exportados por uma representação geométrica explicitamente documentada.

## Seleção da biblioteca

A biblioteca será escolhida após verificar compatibilidade com o runtime exigido pelo Advance Steel 2027, suporte ao IFC4 e capacidade de gravar as entidades de georreferenciamento necessárias.

## Validação de saída

Cada exportação deverá gerar um relatório com contagem por categoria, elementos ignorados, propriedades não mapeadas, avisos, unidades e parâmetros de georreferenciamento.

## IFC2x3

Não faz parte do primeiro alvo. A necessidade e a estratégia de compatibilidade serão avaliadas após validar o fluxo IFC4.
