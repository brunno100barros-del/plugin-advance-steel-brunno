# Desempenho da exportação IFC (versão 0.13.1)

## Problema

Até a 0.13.0, a exportação IFC (e a lista de material, a numeração e a leitura do modelo pelo Conector IA) lia o modelo com o **diagnóstico completo**, feito para localizar leituras que derrubam o AutoCAD, não para ser rápido:

| Causa | Efeito |
|---|---|
| `TraceLog` forçava a gravação física no disco (`Flush(flushToDisk: true)`) **antes de cada leitura** | Cerca de 100 leituras por objeto: um modelo com 5.000 objetos fazia ~500 mil gravações físicas. Muito pior com a pasta Documentos no OneDrive ou com antivírus |
| `AtomicElement.GetQuantityInModel()` em cada objeto | Conta as peças iguais no modelo inteiro a cada chamada: tempo provavelmente quadrático no número de objetos |
| `AtomicElement.GetModelerBodyList(kNormal)` em cada objeto | Calcula o sólido de cada peça só para contar os corpos |
| `objects.jsonl` gravado no disco a cada objeto | Uma gravação por objeto |

Nenhuma dessas leituras é usada pela geometria (`Pasb.Model/Geometry.cs`) nem pelo IFC.

## Solução: dois perfis de leitura (`ReadProfile`)

| | `Diagnostic` | `Export` (trabalho) |
|---|---|---|
| Usado por | `PASB_DIAG_SEL`, `PASB_DIAG_ALL`, leitura de objetos pelo Conector IA | Exportar IFC, lista de material, numeração, leitura do modelo e exportação pelo Conector IA |
| Trace | Uma linha por leitura, gravada no disco físico | Três linhas por objeto (abrir, tipo, gravado), entregues ao Windows sem esperar o disco |
| `objects.jsonl` | Gravado a cada objeto | Com buffer; completo ao final |
| Leituras | Todas | Todas, menos `GetQuantityInModel` e `GetModelerBodyList` |

Garantias mantidas no perfil `Export`:

- Mesma transação com escrita descartada e mesma conferência de `DBMOD`: o modelo continua sem alteração.
- Se o AutoCAD cair, o `trace.log` ainda mostra o objeto que estava sendo lido (o Windows grava o que já recebeu). Para achar a leitura exata, rode o Diagnóstico nesse objeto.
- As leituras omitidas **não somem em silêncio**: o `run.json` lista `omittedReads` e o relatório da exportação (`.report.json`, `Warnings`) avisa quais foram omitidas.

## Medição

A exportação agora informa o tempo de cada etapa:

- na linha de comando, a cada 200 objetos: objetos lidos, tempo decorrido e estimativa do restante;
- na janela final e no `.report.json` (`TimingsMs`): `leitura`, `conversao`, `ifc`, `gravacao` e `total`.

Se a exportação ainda estiver lenta, compare os horários do `trace.log` entre os objetos. Se o intervalo cresce ao longo do arquivo, há outra leitura com custo proporcional ao modelo. Se `leitura` for pequena e `ifc` grande, o gargalo está no exportador.

## Validação

- Testes automáticos: `TraceLog` rápido sobrevive sem `Dispose`; `objects.jsonl` com buffer completo após `Finish`; leituras omitidas viram aviso sem marcar os elementos como parciais.
- **Pendente:** medir antes/depois num modelo real do Advance Steel 2027. O ganho não foi medido; a análise é do código.
