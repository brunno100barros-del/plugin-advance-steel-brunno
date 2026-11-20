# Especificação inicial do exportador IFC

## Requisito principal

Gerar um arquivo IFC próprio a partir do modelo neutro alimentado pela integração com Advance Steel. Não utilizar o exportador IFC nativo do Advance Steel como mecanismo de geração.

## Estratégia

1. Receber elementos e propriedades do modelo neutro.
2. Validar unidades, identificadores e dados mínimos.
3. Criar projeto, unidades, contexto geométrico e estrutura espacial IFC.
4. Converter geometria e semântica dos elementos suportados.
5. Aplicar georreferenciamento.
6. Gravar arquivo e relatório de validação.

## Preservação de dados

A conversão deverá buscar preservar, quando disponíveis na origem:

- identificador de origem e GlobalId IFC;
- classificação/tipo de elemento;
- geometria e posicionamento;
- perfil e dimensões;
- material;
- propriedades e quantidades;
- ligações e componentes, incluindo chapas, parafusos e soldas, quando a extração fornecer dados suficientes.

Não declarar preservação completa até testar cada categoria. Objetos sem mapeamento semântico confiável deverão ser identificados como não suportados ou exportados por uma representação geométrica explicitamente documentada.

## Seleção da biblioteca

A biblioteca será escolhida após verificar compatibilidade com o runtime exigido pelo Advance Steel 2027, suporte ao IFC4 e capacidade de gravar as entidades de georreferenciamento necessárias.

## Validação de saída

Cada exportação deverá gerar um relatório com contagem por categoria, elementos ignorados, propriedades não mapeadas, avisos, unidades e parâmetros de georreferenciamento.

## Implementação (v1, 2026-11-20)

`src/Pasb.IfcExport`, sem biblioteca IFC externa: gera o texto STEP (ISO 10303-21) diretamente. Isso evita baixar dependências; a troca por uma biblioteca continua possível, porque o exportador só consome o modelo neutro. CLI: `tools/ToIfc`. Testes: `tests/Pasb.IfcExport.Tests` (11/11).

### Mapeamento adotado
| Categoria do modelo neutro | IFC4 | Geometria |
|---|---|---|
| beam | `IfcBeam` (BEAM) | Só eixo (`Axis`, `Curve3D`). Corpo pendente: faltam as dimensões da seção |
| plate | `IfcPlate` (SHEET) | `IfcExtrudedAreaSolid` do contorno externo, extrudado pela espessura no sentido da normal |
| bolts / anchors | `IfcMechanicalFastener` (BOLT / ANCHORBOLT), com `NominalDiameter` e `NominalLength` | Um cilindro por parafuso (Ø × comprimento) centrado no ponto médio |
| weld | `IfcFastener` (WELD); **IFC4 não tem `IfcWeld`** (correção do mapeamento anterior) | Sem corpo |
| joint, connection, joint_parameters | `IfcGroup` + `IfcRelAssignsToGroup` com os membros exportados | — |
| internal (não abertos) | não exportado; contado no relatório | — |
| outras | `IfcBuildingElementProxy` | Sem corpo |

### Propriedades
- Um Pset por classe declaradora, `AS_<Classe>` (por exemplo `AS_AtomicElement`, `AS_Beam`), mais `AS_Origin` (handle, tipo, GUID de origem, categoria e estado). O prefixo evita colisão com os `Pset_` padrão.
- Nome = nome técnico original (com argumento, por exemplo `GetUserAttribute(3)`). Descrição = estado + unidade + erro.
- Valores: `IfcBoolean`, `IfcInteger`, `IfcReal`, `IfcText`; enums como `IfcLabel("kNome (valor)")`; estruturas como JSON em `IfcText`. Vazio = `IfcText('')`; nulo, indisponível ou erro = sem valor (`$`), com o estado na descrição.
- Valores com unidade comprovada ganham uma propriedade extra `"<nome> [SI]"`, com a unidade SI referenciada.

