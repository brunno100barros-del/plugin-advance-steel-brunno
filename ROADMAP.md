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

- [x] Identificar comunicação MCP, TCP local e despacho para thread principal.
- [x] Identificar projetos Loader/Core e referências Autodesk declaradas.
- [x] Inventariar operações de leitura e categorias de objetos.
- [x] Registrar unidades, identificadores e limitações observáveis.
- [ ] Confirmar versão/runtime efetivos da instalação Advance Steel 2027 do usuário.
- [ ] Executar advsteel_status com Advance Steel aberto.
- [ ] Extrair amostra somente de leitura com advsteel_list_objects e advsteel_get_objects.
- [ ] Validar amostras reais de vigas, chapas, parafusos, chumbadores e soldas, conforme existirem.
- [ ] Comparar contagens e propriedades com o modelo.
- [ ] Verificar desenho vazio, modelo real, seleção vazia, timeout e erro de conexão.

**Resultado parcial:** revisão estática concluída. A integração em execução depende de teste no computador com Advance Steel.

**Critério de saída:** extração reproduzível de elementos de teste, com evidência dos dados retornados e unidades.

## Etapa 3 — Modelo neutro de dados

- [ ] Definir entidades, propriedades, unidades e identificadores persistentes.
- [ ] Mapear perfis, chapas, parafusos, soldas, materiais e demais objetos.
- [ ] Registrar mapeamento semântico IFC e limitações.
- [ ] Criar testes de integridade e rastreabilidade.

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
- [x] Preparar instalação e documentação: instalador 0.13.1 e guia INSTALACAO.md.
- [ ] Logs e robustez da instalação.
- [ ] Validar instalador e plugin em ambiente limpo com Advance Steel 2027.

## Fora do escopo

- Não alterar o repositório original AdvancedSteelMCP.
- Não substituir o exportador IFC nativo.
- Não declarar suporte a objetos/propriedades sem testes.
- Não prometer IFC2x3 equivalente ao IFC4 sem estratégia própria.
