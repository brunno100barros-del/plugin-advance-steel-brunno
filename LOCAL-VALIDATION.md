# Validação local — Etapa 2

Este roteiro deve ser executado no computador que possui Advance Steel 2027 e AdvancedSteelMCP instalado. A revisão remota do repositório não acessa o processo CAD local.

## Preparação

1. Abra o Advance Steel 2027.
2. Abra uma cópia de um desenho de teste. Não use o único arquivo de produção.
3. Digite ASMCP na linha de comando e verifique o estado da ponte/Core.
4. Inicie uma nova sessão do cliente MCP configurado para advance-steel.
5. Não execute ferramentas de criação, edição, exclusão, numeração, comandos arbitrários ou C# nesta primeira verificação.

## Sequência somente leitura

1. advsteel_status — verificar conexão, desenho ativo, versão reportada, unidades e contagem por categoria.
2. advsteel_model_summary — obter contagens, comprimentos, áreas e pesos agregados.
3. advsteel_list_objects com limit=20 e detailed=false — conferir handles e categorias.
4. advsteel_list_objects com limit=20 e detailed=true — conferir propriedades expostas.
5. advsteel_get_objects com 1 a 3 handles retornados — conferir o detalhamento individual.
6. Se existirem no desenho, testar ao menos uma viga e uma chapa; testar parafusos, chumbadores e soldas quando presentes.

## Registrar

- Versão Advance Steel informada por advsteel_status.
- Nome do desenho de teste (pode mascarar dados confidenciais).
- Unidade do desenho e unidades retornadas.
- Contagem por categoria.
- Um exemplo JSON de viga e um de chapa.
- Campos ausentes, nulos ou com erro.
- Exemplos de parafuso, chumbador e solda, se disponíveis.
- Tempo aproximado e mensagens de erro.

Antes de compartilhar resultados, remova caminhos locais, nomes de clientes/projetos e dados confidenciais. Nunca compartilhe o token do arquivo bridge.json nem o conteúdo integral desse arquivo.

## Resultado esperado

A sequência deve demonstrar conexão e leitura repetível. Se uma consulta falhar, registre a mensagem e interrompa; não tente contornar com ferramentas de escrita ou scripts arbitrários.

Uma amostra não prova que todos os objetos e detalhes construtivos podem ser convertidos em IFC. A cobertura será ampliada na Etapa 3.
