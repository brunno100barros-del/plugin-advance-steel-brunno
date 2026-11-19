# ADR-001 — Plataforma e dependências

- **Status:** proposta em aberto
- **Data:** 2026-11-19

## Contexto

O projeto precisa executar como plugin do Autodesk Advance Steel 2027 e interoperar com um servidor MCP existente. O fato de o ambiente do servidor MCP utilizar determinada versão do .NET ou Python não determina o runtime aceito pelo processo do Advance Steel.

## Decisão

Adiar a escolha do target framework, runtime, biblioteca IFC e mecanismo de UI até verificar:

1. Requisitos oficiais de desenvolvimento e carregamento de add-ins do Advance Steel 2027.
2. Versões das assemblies Autodesk disponíveis e seus requisitos de runtime.
3. Compatibilidade das bibliotecas candidatas com esse runtime.
4. Protocolo e transporte usados pelo AdvancedSteelMCP instalado.

## Consequências

- Evita iniciar uma solução que compila isoladamente, mas não carrega no Advance Steel.
- A primeira prova técnica deverá ser um add-in mínimo que carregue e execute um comando simples.
- A biblioteca IFC será selecionada por prova de compatibilidade e validação de arquivos, não apenas por popularidade.

## Critério para fechar esta ADR

Registrar versões, referências, documentação consultada, ambiente de compilação e resultado de um teste mínimo dentro do Advance Steel 2027.
