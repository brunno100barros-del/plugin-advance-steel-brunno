# Revisão técnica do AdvancedSteelMCP

- Repositório: https://github.com/Shame625/AdvancedSteelMCP
- Branch: main
- Data: 2026-11-19
- Escopo: revisão estática do código público; nenhum arquivo do projeto original foi alterado e nenhuma conexão CAD local foi executada nesta revisão.

## Arquitetura observada

Cliente MCP --stdio--> Python/FastMCP --TCP local/JSON por linha/token--> Loader C# --> despacho para thread principal AutoCAD/Advance Steel --> Core C# --> APIs Autodesk.

O Python lê a descoberta em %LOCALAPPDATA%\AdvanceSteelMCP\bridge.json a cada chamada. O Loader escuta em 127.0.0.1, gera token aleatório por sessão e valida o token. Cada conexão transporta uma requisição JSON terminada em nova linha e recebe resposta JSON.

O Loader despacha chamadas para a thread principal. O Core classifica métodos em contextos command, application ou none. Operações de escrita usam o mecanismo transacional do projeto quando aplicável.

## Projetos e plataforma declarada

- plugin/Loader: add-in fino, ponte TCP e despacho; referencia assemblies gerenciadas do AutoCAD.
- plugin/Core: consultas, modelagem, modificação, visualização, bancos de catálogo e execução C#; referencia assemblies do AutoCAD e Advance Steel.
- server: servidor Python/FastMCP e cliente TCP.
- plugin/bundle: manifesto do ApplicationPlugins.
- install.ps1: build, instalação do bundle e preparação do ambiente Python.

Directory.Build.props declara net10.0-windows, x64, caminho padrão do AutoCAD 2027 e subpasta ADVS. O Core referencia assemblies como ASCADLinkMgd, ASGeometryMgd, ASObjectsMgd, ASMgd, ASProfilesMgd e ASModelerMgd.

A documentação Autodesk consultada confirma .NET 10 para plugins gerenciados AutoCAD 2027 e disponibiliza SDK específico do Advance Steel 2027. A compatibilidade final deve ser validada na instalação local e no SDK correspondente.

## Ferramentas MCP de leitura

O código Python registra 27 ferramentas. Para o primeiro teste, as consultas relevantes são:
- advsteel_status
- advsteel_list_objects
- advsteel_get_objects
- advsteel_get_selection
- advsteel_model_summary
- advsteel_bill_of_materials
- advsteel_search_profiles
- advsteel_list_databases
- advsteel_db_query
- advsteel_api_help

Há também ferramentas de visualização e várias de criação, edição, exclusão, numeração e execução arbitrária de C#. Essas últimas não devem ser usadas nos testes iniciais.

## Dados que o código declara expor

Categorias físicas incluem beam, plate, bolts, anchors, weld, special_part e connector.

Campos inspecionados:
- Identificação: handle, type e category.
- Propriedades: material, coating, assembly, denotation/nome, layer, role e números de peça/assembly, quando disponíveis.
- Vigas: pontos inicial/final, comprimento, perfil, eixo de referência, peso e área de pintura.
- Chapas: espessura, dimensões físicas, área, peso e dados do sistema de coordenadas físico.
- Parafusos/chumbadores: padrão, classe, diâmetro, comprimento, conjunto e quantidade, conforme o tipo.
- Soldas: tipo, espessura e comprimento, quando a API retorna esses valores.
- Catálogos: perfis, materiais e outros dados disponíveis na instalação.

list_objects pagina resultados por limit e offset; o limite máximo declarado é 5000. detailed solicita mais propriedades. get_objects recebe handles para detalhamento individual.

## Unidades e coordenadas

O README do MCP declara dimensões em milímetros, pesos em quilogramas, ângulos em graus, coordenadas WCS e Z positivo para cima. Os identificadores são handles do desenho, por exemplo 2A7F.

Essas convenções devem ser confrontadas com amostras reais, unidades do desenho e valores retornados pela API.

## Limitações para o exportador IFC

1. Handle não é GlobalId IFC. O caminho de consulta inspecionado expõe handles CAD/Advance Steel e conversões entre IDs do AutoCAD e IDs internos; não foi identificado GlobalId IFC persistente.
2. A cobertura geométrica não está comprovada. Categorias e propriedades não provam fidelidade de cortes, furos, juntas, chumbadores, soldas e peças especiais.
3. Campos podem falhar individualmente ou não existir para determinados tipos.
4. BOM não equivale a modelo IFC: quantidades não substituem geometria e semântica.
5. Scripts C# têm alto privilégio dentro do processo Advance Steel e não são sandbox.
6. Timeout não garante cancelamento de operação já iniciada; não repetir escritas sem inspecionar o desenho.

## Conclusão

É razoável usar o MCP existente como camada inicial de acesso, sem duplicar seu servidor. A integração deve começar com consultas de leitura e converter respostas para um modelo neutro próprio. O exportador não deve depender diretamente do JSON bruto do MCP.

Status: arquitetura, protocolo e superfície de dados revisados estaticamente. Nenhuma conexão ao Advance Steel do usuário foi realizada nesta sessão; a extração real ainda precisa ser testada localmente.
