# IFC vinculado (proposta, 23/11/2026)

> **Status:** só estudo de viabilidade. Nada foi implementado.

## A ideia

Um botão **PASB > Vincular IFC** que liga um IFC (ex.: o da arquitetura) ao desenho do Advance Steel. Quando o arquivo IFC mudar, o modelo 3D no desenho é atualizado, **sempre no mesmo lugar**: um ponto em comum, escolhido uma vez, mantém a posição entre as atualizações.

## Conclusão: é viável

Quase tudo já existe na importação atual (`PASB_IMPORTAR_IFC`):
- leitor xBIM fora do AutoCAD;
- malhas, sólidos e blocos;
- layers e cores por nível;
- XData com o GlobalId de cada peça.

O que falta é guardar o vínculo, detectar a mudança no arquivo, refazer o desenho sem mexer no resto e fixar a posição.

## Abordagem recomendada: IFC → DWG auxiliar → Xref

```
arquitetura.ifc ──(Pasb.IfcImport.exe)──► arquitetura.ifc.pasb.dwg ──(Xref)──► desenho do Advance Steel
                                           (coordenadas do IFC)          (inserido no ponto em comum)
```

1. **Vincular:** o usuário escolhe o IFC e as opções da janela de importação (níveis, tipos, cores, Mesh ou Solid3d). O plugin gera um **DWG auxiliar** ao lado do IFC, num banco de dados separado (`new Database()` e `SaveAs`, sem abrir na tela), e anexa esse DWG como **Xref**.
2. **Ponto em comum:** o usuário diz onde fica, no desenho, um ponto conhecido do IFC. O plugin guarda isso como a posição e a rotação da referência Xref.
3. **Atualizar:** quando o IFC muda, o plugin gera o DWG auxiliar de novo e manda o AutoCAD recarregar a Xref. A referência continua no mesmo lugar, então nada se perde.

Vantagens sobre desenhar dentro do próprio DWG:
- **DWG do Advance Steel leve:** a geometria da arquitetura fica no arquivo auxiliar, não no desenho.
- **Comportamento nativo:** recarregar, descarregar, recortar (XCLIP), congelar layers da Xref e o aviso de "referência modificada" do próprio AutoCAD.
- **Sem risco ao modelo de aço:** a atualização não mexe em nenhuma peça do Advance Steel nem em objetos do usuário.
- **Ctrl+Z:** o vínculo e as atualizações são operações separadas e simples.

Alternativa mais simples, sem Xref: um **bloco** `PASB_IFC_<nome>` dentro do desenho, inserido uma vez no ponto em comum. Atualizar = redefinir o conteúdo do bloco; a inserção (posição) não muda. Funciona, mas o DWG cresce com a arquitetura inteira.

## O ponto em comum (o mais importante)

### Problema encontrado no leitor atual

Hoje o `Pasb.IfcImport` chama `Xbim3DModelContext.CreateContext(..., true)`. Esse `true` liga o **ajuste de coordenadas da xBIM**: o modelo é deslocado para perto da origem, por um valor calculado a partir do conteúdo do arquivo. No IFC do SESI, o site está a 493 km e a importação saiu entre 0 e 144 m.

Para o vínculo, isso é um risco: se a arquitetura mudar o modelo (ex.: acrescentar um bloco), o deslocamento calculado pode mudar e **tudo chega deslocado** na atualização. Isso também contradiz o docs/IFC-IMPORT.md, que diz "coordenadas do IFC, sem deslocamento".

**Correção necessária:** usar `CreateContext(..., false)`, para manter as coordenadas reais do IFC, ou registrar o deslocamento aplicado e desfazê-lo. A geometria grande (UTM) vai para o DWG auxiliar em coordenadas reais, e a Xref traz para perto da origem do desenho. Coordenadas na casa de centenas de km em double têm precisão de ~1e-10 m, sem problema.

### Como fixar o ponto

