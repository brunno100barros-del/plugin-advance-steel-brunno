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
        catch (Exception) { }
    }

    private static void OnItemInitialized(object? sender, RibbonItemEventArgs e)
    {
        if (ComponentManager.Ribbon == null) return;
        ComponentManager.ItemInitialized -= OnItemInitialized;
        SafeCreate();
    }

    private static void SafeCreate() { try { Create(); } catch (Exception) { } }

    private static void Create()
    {
        RibbonControl? ribbon = ComponentManager.Ribbon;
        if (ribbon == null || ribbon.FindTab(TabId) != null) return;

        var tab = new RibbonTab { Title = "PASB", Id = TabId };
        tab.Panels.Add(Panel("IFC", Large("Exportar\nIFC", "PASB_EXPORTAR_IFC", "IFC", Color.FromRgb(0, 122, 204),
            "Exporta o modelo para IFC4 georreferenciado, com todas as propriedades do Advance Steel.")));
        tab.Panels.Add(Panel("Diagnóstico",
            Large("Diagnóstico\nseleção", "PASB_DIAG_SEL", "SEL", Color.FromRgb(90, 90, 96), "Lê os objetos selecionados (somente leitura) e grava o resultado em Documentos\\PASB-Diagnostics."),
            Large("Diagnóstico\nmodelo", "PASB_DIAG_ALL", "ALL", Color.FromRgb(90, 90, 96), "Lê todos os objetos do modelo (somente leitura).")));
        _connector = Large("Conector\nIA", "PASB_CONECTOR_IA", "IA", Color.FromRgb(120, 70, 170),
            "Liga/desliga a ponte local para o assistente de IA (Claude) ler o modelo e exportar IFC. Nenhuma ferramenta altera o desenho.");
        ApplyConnectorLook(AiConnector.IsRunning);
        tab.Panels.Add(Panel("IA", _connector));
        tab.Panels.Add(Panel("PASB", Large("Sobre", "PASB_DIAG_VERSION", "i", Color.FromRgb(60, 140, 90), "Mostra a versão instalada do plugin.")));
        ribbon.Tabs.Add(tab);
    }

    /// <summary>Atualiza o botão "Conector IA" (ligado em verde, desligado em roxo). Nunca lança.</summary>
    public static void SetConnectorState(bool on)
    {
        try { ApplyConnectorLook(on); } catch (Exception) { }
    }

    private static void ApplyConnectorLook(bool on)
    {
        if (_connector == null) return;
        Color c = on ? Color.FromRgb(40, 160, 80) : Color.FromRgb(120, 70, 170);
        _connector.Text = on ? "Conector IA\n(ligado)" : "Conector\nIA";
        _connector.LargeImage = Icon("IA", c, 32);
        _connector.Image = Icon("IA", c, 16);
    }

    private static RibbonPanel Panel(string title, params RibbonItem[] items)
    {
        var src = new RibbonPanelSource { Title = title };
        foreach (RibbonItem i in items) src.Items.Add(i);
        return new RibbonPanel { Source = src };
    }

    private static RibbonButton Large(string text, string command, string glyph, Color color, string tooltip) => new()
    {
        Text = text,
        ShowText = true,
        ShowImage = true,
        Size = RibbonItemSize.Large,
        Orientation = Orientation.Vertical,
        LargeImage = Icon(glyph, color, 32),
        Image = Icon(glyph, color, 16),
        ToolTip = tooltip,
        CommandParameter = "\x03\x03" + command + " ",   // cancela comando em andamento e executa
        CommandHandler = new SendCommand(),
    };

    /// <summary>Ícone gerado em código (quadrado colorido com texto), sem arquivos de imagem externos.</summary>
    private static ImageSource Icon(string glyph, Color color, int size)
    {
        var visual = new DrawingVisual();
        using (DrawingContext dc = visual.RenderOpen())
        {
            double r = size / 6.0;
            dc.DrawRoundedRectangle(new SolidColorBrush(color), null, new Rect(1, 1, size - 2, size - 2), r, r);
            var ft = new FormattedText(glyph, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
                glyph.Length >= 3 ? size * 0.34 : size * 0.55, Brushes.White, 1.0);
            dc.DrawText(ft, new Point((size - ft.Width) / 2, (size - ft.Height) / 2));
        }
        var bmp = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
        bmp.Render(visual);
        bmp.Freeze();
        return bmp;
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
