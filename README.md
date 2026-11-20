<div align="center">

# 🏗️ Plugin Advance Steel — Brunno

**Do modelo estrutural ao IFC4 georreferenciado, sem perder nenhuma propriedade.**

Plugin independente para **Autodesk Advance Steel 2027** que extrai o modelo via [AdvancedSteelMCP](https://github.com/Shame625/AdvancedSteelMCP) e gera um **IFC4 próprio**, posicionado em **SIRGAS 2000 / UTM**.

![Status](https://img.shields.io/badge/status-em%20desenvolvimento-orange?style=for-the-badge)
![Advance Steel](https://img.shields.io/badge/Advance%20Steel-2027-0696D7?style=for-the-badge&logo=autodesk&logoColor=white)
![.NET](https://img.shields.io/badge/.NET-10-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)
![IFC](https://img.shields.io/badge/IFC-4-2E7D32?style=for-the-badge)
![SIRGAS](https://img.shields.io/badge/SIRGAS%202000-UTM-009C3B?style=for-the-badge)

[Visão geral](#-visão-geral) •
[Arquitetura](#-arquitetura) •
[Roadmap](#-roadmap) •
[Documentação](#-documentação)

</div>

---

## ⬇️ Instalar

Baixe o pacote pronto em [`release/`](release/) e siga o passo a passo em **[INSTALACAO.md](INSTALACAO.md)**. Não precisa compilar nem de administrador.

## ✨ Visão geral

Exportar um modelo de estrutura metálica para IFC costuma significar escolher entre duas perdas: **dados** (propriedades que somem no caminho) ou **posição** (o modelo chega na origem e precisa ser reposicionado à mão). Este projeto existe para eliminar as duas.

| | Recurso | O que significa |
|:-:|---|---|
| 🔌 | **Integração MCP** | Lê o modelo do Advance Steel através do AdvancedSteelMCP, sem modificar o repositório original |
| 🧩 | **Modelo neutro** | Camada intermediária independente do CAD, do protocolo e da biblioteca IFC — testável sem abrir o Advance Steel |
| 🗂️ | **Preservação integral** | Todas as propriedades acessíveis — gerais, herdadas, específicas e User Attributes — sem whitelist fechada |
| 🌎 | **Georreferenciamento real** | `IfcProjectedCRS` + `IfcMapConversion` a partir de um ponto de controle e do ângulo com o Norte |
| 🤖 | **Conector IA** | Um assistente de IA (Claude Code) lê o modelo e exporta IFC pelo próprio plugin, só por leitura — [docs/AI-CONNECTOR.md](docs/AI-CONNECTOR.md) |
| 👁️ | **Identidade Coordix** | Janelas e ícones da aba com a identidade visual Coordix · BIMOFFICE (paleta cobre/ink, IBM Plex quando instalada, símbolo do olho) |
| 🧩 | **Macros** | Contraventamento em X com barra redonda e esticador, tudo editável e regerável ([docs/BRACING.md](docs/BRACING.md)) |
| 🔢 | **Numeração itemizada** | Peças iguais recebem a mesma marca (vigas por perfil, chapas por espessura, parafusos por Ø × comprimento), com prévia e filtros ([docs/NUMBERING.md](docs/NUMBERING.md)) |
| 📥 | **Importação de IFC** | Qualquer IFC como sólidos ou malhas, separado por nível com cor por nível ([docs/IFC-IMPORT.md](docs/IFC-IMPORT.md)) |
| 📦 | **Exportador IFC4 próprio** | Não depende do exportador nativo; cada exportação gera um relatório de cobertura e limitações |

## 🧭 Arquitetura

```mermaid
flowchart TD
    AS["🏗️ Advance Steel 2027"] --> PL["Plugin / Adapter"]
    PL --> MCP["AdvancedSteelMCP<br/><sub>servidor existente</sub>"]
    MCP --> CL["MCP Client / Adapter"]
    CL --> NM["🧩 Modelo Neutro"]
    NM --> GEO["🌎 Georreferenciamento<br/><sub>SIRGAS 2000 / UTM</sub>"]
    NM --> IFC["📦 Exportador IFC4"]
    GEO --> IFC
    IFC --> OUT[("Arquivo .ifc<br/>+ relatório de validação")]

    classDef core fill:#1565C0,stroke:#0D47A1,color:#fff;
    classDef out fill:#2E7D32,stroke:#1B5E20,color:#fff;
    class NM,GEO,IFC core;
    class OUT out;
```

**Princípio central:** a API do Advance Steel não contamina o núcleo. O exportador consome **apenas** o modelo neutro, nunca o JSON bruto do MCP. Detalhes em [ARCHITECTURE.md](ARCHITECTURE.md).

### Mapeamento IFC4 previsto

| Advance Steel | IFC4 |
|---|---|
| Vigas / perfis | `IfcBeam` |
| Chapas | `IfcPlate` |
| Parafusos e chumbadores | `IfcMechanicalFastener` |
| Soldas | `IfcFastener` (WELD); o IFC4 não tem `IfcWeld` |
| Peças especiais | tipo específico ou `IfcBuildingElementProxy` |

Propriedades sem equivalente IFC padrão vão para **Property Sets customizados**, preservando nome técnico, valor, tipo, unidade e proveniência. Veja [IFC4-MAPPING.md](docs/IFC4-MAPPING.md).

## 🗺️ Roadmap

| Etapa | Descrição | Status |
|:-:|---|:-:|
| 1 | Fundação do projeto e especificações | ✅ Concluída |
| 2 | Integração com Advance Steel + inventário | 🟢 Diagnóstico próprio validado em vigas, ligações, chapas, parafusos e soldas; outras categorias pendentes |
| 3 | Modelo neutro e preservação integral | 🟢 v1 implementada (propriedades + geometria analítica) |
| 4 | Georreferenciamento (SIRGAS 2000 / UTM ou X,Y,Z + rotação) | 🟢 v1 implementada e testada; validação em visualizador pendente |
| 5 | Exportador IFC4 | 🟡 v1 gerando IFC dos modelos de teste; validação em visualizador e corpo das vigas pendentes |

Lista completa de tarefas e critérios de saída em [docs/ROADMAP.md](docs/ROADMAP.md).

> [!NOTE]
> O projeto já tem código executável (diagnóstico, modelo neutro, georreferenciamento e exportador IFC4 v1). Nada é dado como "pronto" antes de ser validado em modelo real do Advance Steel 2027 e em visualizador IFC independente. Registro dos testes: [docs/DIAGNOSTIC-TEST-LOG.md](docs/DIAGNOSTIC-TEST-LOG.md).

## 📚 Documentação

| Documento | Conteúdo |
|---|---|
| [docs/BRACING.md](docs/BRACING.md) | Macro de contraventamento em X: montagem, parâmetros, edição |
| [docs/NUMBERING.md](docs/NUMBERING.md) | Numeração itemizada: regras, campos, filtros, segurança |
| [docs/IFC-IMPORT.md](docs/IFC-IMPORT.md) | Importação de IFC: níveis, cores, layers, blocos, xBIM |
| [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) | Licenças de terceiros (xBIM, OpenCascade) |
| [INSTALACAO.md](INSTALACAO.md) | Como instalar o plugin em qualquer computador (pacote pronto) |
| [ARCHITECTURE.md](ARCHITECTURE.md) | Módulos, fluxo de dados e responsabilidades |
| [MCP-INTEGRATION.md](MCP-INTEGRATION.md) | Diretrizes de integração com o AdvancedSteelMCP |
| [MCP-REVIEW.md](MCP-REVIEW.md) | Auditoria do código-fonte do MCP |
| [LOCAL-VALIDATION.md](LOCAL-VALIDATION.md) | Roteiro de testes somente leitura no Advance Steel |
| [docs/NEUTRAL-MODEL.md](docs/NEUTRAL-MODEL.md) | Contrato do modelo neutro |
| [docs/PROPERTY-PRESERVATION.md](docs/PROPERTY-PRESERVATION.md) | Política de preservação integral de propriedades |
| [docs/IFC4-MAPPING.md](docs/IFC4-MAPPING.md) | Matriz de mapeamento para IFC4 |
| [IFC-EXPORTER.md](IFC-EXPORTER.md) | Especificação do exportador IFC |
| [GEOREFERENCING.md](GEOREFERENCING.md) | Especificação do georreferenciamento |
| [docs/AI-CONNECTOR.md](docs/AI-CONNECTOR.md) | Conector IA: ponte local, servidor MCP próprio, ferramentas e instalação |
| [decisions/](decisions/) | Registros de decisões de arquitetura (ADRs) |

## 🙏 Créditos

- [AdvancedSteelMCP](https://github.com/Shame625/AdvancedSteelMCP), usado como infraestrutura externa.
- Autodesk Advance Steel é marca registrada da Autodesk, Inc. Este é um projeto independente, sem afiliação com a Autodesk.

<div align="center">

---

Feito com ☕ e aço por **Brunno**

</div>