Opções na janela de vínculo, nesta ordem de preferência:
1. **Coordenadas do IFC (georreferenciado):** usar o IfcSite / `IfcMapConversion` do IFC e o georreferenciamento já configurado na exportação (ponto base + coordenadas reais + ângulo). A arquitetura e o aço ficam no mesmo sistema da obra, sem clique.
2. **Ponto do IFC ↔ ponto do desenho:** o usuário escolhe um ponto do IFC e clica onde ele fica no desenho. O ponto do IFC pode ser uma interseção de eixos (`IfcGrid`), um canto de pilar ou coordenadas digitadas. Opcionalmente, um segundo par de pontos dá a rotação.
3. **Sem deslocamento:** origem do IFC = origem do desenho.

O vínculo guarda, no dicionário do desenho:
- o caminho do IFC (absoluto e relativo ao DWG);
- o hash e a data do arquivo;
- as opções de importação;
- a transformação, com ponto, rotação e escala de unidade.

### Proteção contra a arquitetura mover a origem

Se o arquiteto exportar o IFC com outra origem (no Revit, "coordenadas compartilhadas" × "ponto base do projeto"), a arquitetura inteira se desloca. Para pegar isso:
- **Guardar referências:** no vínculo, guardar o IfcSite e o `IfcMapConversion` do IFC, e a posição de 2 ou 3 objetos estáveis (eixos, níveis, ou os pilares com o mesmo GlobalId).
- **Comparar na atualização:** se esses objetos andaram mais que uma tolerância (ex.: 5 mm), **não atualizar sozinho**. Avisar: "A origem do IFC mudou X m. Manter a posição anterior (corrigir) ou aceitar a nova?".

## Detectar a mudança do IFC

| Momento | Como |
|---|---|
| Ao abrir o desenho | Compara data e hash do IFC com os guardados no vínculo |
| Com o desenho aberto | `FileSystemWatcher` na pasta do IFC, mais uma conferência periódica no `Application.Idle` (unidades de rede como `X:` às vezes não avisam) |
| Manual | Botão **Atualizar IFC vinculado** |

**Recomendação:** "automático" = **avisar e atualizar com um clique** (balão "O IFC da arquitetura mudou. Atualizar agora?"), com opção de atualizar sem perguntar. Atualizar no meio de um comando ou de uma edição pode travar o AutoCAD. A atualização tem que esperar o programa ficar ocioso, e o IFC pode estar sendo gravado pela metade (esperar o arquivo estabilizar por alguns segundos).

## Desempenho

- Leitura e triangulação: ~25 s para o IFC de 38 MB do SESI (33.786 elementos), fora do AutoCAD.
- **Mesh** é o modo recomendado para referência: rápido e leve. **Solid3d** com dezenas de milhares de peças leva minutos.
- **Atualização incremental**, segunda fase: cada peça já guarda o GlobalId em XData. Comparar o hash da geometria e a posição por GlobalId e refazer só o que mudou, foi criado ou foi apagado. Isso deixa a atualização de segundos.

## Riscos e cuidados

- **IFC na rede (`X:`) indisponível:** manter a última versão desenhada e avisar.
- **Vários computadores:** guardar também o caminho relativo ao DWG, e fazer o DWG auxiliar seguir o IFC (mesma pasta).
- **Mais de um IFC vinculado** (arquitetura, instalações...): o vínculo é uma lista, com um Xref por IFC.
- **Layers da Xref** ficam com prefixo `nomedoarquivo|`, e o congelar/cor por layer do usuário é mantido (VISRETAIN).
- **Desvincular:** remove a Xref e o vínculo. O DWG auxiliar pode ser apagado.

## Etapas sugeridas

1. Corrigir o ajuste de coordenadas do leitor (`CreateContext(..., false)` ou registrar o deslocamento), com teste.
2. Gerar o DWG auxiliar a partir do pacote de malhas (mesmo desenho da importação, em banco separado).
3. Comando **Vincular IFC**: janela de importação + ponto em comum + Xref.
4. Detectar mudança (abrir desenho, `FileSystemWatcher`, ocioso) + **Atualizar IFC vinculado**.
5. Proteção contra mudança de origem.
6. Atualização incremental por GlobalId.

Estimativa: etapas 1 a 4 formam uma primeira versão utilizável; 5 e 6 deixam robusto e rápido.
