# Roadmap do projeto

Este roadmap separa preparação, validação técnica e implementação. Uma etapa só será considerada concluída quando seus critérios puderem ser verificados.

## Etapa 1 — Fundação do projeto
- [x] Criar estrutura modular.
- [x] Registrar premissas e decisões pendentes.
- [x] Separar MCP, modelo neutro, georreferenciamento e exportação IFC.
- [x] Definir critérios iniciais.
- [x] Confirmar direção de plataforma: .NET 10 / Windows x64, sujeita à validação no host.
- [x] Inspecionar código e protocolo do AdvancedSteelMCP.

## Etapa 2 — Prova de integração com Advance Steel e MCP
- [x] Revisar estaticamente comunicação MCP, TCP local e despacho para thread principal.
- [x] Identificar projetos Loader/Core e referências Autodesk declaradas.
- [x] Inventariar operações de leitura e categorias de objetos.
- [x] Usuário confirmou que o MCP original está funcionando em seu ambiente.
- [ ] Registrar respostas JSON reais de leitura e amostras por categoria.
- [ ] Confirmar versão/runtime efetivos e unidades do desenho de teste.
- [ ] Comparar contagens e propriedades extraídas com o modelo.
- [ ] Verificar casos de desenho vazio, seleção vazia, timeout e erro de conexão.

**Resultado:** funcionamento geral do MCP confirmado pelo usuário. A cobertura e a qualidade dos dados necessários ao IFC ainda não foram comprovadas por amostras documentadas.

## Etapa 3 — Contrato do modelo neutro
- [x] Definir princípios de independência do MCP e da API Autodesk.
- [x] Definir identidade de origem, identidade interna e estratégia provisória de GlobalId IFC.
- [x] Definir categorias iniciais e estados de validação.
- [x] Definir requisitos de geometria, unidades, proveniência e rastreabilidade.
- [x] Elaborar matriz inicial de mapeamento IFC4.
- [ ] Conferir contrato com JSONs reais de viga, chapa e demais categorias.
- [ ] Implementar classes C# e conversores após validar as amostras.
- [ ] Criar testes unitários do contrato e das conversões.

**Critério de saída:** o contrato é confirmado contra dados reais, implementado e testado sem dependência direta do MCP ou da API Autodesk.

## Etapa 4 — Georreferenciamento
- [ ] Definir ponto de controle, coordenadas E/N/Z e unidade.
- [ ] Definir convenção angular em relação ao Norte.
- [ ] Definir zona UTM e validação.
- [ ] Implementar transformação local-projetada e inversa.
- [ ] Testar rotação, elevação, escala e tolerâncias.

## Etapa 5 — Exportador IFC4
- [ ] Selecionar biblioteca IFC após prova de compatibilidade.
- [ ] Construir estrutura espacial e unidades.
- [ ] Converter geometria e propriedades.
- [ ] Incorporar georreferenciamento IFC4.
- [ ] Validar em visualizador independente.
- [ ] Comparar coordenadas, dimensões, quantidades e propriedades.

## Etapa 6 — Interface, robustez e distribuição
- [ ] Criar interface de ponto de controle e parâmetros.
- [ ] Exibir validações e relatório de exportação.
- [ ] Tratar erros sem bloquear ou corromper o modelo.
- [ ] Preparar instalação, logs e documentação.
- [ ] Validar em ambiente limpo com Advance Steel 2027.

## Fora do escopo
- Não alterar o repositório original AdvancedSteelMCP.
- Não substituir o exportador IFC nativo.
- Não declarar suporte a objetos/propriedades sem testes.
- Não prometer IFC2x3 equivalente ao IFC4 sem estratégia própria.
