using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Autodesk.Windows;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace Pasb.Diagnostics.AdvanceSteel;

/// <summary>
/// Aba "PASB" na faixa de opções (ribbon) do Advance Steel. Cada botão só envia um comando PASB_* para a linha de comando;
/// nenhuma lógica de leitura ou exportação roda aqui.
/// </summary>
internal static class PasbRibbon
{
    private const string TabId = "PASB_TAB";
    private static bool _hooked;
    private static RibbonButton? _connector;

    /// <summary>Chamado pelo Initialize do plugin. Nunca lança: falha na aba não pode impedir o AutoCAD de abrir.</summary>
    public static void Initialize()
    {
        try
        {
            if (ComponentManager.Ribbon == null) ComponentManager.ItemInitialized += OnItemInitialized;
            else Create();
            if (!_hooked)
            {
                // Trocar de espaço de trabalho recria a faixa de opções: recolocar a aba.
                AcApp.SystemVariableChanged += (_, e) => { if (string.Equals(e.Name, "WSCURRENT", StringComparison.OrdinalIgnoreCase)) SafeCreate(); };
                _hooked = true;
            }
        }
        catch (Exception ex) { StartupLog.Write("Aba PASB (Initialize) falhou: " + ex); }
    }

    private static void OnItemInitialized(object? sender, RibbonItemEventArgs e)
    {
        if (ComponentManager.Ribbon == null) return;
        ComponentManager.ItemInitialized -= OnItemInitialized;
        SafeCreate();
    }

    private static void SafeCreate() { try { Create(); } catch (Exception ex) { StartupLog.Write("Aba PASB (Create) falhou: " + ex); } }

    private static void Create()
    {
        RibbonControl? ribbon = ComponentManager.Ribbon;
        if (ribbon == null || ribbon.FindTab(TabId) != null) return;

        var tab = new RibbonTab { Title = "PASB", Id = TabId };
        tab.Panels.Add(Panel("IFC", Large("Exportar\nIFC", "PASB_EXPORTAR_IFC", "IFC", false,
            "Exporta o modelo para IFC4 georreferenciado, com todas as propriedades do Advance Steel."),
            Large("Importar\nIFC", "PASB_IMPORTAR_IFC", "IMP", false,
            "Importa qualquer IFC como sólidos (plotam) ou malhas (leves), com níveis, cores por nível, layers e blocos.")));
        tab.Panels.Add(Panel("Macros",
            Large("Contraventa-\nmento em X", "PASB_CONTRAVENTAMENTO", "CV", false,
                "Contraventamento em X com barra redonda e esticador: clique os 2 cantos opostos (plano XY, cota Z do 1º ponto) e ajuste todos os parâmetros na janela."),
            Large("Editar\ncontravent.", "PASB_EDITAR_CONTRAVENTAMENTO", "ED", false,
                "Selecione uma peça de um contraventamento PASB para reabrir os parâmetros e regerar.")));
        tab.Panels.Add(Panel("Numeração", Large("Numerar\nitens", "PASB_NUMERAR_ITENS", "Nº", false,
            "Numeração itemizada, como no Revit: peças iguais ganham o mesmo item (vigas por perfil, chapas por espessura, parafusos por diâmetro x comprimento), com prévia e filtros antes de gravar.")));
        tab.Panels.Add(Panel("Listas", Large("Lista de\nmaterial", "PASB_LISTA_MATERIAL", "LM", false,
            "Lê o modelo (sem alterá-lo) e insere no desenho uma tabela com perfis, chapas e parafusos: comprimento, quantidade, peso e pintura.")));
        tab.Panels.Add(Panel("Diagnóstico",
            Large("Diagnóstico\nseleção", "PASB_DIAG_SEL", "SEL", false, "Lê os objetos selecionados (somente leitura) e grava o resultado em Documentos\\PASB-Diagnostics."),
            Large("Diagnóstico\nmodelo", "PASB_DIAG_ALL", "ALL", false, "Lê todos os objetos do modelo (somente leitura).")));
        _connector = Large("Conector\nIA", "PASB_CONECTOR_IA", "IA", true,
            "Liga/desliga a ponte local para o assistente de IA (Claude) ler o modelo e exportar IFC. Nenhuma ferramenta altera o desenho.");
        ApplyConnectorLook(AiConnector.IsRunning);
        tab.Panels.Add(Panel("IA", _connector));
        tab.Panels.Add(Panel("PASB", Large("Sobre", "PASB_DIAG_VERSION", "i", false, "Mostra a versão instalada do plugin.")));
        ribbon.Tabs.Add(tab);
    }

    /// <summary>Atualiza o botão "Conector IA" (ligado em verde, desligado em roxo). Nunca lança.</summary>
    public static void SetConnectorState(bool on)
    {
        try { ApplyConnectorLook(on); } catch (Exception ex) { StartupLog.Write("Botão Conector IA falhou: " + ex); }
    }

    private static void ApplyConnectorLook(bool on)
    {
        if (_connector == null) return;
        System.Drawing.Color? dot = on ? Pasb.Ui.Brand.Ok : null;
        _connector.Text = on ? "Conector IA\n(ligado)" : "Conector\nIA";
        _connector.LargeImage = Icon("IA", true, 32, dot);
        _connector.Image = Icon("IA", true, 16, dot);
    }

    private static RibbonPanel Panel(string title, params RibbonItem[] items)
    {
        var src = new RibbonPanelSource { Title = title };
        foreach (RibbonItem i in items) src.Items.Add(i);
        return new RibbonPanel { Source = src };
    }

    private static RibbonButton Large(string text, string command, string glyph, bool eye, string tooltip) => new()
    {
        Text = text,
        ShowText = true,
        ShowImage = true,
        Size = RibbonItemSize.Large,
        Orientation = Orientation.Vertical,
        LargeImage = Icon(glyph, eye, 32),
        Image = Icon(glyph, eye, 16),
        ToolTip = tooltip,
        CommandParameter = "\x03\x03" + command + " ",   // cancela comando em andamento e executa
        CommandHandler = new SendCommand(),
    };

    /// <summary>Ícone com a identidade Coordix (cartão escuro, sigla em cobre ou o símbolo do olho), gerado em código.</summary>
    private static ImageSource Icon(string glyph, bool eye, int size, System.Drawing.Color? dot = null)
    {
        byte[] png = Pasb.Ui.Brand.RibbonIconPng(glyph, size, eye, dot);
        var img = new BitmapImage();
        img.BeginInit();
        img.CacheOption = BitmapCacheOption.OnLoad;
        img.StreamSource = new System.IO.MemoryStream(png);
        img.EndInit();
        img.Freeze();
        return img;
    }

    private sealed class SendCommand : ICommand
    {
        public event EventHandler? CanExecuteChanged { add { } remove { } }
        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter)
        {
            if (parameter is RibbonButton { CommandParameter: string cmd })
                AcApp.DocumentManager.MdiActiveDocument?.SendStringToExecute(cmd, true, false, true);
        }
    }
}