### Identidade
`GlobalId` = GUID do Advance Steel (`GetUniqueId`) comprimido para 22 caracteres, quando existe; senão, determinístico a partir do handle. **A persistência do `GetUniqueId` entre sessões ainda não foi verificada.** Psets e relações têm GlobalId determinístico.

### Georreferenciamento
`IfcProjectedCRS` + `IfcMapConversion` sobre o contexto `Model`, nos modos A (EPSG) e B (CRS descritivo), conforme [GEOREFERENCING.md](GEOREFERENCING.md). Geometria em metros, `Scale = 1`.

### Resultado com dados reais (diagnósticos dos Testes 7 e 8)
| Modelo | Entidades | Elementos | Psets / propriedades |
|---|---|---|---|
| Portal.dwg (modo A, 22S, coordenadas de exemplo) | 3.212 | 9 `IfcBeam`, 13 `IfcGroup` | 116 / 2.880 |
| DWG de teste (modo B, exemplo) | 1.138 | `IfcPlate`, `IfcMechanicalFastener` (4 cilindros), `IfcFastener`, 6 `IfcGroup`, 1 proxy | 46 / 973 |

Checagens automáticas nos dois arquivos: ids sequenciais, nenhuma referência quebrada, parênteses balanceados e nenhum caractere fora de ASCII.

### Validação em visualizador independente — Trimble Connect (2026-11-20)
- `portal.ifc` e `teste-chapa.ifc` carregados sem erro no visualizador 3D do Trimble Connect.
- Geometria: chapa extrudada e 4 cilindros de parafuso visíveis; vigas visíveis só como eixo (esperado).
- Propriedades: Psets `AS_*` exibidos (por exemplo, na chapa C7, `Material = S235JRG2`, `Number Of Holes = 4`, `Main Part Number = 49`, `Preliminary Part Number = "indefinido"`). Viga: `Common Type = BEAM`, `Product Object Type = StraightBeam`, GlobalId IFC de 22 caracteres lido corretamente.
- **Georreferenciamento não confirmado:** os dois modelos, com coordenadas de destino muito diferentes (modo A em E≈500.000/N≈7.400.000; modo B em X≈1000/Y≈2000), apareceram lado a lado. O visualizador não aplicou `IfcMapConversion` nessa visualização, ou alinhou os modelos pela origem local. Falta validar a posição em outro visualizador ou com uma configuração de georreferenciamento do Trimble.

### Corpo das vigas — validado (Teste 10, 2026-11-20)
- Diagnóstico 0.2.3 serializou `ProfileTypeU` (H, B, Tw, Tf, R1, R2, E). Perfis compostos não têm dimensões.
- Regras confirmadas com as caixas envolventes reais e com o desenvolvedor no Trimble Connect (`portal-v2.ifc` "igual ao Advance Steel"):
  - o eixo do Advance Steel passa pelo centroide do U (E a partir das costas da alma); as mesas abrem no sentido do Y local; H segue o Z local;
  - comprimento físico = comprimento do sistema − `dStart` − `dEnd` (offset negativo alonga);
  - cortes de serra das mesas aplicados por planos (`IfcBooleanClippingResult`). A correspondência início/fim do corte se inverte em seção espelhada, e o ângulo efetivo é −ângulo no "Start" e +ângulo no "End";
  - vigas compostas sem representação gráfica; a forma vem das componentes.
- Pendente: ângulos de corte na alma (`webAngle`), recortes e furos em vigas, perfis I e outros tipos (só U validado).

### Não validado / pendente
- Posição georreferenciada em visualizador (ver acima).
- Corpo das vigas (dimensões da seção: diagnóstico 0.2.3 serializa `ProfileType`, ainda não executado), furos e recortes das chapas, `Portioning` das chapas, trajetória das soldas, cabeça e porca dos parafusos.
- Comparação automática propriedade a propriedade entre o IFC e a origem (IFC-EXPORTER.md, critérios).

## IFC2x3

Não faz parte do primeiro alvo. A necessidade e a estratégia de compatibilidade serão avaliadas após validar o fluxo IFC4.
