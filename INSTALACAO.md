# Instalação do PASB em qualquer computador

Como instalar o plugin **PASB (Coordix · BIMOFFICE)** no Advance Steel 2027 em qualquer computador (do trabalho, de casa ou de colegas), **sem compilar nada** e **sem administrador**.

## O que você precisa

| Item | Observação |
|---|---|
| Windows 10 ou 11, 64 bits | |
| **Advance Steel 2027** instalado | O plugin só carrega no Advance Steel 2027 |
| Internet para baixar o pacote | Mais ou menos 22 MB (inclui o leitor de IFC da importação) |
| Usuário comum do Windows | Não precisa de administrador: o plugin é instalado só para o seu usuário |

Você **não precisa** de Git, .NET SDK, Visual Studio nem do código-fonte.

## 1. Baixar o pacote

1. Abra o repositório: <https://github.com/brunno100barros-del/plugin-advance-steel-brunno>
2. Entre na pasta **`release`** e clique no arquivo **`PASB-<versão>.zip`** (por exemplo, `PASB-0.13.0.zip`).
3. Clique em **Download** (ícone de seta para baixo, à direita, ou em "Download raw file").

> Link direto da versão atual: <https://github.com/brunno100barros-del/plugin-advance-steel-brunno/raw/main/release/PASB-0.13.0.zip>

**Conferir o download (opcional):** o arquivo `PASB-<versão>.zip.sha256`, ao lado do zip, traz o código de verificação. No PowerShell:

```powershell
Get-FileHash "$env:USERPROFILE\Downloads\PASB-0.13.0.zip" -Algorithm SHA256
```

O resultado deve ser igual ao do arquivo `.sha256`, sem diferenciar maiúsculas de minúsculas.

## 2. Instalar

1. **Feche o Advance Steel.** Se precisar, confira no Gerenciador de Tarefas que não ficou nenhum `acad.exe` aberto.
2. **Descompacte o .zip inteiro.** Clique com o botão direito no arquivo, escolha **Extrair tudo...** e confirme em **Extrair**.
   - Não rode o instalador de dentro do .zip sem extrair.
3. Abra a pasta extraída, `PASB-0.13.0`, e dê **dois cliques em `Instalar-PASB.cmd`**.
   - Se o Windows mostrar "O Windows protegeu o computador" (SmartScreen), clique em **Mais informações → Executar assim mesmo**. O instalador é um script de texto; você pode abrir o `Instalar-PASB.ps1` no Bloco de Notas e ler o que ele faz.
4. Espere a mensagem **"Pronto."** e aperte uma tecla para fechar.

