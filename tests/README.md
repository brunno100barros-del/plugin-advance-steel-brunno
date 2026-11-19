# Testes

Os testes serão organizados por nível:

- **Unitários:** transformação de coordenadas, conversão de unidades e validações do modelo neutro.
- **Integração MCP:** comunicação com servidor real e verificação dos schemas de resposta.
- **Integração Advance Steel:** carregamento do add-in e extração em modelos controlados.
- **Validação IFC:** leitura do arquivo exportado por ferramenta independente e comparação de geometria, propriedades e georreferenciamento.

Os casos de teste devem registrar arquivo/modelo de origem, ambiente, versão, resultado esperado e tolerância. Não usar arquivos de projeto reais ou dados sensíveis sem autorização.
