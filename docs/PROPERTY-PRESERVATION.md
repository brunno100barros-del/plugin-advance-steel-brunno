# Política de preservação integral de propriedades

Status: requisito de produto aprovado. A cobertura real depende daquilo que a API do Advance Steel 2027 permitir ler e do que o MCP disponibilizar ou vier a ser ampliado para expor.

## 1. Requisito

O exportador deverá preservar **todas as propriedades de cada objeto que forem acessíveis por meios suportados no Advance Steel**, e não somente uma lista pré-selecionada de campos considerados importantes.

Não será adotada uma whitelist fechada de propriedades como limite da exportação. A solução deverá descobrir, capturar, tipar e transportar propriedades de forma extensível.

A expressão "todas as propriedades" significa:
- propriedades gerais herdadas pelas classes-base;
- propriedades específicas de cada classe de objeto;
- atributos de usuário (User Attributes);
- atributos de conexões/juntas quando acessíveis e associados ao objeto;
- propriedades de fabricação, montagem, identificação, materiais, revestimentos, perfis, posicionamento e comportamento;
- propriedades customizadas ou não mapeadas, quando a API fornecer acesso a elas.

Não significa que o IFC possa representar nativamente toda estrutura interna proprietária do Advance Steel. O que não tiver equivalente IFC será preservado como propriedade complementar, desde que seja possível ler seu valor e tipo.

## 2. Estratégia de descoberta

A implementação deverá realizar inventário por classe/categoria, incluindo:
1. propriedades públicas acessíveis pela API .NET/COM aplicável;
2. propriedades herdadas das classes-base;
3. getters específicos por tipo;
4. User Attributes e seus índices/nomes/seções;
5. dados serializados de juntas/objetos, somente quando houver API documentada e acesso seguro;
6. metadados e valores de catálogo referenciados pelo objeto.

Não assumir que reflexão sobre propriedades .NET revela todos os atributos internos do Advance Steel. A reflexão será uma fonte de inventário, combinada com métodos oficiais de leitura e mapeamentos específicos.

## 3. Captura sem perda

Para cada propriedade capturada, registrar:
- nome técnico original;
- rótulo amigável, se disponível;
- valor bruto e valor normalizado;
- tipo de dado original;
- unidade, se aplicável;
- origem (API, getter, User Attribute, MCP ou outro método);
- classe/categoria do objeto;
- estado: lida, vazia, indisponível, erro de leitura ou não aplicável.

Distinguir valor vazio, zero, falso, nulo e propriedade não acessível. Não converter esses estados uns nos outros.

Preservar os dados brutos da origem para auditoria. Não descartar propriedades desconhecidas durante a normalização.

## 4. Representação no IFC4

Aplicar duas camadas complementares:

### 4.1 Propriedades com semântica IFC
Mapear para atributos, relações, materiais, classificações, quantidades e Property Sets padronizados quando o significado e o tipo forem compatíveis com o schema IFC4.

### 4.2 Propriedades Advance Steel
Exportar todas as demais propriedades capturadas em Property Sets customizados, agrupados por classe/categoria e origem. Preservar o nome técnico original, valor, tipo e unidade. Um nome amigável pode ser adicionado sem substituir o nome original.

Não sobrescrever Psets padronizados. Evitar colisões de nomes e registrar o mapeamento no relatório.

## 5. Limites e transparência

Algumas propriedades podem ser calculadas internamente, armazenadas em estruturas proprietárias, depender de contexto de conexão, estar indisponíveis para determinados tipos ou não ser expostas pela versão da API/MCP. O exportador deverá:
- registrar quais propriedades foram descobertas;
- registrar quais foram efetivamente lidas;
- registrar propriedades com erro, indisponíveis ou não aplicáveis;
- apresentar cobertura por classe/categoria;
- nunca declarar "100% de propriedades preservadas" sem inventário comparativo verificável.

Se o MCP atual não expuser um atributo necessário, primeiro investigar se a API oficial do Advance Steel 2027 permite obtê-lo por uma extensão/adaptador próprio. Isso não implica modificar o repositório original AdvancedSteelMCP: a extensão pode permanecer em nosso projeto independente.

## 6. Critérios de aceite

- Inventário das propriedades por classe/categoria de objeto.
- Cobertura de propriedades herdadas e específicas.
- Captura dos User Attributes.
- Preservação de propriedades desconhecidas e customizadas.
- Valores, tipos e unidades preservados sem confundir vazio/zero/nulo/erro.
- Exportação de propriedades IFC padrão quando houver correspondência correta.
- Exportação das demais propriedades em Psets customizados.
- Relatório de cobertura e de todas as perdas/indisponibilidades.
- Testes de ida: comparar os valores do Advance Steel com os valores lidos no IFC por visualizador ou parser independente.
