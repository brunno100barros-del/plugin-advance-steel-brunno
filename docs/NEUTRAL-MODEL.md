# Contrato do modelo neutro

Status: especificação da Etapa 3. Ainda não representa implementação executável nem validação com amostras reais do usuário.

## 1. Objetivo
Definir um formato interno independente do Advance Steel, do protocolo MCP e da biblioteca IFC. O exportador deverá consumir somente este modelo, nunca o JSON bruto do MCP.

## 2. Princípios obrigatórios
- Preservar a resposta original do MCP em uma camada de origem/auditoria, sem torná-la dependência do exportador.
- Não inventar valores ausentes. Propriedades não fornecidas ficam nulas ou ausentes e geram avisos quando forem necessárias.
- Separar identidade da origem, identidade interna e GlobalId IFC.
- Declarar unidades em cada grandeza ou normalizá-las explicitamente para unidades internas.
- Preservar coordenadas locais do desenho antes de qualquer transformação geográfica.
- Manter geometria analítica e representação geométrica de exportação como conceitos distintos.
- Registrar proveniência: ferramenta MCP, handle, tipo/categoria, documento e instante da extração.
- Não assumir que dois objetos com o mesmo handle representam o mesmo objeto se o desenho tiver sido substituído ou reconstruído.

## 3. Estrutura lógica
Cada extração contém:
- sourceDocument: identificação do documento de origem, nome opcional, fingerprint quando disponível e unidade declarada/observada.
- extraction: versão do adaptador, ferramenta MCP, data/hora e avisos.
- elements: coleção de elementos normalizados.
- unmappedData: propriedades originais que ainda não possuem mapeamento, quando tecnicamente possível.

Cada elemento normalizado contém:

| Campo | Finalidade |
|---|---|
| internalId | Identificador interno estável dentro do conjunto exportado |
| sourceSystem | Ex.: AdvanceSteel |
| sourceHandle | Handle recebido do MCP; não equivale a GlobalId |
| sourceType / category | Tipo e categoria informados pela origem |
| ifcGlobalId | Reservado; só preenchido quando houver ID IFC válido e persistente comprovado |
| name / description | Denominação e descrição, se disponíveis |
| material | Referência ou propriedades do material |
| properties | Propriedades tipadas e respectivas unidades |
| geometry | Geometria analítica normalizada e/ou referência à geometria triangulada/sólida |
| placement | Posição e orientação local no sistema do desenho |
| relationships | Ligações com outros elementos, quando comprovadas |
| provenance | Ferramenta, handle e evidência da extração |
| validation | Estado, avisos e dados ausentes |

## 4. Identidade e rastreabilidade
O handle CAD é um identificador de origem, não uma identidade IFC global. A aplicação deverá gerar um internalId para a sessão/conjunto de exportação e criar um manifesto de correspondência entre internalId, documento de origem, handle e IfcGlobalId gerado.

Na primeira versão, o IfcGlobalId será gerado pelo exportador para cada entidade IFC que o exigir. Em reexportações, a preservação do mesmo GlobalId só será permitida se existir uma chave de correspondência confiável entre documento, objeto e versão. Não reutilizar GlobalIds com base apenas em posição, nome ou perfil.

O manifesto de exportação deverá permitir auditoria e comparação entre exportações. A estratégia de persistência entre sessões será decidida após avaliar se o Advance Steel fornece um identificador persistente adequado.

## 5. Geometria
O contrato deve admitir, sem confundir:
1. Geometria paramétrica/analítica: eixo de viga, perfil, espessura, dimensões, furos e parâmetros que a origem realmente fornecer.
2. Geometria BRep/sólida: quando a API ou MCP disponibilizar sólido com topologia suficiente.
3. Geometria tessellada: vértices e faces para fallback explícito, com tolerância e unidades registradas.

Não presumir que start/end, comprimento, perfil e espessura sejam suficientes para reconstruir cortes, furos, recortes, chanfros, ligações ou detalhes de fabricação. Elementos sem geometria confiável devem ser reportados como incompletos ou não exportados, nunca representados silenciosamente como sólidos simplificados.

## 6. Unidades e coordenadas
Adotar metros como unidade interna de comprimento e quilogramas como unidade interna de massa, desde que a conversão a partir da origem seja explícita e testada. Ângulos internos devem usar uma convenção única documentada. Manter também a unidade original declarada/observada.

O sistema local do desenho permanece separado do sistema projetado. O módulo de georreferenciamento aplica a transformação apenas na etapa de exportação IFC, evitando alterar os dados de origem.

## 7. Tipos iniciais
Categorias previstas, sujeitas à confirmação nas respostas reais do MCP:
- beam
- plate
- bolts
- anchors
- weld
- special_part
- connector

A normalização deve aceitar categorias desconhecidas e preservar os dados de origem para que uma nova categoria não quebre a leitura.

## 8. Validação por elemento
Cada elemento recebe um estado:
- Valid: dados mínimos para o mapeamento pretendido presentes.
- Partial: elemento reconhecido, mas faltam propriedades ou geometria.
- Unsupported: não há estratégia de conversão definida.
- Invalid: dados contraditórios ou impossíveis de interpretar.

O estado deve ser calculado por alvo de exportação. Um elemento pode ser válido para propriedades/relatório e parcial para geometria IFC.

## 9. Critérios de aceite do contrato
- Ler categorias conhecidas e desconhecidas sem falhar.
- Preservar handle e proveniência.
- Não confundir handle com GlobalId.
- Normalizar unidades com testes de conversão.
- Representar campos ausentes sem valores artificiais.
- Permitir que o exportador opere sem referência a classes Autodesk ou mensagens MCP.
- Gerar relatório dos elementos válidos, parciais, não suportados e inválidos.

## 10. Pendências para implementação
Antes de congelar o contrato em classes C# e criar conversores, anexar amostras JSON reais de viga, chapa e, se disponíveis, parafuso, chumbador e solda. Validar especialmente a estrutura exata dos campos geométricos, unidades, orientação e propriedades opcionais.
