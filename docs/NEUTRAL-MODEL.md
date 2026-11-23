# Contrato do modelo neutro

Status: **implementado (v1, 2026-11-20)** em `src/Pasb.Model`, com conversor da saída do diagnóstico (`DiagnosticImporter`), gravação em JSON e CLI `tools/ToNeutral`. Testes: 12/12 (`tests/Pasb.Model.Tests`). Executado sobre saídas reais: Portal.dwg (26 elementos, 2.716 propriedades, 73 relações) e DWG de teste (15 elementos, 903 propriedades, 14 relações), com todas as propriedades preservadas. Geometria e posicionamento ainda não convertidos (ver seção 10).

## 10. Implementação v1: decisões e limites

- Categorias pela cadeia de herança (`Categories`); tipos sem regra viram `other:<Tipo>`, sem descarte.
- Relações: qualquer valor lido com `handle` vira `Relationship`, com o nome técnico da propriedade como tipo (por exemplo `PassiveConstructionObject.Drivers`).
- Unidades só onde comprovadas (Testes 3 e 8): pesos em g → kg, áreas em mm² → m², volume em mm³ → m³, peso específico em kg/mm³ → kg/m³ e comprimentos selecionados em mm → m (somente com `INSUNITS = Millimeters`). Fora disso, só o valor bruto.
- `SourceUniqueId` (GetUniqueId) é guardado separado de `IfcGlobalId`, que permanece nulo até a exportação.
- Objetos que a API não abre viram elementos `Unsupported`, com a identificação (classe AutoCAD).
- Estado `Partial` quando há leitura `ReadError`, `Unavailable` ou `ComplexNotSerialized` (por exemplo, `GetProfType` nas vigas e `getPrimaryJoint` nas ligações).
- Pendente: geometria (eixo, contorno, sólido) e posicionamento normalizados; espessura de solda inferior a ignorar quando não houver cordão inferior.

## 1. Objetivo
Definir um formato interno independente do Advance Steel, do protocolo MCP e da biblioteca IFC. O exportador deverá consumir somente este modelo, nunca o JSON bruto do MCP.

## 2. Princípios obrigatórios
- Preservar a resposta original do MCP em uma camada de origem/auditoria.
- Não usar whitelist fechada de propriedades: capturar todas as propriedades que forem acessíveis por meios suportados no Advance Steel.
- Não inventar valores ausentes. Distinguir vazio, zero, falso, nulo, não aplicável, indisponível e erro de leitura.
- Separar identidade da origem, identidade interna e GlobalId IFC.
- Declarar unidades em cada grandeza ou normalizá-las explicitamente para unidades internas.
- Preservar coordenadas locais do desenho antes de qualquer transformação geográfica.
- Registrar proveniência de cada propriedade: método de leitura, classe, ferramenta, documento e instante.
- Aceitar propriedades desconhecidas sem falhar nem descartá-las.

## 3. Estrutura lógica
Cada extração contém:
- sourceDocument: identificação do documento de origem, nome opcional, fingerprint quando disponível e unidade declarada/observada.
- extraction: versão do adaptador, ferramenta MCP, data/hora e avisos.
- elements: coleção de elementos normalizados.
- rawSourceData: resposta original ou referência íntegra à resposta original.
- propertyInventory: inventário de propriedades descobertas por classe/categoria.
- unmappedData: propriedades que ainda não possuem mapeamento IFC.

Cada elemento normalizado contém:

| Campo | Finalidade |
|---|---|
| internalId | Identificador interno no conjunto exportado |
| sourceSystem | AdvanceSteel |
| sourceHandle | Handle de origem; não equivale a GlobalId |
| sourceType / category | Tipo e categoria informados pela origem |
| ifcGlobalId | GlobalId IFC gerado ou preservado com identidade comprovada |
| properties | Coleção aberta de propriedades tipadas, sem whitelist fechada |
| geometry | Geometria analítica, sólida e/ou tessellada |
| placement | Posição e orientação local no desenho |
| relationships | Relações com outros elementos, quando comprovadas |
| provenance | Ferramenta, classe, método e evidência da extração |
| validation | Estado, avisos e dados ausentes |

Cada propriedade deve guardar:
- technicalName: nome técnico original;
- displayName: rótulo amigável, quando disponível;
- rawValue: valor original;
- normalizedValue: valor convertido, quando necessário;
- dataType: tipo original;
- unit: unidade original e unidade normalizada, quando aplicável;
- source: API, getter, User Attribute, MCP ou outra origem;
- status: Read, Empty, Null, NotApplicable, Unavailable ou ReadError.

## 4. Identidade e rastreabilidade
O handle CAD é um identificador de origem, não uma identidade IFC global. Criar manifesto entre internalId, documento, handle e IfcGlobalId. Em reexportações, preservar GlobalIds somente quando houver chave confiável. Não reutilizar IDs com base apenas em posição, nome ou perfil.

## 5. Geometria
Admitir:
1. Geometria paramétrica/analítica: eixos, perfis, espessuras, dimensões, furos e parâmetros realmente fornecidos.
2. Geometria BRep/sólida, quando a API fornecer topologia suficiente.
3. Geometria tessellada, com tolerância e unidades registradas.

Não presumir que eixo, comprimento, perfil ou espessura reconstruam cortes, furos, recortes, chanfros, ligações ou detalhes de fabricação. Limitações devem aparecer no relatório.

## 6. Unidades e coordenadas
Adotar metros como unidade interna de comprimento e quilogramas como unidade interna de massa, com conversão explícita e testada. Manter valores e unidades originais. O sistema local permanece separado do sistema projetado.

## 7. Categorias
Categorias iniciais: beam, plate, bolts, anchors, weld, special_part e connector. Aceitar categorias desconhecidas e preservar seus dados.

## 8. Validação
Estados de elemento: Valid, Partial, Unsupported e Invalid. Estado de propriedade: Read, Empty, Null, NotApplicable, Unavailable e ReadError. Cobertura deve ser calculada por classe/categoria e propriedade, não apenas pelo número de objetos.

## 9. Critérios de aceite
- Nenhuma whitelist fechada de propriedades.
- Inventário e captura de propriedades gerais, herdadas, específicas e User Attributes.
- Preservar nome técnico, valor, tipo, unidade e proveniência.
- Preservar propriedades sem mapeamento IFC.
- Distinguir estados de valor.
- Exportador independente de classes Autodesk e JSON MCP.
- Relatório de cobertura e perdas.
