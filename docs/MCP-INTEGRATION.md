# Integração com AdvancedSteelMCP

## Diretriz

Utilizar o repositório existente [AdvancedSteelMCP](https://github.com/Shame625/AdvancedSteelMCP) como infraestrutura externa. Este projeto não deve modificar, copiar ou assumir manutenção do repositório original.

## O que precisa ser confirmado

Antes de implementar o cliente, inspecionar o código instalado e registrar:

- Como o servidor Python é iniciado.
- Como o Loader/add-in C# e a ponte TCP se comunicam.
- Endereço, porta, framing e formato das mensagens, se aplicável.
- Ciclo de conexão, desconexão, timeout e tratamento de erros.
- Lista de ferramentas MCP e respectivos schemas de entrada/saída.
- Quais operações retornam geometria, propriedades, identificadores e unidades.
- Quais operações foram efetivamente testadas em Advance Steel 2027.

## Contrato interno desejado

O módulo MCP deverá expor operações internas tipadas e independentes da interface. Respostas brutas do servidor devem ser convertidas para objetos do modelo neutro e manter referência ao identificador de origem.

## Limites

A existência de uma ferramenta MCP não prova, por si só, que ela retorna todos os dados necessários ao IFC. A cobertura será documentada ferramenta por ferramenta e propriedade por propriedade.

## Segurança e estabilidade

- Não armazenar credenciais ou configurações locais no repositório.
- Não bloquear a interface do Advance Steel durante chamadas demoradas.
- Definir timeout e cancelamento.
- Registrar logs técnicos sem dados sensíveis desnecessários.
- Não executar comandos de escrita no modelo sem ação explícita do usuário.
