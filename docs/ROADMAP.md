# Roadmap do projeto

Este roadmap separa preparação, validação técnica e implementação. Uma etapa só será considerada concluída quando seus critérios puderem ser verificados.

## Etapa 1 — Fundação
- [x] Criar estrutura modular e registrar premissas.
- [x] Separar MCP, modelo neutro, georreferenciamento e exportação IFC.
- [x] Inspecionar código e protocolo do AdvancedSteelMCP.

## Etapa 2 — Integração MCP
- [x] Revisão estática do MCP e confirmação do usuário de que o servidor original funciona.
- [ ] Registrar respostas JSON reais e amostras por categoria.
- [ ] Comparar dados extraídos com propriedades exibidas no Advance Steel.
- [ ] Identificar propriedades não expostas pelo MCP, mas acessíveis pela API oficial.

## Etapa 3 — Modelo neutro e preservação integral
- [x] Definir modelo neutro independente do MCP/API.
- [x] Definir identidade, proveniência, unidades e estados de validação.
- [x] Aprovar requisito de preservação de todas as propriedades acessíveis.
- [x] Definir estratégia IFC padrão + Psets customizados.
- [ ] Inventariar propriedades gerais, herdadas, específicas e User Attributes por classe.
- [ ] Validar inventário com JSONs reais e Advance Steel 2027.
- [ ] Implementar captura tipada e armazenamento sem whitelist fechada.
- [ ] Criar testes de cobertura e comparação de propriedades.

**Critério de saída:** inventário verificável por categoria, com valores/tipos/unidades preservados e relatório explícito de limitações.

## Etapa 4 — Georreferenciamento
- [ ] Definir ponto de controle, coordenadas E/N/Z, zona e hemisfério.
- [ ] Definir convenção angular em relação ao Norte.
- [ ] Implementar transformação direta/inversa e testes.

## Etapa 5 — Exportador IFC4
- [ ] Selecionar biblioteca compatível.
- [ ] Criar estrutura espacial e unidades.
- [ ] Converter geometria e semântica.
- [ ] Exportar todas as propriedades acessíveis em atributos/Psets padrão ou customizados.
- [ ] Incorporar georreferenciamento IFC4.
- [x] Leitura otimizada para exportação (perfil de trabalho, 0.13.1; docs/EXPORT-PERFORMANCE.md).
- [x] Corpo sólido real das peças (cortes, furos, perfis do usuário, parafusos com cabeça, arruelas e porca) e exportação resumida (0.14.0; docs/IFC-BODY.md).
- [ ] Medir o tempo de exportação antes/depois num modelo real.
- [ ] Validar em visualizador independente e comparar propriedades.

## Ideias em estudo
- [ ] IFC vinculado: atualizar o modelo 3D da arquitetura no desenho quando o IFC mudar, mantendo o ponto em comum (docs/IFC-LINK.md; só viabilidade).

## Etapa 6 — Interface e distribuição
- [ ] Interface de ponto de controle e exportação.
- [ ] Relatório de cobertura, perdas e avisos.
- [x] Instalador 0.14.2 e guia de instalação.
- [ ] Logs e robustez da instalação.
- [ ] Validação do instalador e do plugin em ambiente limpo com Advance Steel 2027.

## Fora do escopo
- Não alterar preventivamente o repositório original AdvancedSteelMCP.
- Não usar o exportador IFC nativo do Advance Steel.
- Não declarar 100% de cobertura sem inventário comparativo.
- Não descartar propriedades desconhecidas.