O instalador:
- copia a pasta `PASB-Diagnostics.bundle` para `%APPDATA%\Autodesk\ApplicationPlugins\`;
- desbloqueia os arquivos baixados da internet, para o AutoCAD conseguir carregá-los;
- **não altera** nenhum outro plugin nem configuração do AutoCAD.

A importação de IFC usa as bibliotecas livres xBIM e OpenCascade, sem modificação, num programa separado (`Contents\IfcImport`). As licenças estão em [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md), que também vai dentro do pacote.

## 3. Primeiro uso

1. Abra o **Advance Steel 2027**.
2. Aparece o aviso **"Segurança – arquivo executável não assinado"** para `Pasb.Diagnostics.AdvanceSteel.dll`. Clique em **Sempre carregar**.
   - Se clicar em "Carregar uma vez", o aviso volta a cada abertura.
   - Se clicar em "Não carregar", a aba não aparece. Nesse caso, feche o programa, instale de novo e escolha "Sempre carregar".
3. Quando o programa terminar de abrir, aparece a aba **PASB** na faixa de opções:

| Botão | O que faz |
|---|---|
| **Exportar IFC** | IFC4 georreferenciado (SIRGAS 2000/UTM ou X,Y,Z da obra), com todas as propriedades |
| **Importar IFC** | Qualquer IFC como sólidos ou malhas, separado por nível com cor por nível ([docs/IFC-IMPORT.md](docs/IFC-IMPORT.md)) |
| **Menu de ligações** | Lista as ligações do PASB e cria a escolhida |
| **Contraventamento em X** | Ligação nativa de contraventamento com barra redonda e esticador ([docs/BRACING.md](docs/BRACING.md)) |
| **Sistema antidobra** | Ligação nativa de correntes de terça: clique os 2 banzos e selecione as terças ([docs/ANTIDOBRA.md](docs/ANTIDOBRA.md)) |
| **Renderizar** | Janela com a estrutura renderizada na vista atual; salvar, copiar ou inserir na prancha só se quiser ([docs/RENDER.md](docs/RENDER.md)) |
| **Vista realista na prancha** | Janela de vista no layout com estilo Realista, que sai realista na impressão |
| **Numerar itens** | Numeração itemizada: peças iguais recebem a mesma marca, com prévia, filtros e confirmação ([docs/NUMBERING.md](docs/NUMBERING.md)) |
| **Lista de material** | Insere no desenho as tabelas escolhidas: detalhada, resumo de perfis, parafusos, chumbadores e chapas |
| **Diagnóstico seleção / modelo** | Leitura técnica dos objetos, somente leitura |
| **Conector IA** | Liga a ponte para o Claude Code (opcional) |
| **Sobre** | Mostra a versão instalada |

### Macros de ligação (uma vez por versão)

As ligações **Contraventamento em X** e **Sistema antidobra** são ligações nativas do Advance Steel (caixa cinza, edição com duplo clique). Elas precisam ser registradas uma vez a cada versão nova:

1. **Feche o Advance Steel.**
2. Na pasta extraída, dê dois cliques em **`Registrar-Macros-PASB.cmd`**.
3. O Windows pede permissão de administrador uma vez (só para copiar `PasbJoints.dll` para a pasta do Advance Steel): clique em **Sim**.
4. Antes de registrar, ele faz backup dos bancos do Advance Steel em `%LOCALAPPDATA%\PASB\backup-bancos`. Só mexe nas linhas do PASB.

Para desfazer, use **`Remover-Macros-PASB.cmd`** (com o Advance Steel fechado).

## 4. Atualizar para uma versão nova

Baixe o zip da versão nova e repita os passos 1 a 3. O instalador substitui a versão anterior, e as suas configurações são mantidas. Depois de cada atualização, o aviso de segurança aparece **uma vez**: clique de novo em **Sempre carregar**.

## 5. Conector IA (opcional)

Só é necessário se você usa o **Claude Code** nesse computador.

1. Depois de instalar o plugin, dê dois cliques em **`Registrar-Conector-IA.cmd`**, uma vez só.
   - Ele altera somente a entrada `pasb` da configuração do Claude Code (`~\.claude.json`) e faz uma cópia de segurança antes.
2. Feche e abra o Claude Code.
3. No Advance Steel, clique em **PASB > Conector IA**. O botão mostra um ponto verde quando está ligado.

Para desfazer o registro, use **`Remover-Conector-IA.cmd`**.

## 6. Remover

Feche o Advance Steel e dê dois cliques em **`Desinstalar-PASB.cmd`**. Ele apaga só a pasta do plugin. Os diagnósticos em `Documentos\PASB-Diagnostics` e as configurações em `%APPDATA%\PASB` são mantidos.

## Problemas comuns

| Sintoma | O que fazer |
|---|---|
| "O AutoCAD / Advance Steel está aberto" | Feche o programa. Se não resolver, encerre `acad.exe` no Gerenciador de Tarefas |
| A aba PASB não aparece | Veja se clicou em "Não carregar" no aviso de segurança e instale de novo. O registro de início fica em `%LOCALAPPDATA%\PASB\inicio.log` |
| "O servidor do Conector IA está em uso" | Feche o Claude Code e rode o instalador de novo |
| O computador bloqueia scripts (política da empresa) | Peça à TI para liberar a execução de `Instalar-PASB.cmd`. Outra saída é copiar manualmente a pasta `PASB-Diagnostics.bundle` para `%APPDATA%\Autodesk\ApplicationPlugins\` |
| A empresa usa só caminhos confiáveis no AutoCAD (`SECURELOAD`) | Peça à TI para incluir `%APPDATA%\Autodesk\ApplicationPlugins\PASB-Diagnostics.bundle\Contents` nos caminhos confiáveis (`TRUSTEDPATHS`) |

## Para quem desenvolve: gerar um pacote novo

Na raiz do repositório (precisa do .NET SDK e do Advance Steel 2027 instalados):

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File bundle\Build-Release.ps1
```

O script gera `release\PASB-<versão>.zip` e o `.sha256`. Antes de gerar, atualize a versão em `src\Directory.Build.props` e em `bundle\PASB-Diagnostics\PackageContents.xml`.
