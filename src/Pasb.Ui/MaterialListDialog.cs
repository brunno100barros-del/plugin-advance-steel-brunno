using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Windows.Forms;

namespace Pasb.Ui;

/// <summary>Opções da lista de material (lembradas em %APPDATA%\PASB\material-list-settings.json).</summary>
public sealed class MaterialListSettings
{
    public bool Detailed { get; set; } = true;
    public bool ProfileSummary { get; set; } = true;
    public bool Bolts { get; set; } = true;
    public bool Anchors { get; set; } = true;
    public bool Plates { get; set; } = true;
    /// <summary>Altura do texto em unidades do desenho; 0 = usar a sugestão (2,5 mm × escala de anotação).</summary>
    public double TextHeight { get; set; }

    public bool AnySelected => Detailed || ProfileSummary || Bolts || Anchors || Plates;

    public static string DefaultPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PASB", "material-list-settings.json");

    public static MaterialListSettings Load(string? path = null)
    {
        try
        {
            string p = path ?? DefaultPath;
            return File.Exists(p) ? JsonSerializer.Deserialize<MaterialListSettings>(File.ReadAllText(p)) ?? new() : new();
        }
        catch (Exception) { return new(); }
    }

    public void Save(string? path = null)
    {
        try
        {
            string p = path ?? DefaultPath;
            Directory.CreateDirectory(Path.GetDirectoryName(p)!);
            File.WriteAllText(p, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));
        }
        catch (Exception) { }
    }
}

/// <summary>Janela "Lista de material": escolha das tabelas e da altura do texto. OK = inserir.</summary>
public sealed class MaterialListDialog : Form
{
    private static readonly Color Back = Brand.BgDark, Panel_ = Brand.Ink,
        Field = Brand.Field, Text_ = Brand.BgLight, Muted = Brand.Muted,
        Accent = Brand.Copper;

    public MaterialListSettings Settings { get; }
    public List<string> LastErrors { get; private set; } = new();

    private readonly CheckBox _detailed = Check("Lista detalhada (perfis, chapas e parafusos por comprimento)");
    private readonly CheckBox _summary = Check("Resumo de perfis (um item por perfil: comprimento total, kg/m, kg, pintura)");
    private readonly CheckBox _bolts = Check("Parafusos (quantidade por diâmetro x comprimento, classe e norma)");
    private readonly CheckBox _anchors = Check("Chumbadores (quantidade por diâmetro x comprimento)");
    private readonly CheckBox _plates = Check("Chapas (agrupadas por espessura: quantidade, área, peso, pintura)");
    private readonly TextBox _height = new() { Width = 90 };
    private readonly Label _errors = new() { ForeColor = Brand.ErrorText, AutoSize = true, Margin = new Padding(3, 8, 3, 3) };
    private readonly double _suggested;

    public MaterialListDialog(MaterialListSettings settings, double suggestedHeight, string version = "")
    {
        Settings = settings;
        _suggested = suggestedHeight;
        Text = "PASB — Lista de material" + (version.Length > 0 ? "  (" + version + ")" : "");
        BackColor = Back; ForeColor = Text_; Font = Brand.Sans(9f);
        FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(600, 470);

        var col = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, BackColor = Panel_,
            Padding = new Padding(14), Margin = new Padding(0),
        };
        col.Controls.Add(Header("TABELAS A INSERIR NO DESENHO"));
        foreach (CheckBox c in new[] { _detailed, _summary, _bolts, _anchors, _plates }) col.Controls.Add(c);
        var row = new FlowLayoutPanel { AutoSize = true, BackColor = Panel_, Margin = new Padding(0, 6, 0, 0) };
        var all = Btn("Todas"); var none = Btn("Nenhuma");
        all.Click += (_, _) => SetAll(true); none.Click += (_, _) => SetAll(false);
        row.Controls.Add(all); row.Controls.Add(none); col.Controls.Add(row);

        col.Controls.Add(Header("TEXTO"));
        var hl = new FlowLayoutPanel { AutoSize = true, BackColor = Panel_, Margin = new Padding(0) };
        _height.BackColor = Field; _height.ForeColor = Text_;
        hl.Controls.Add(new Label { Text = "Altura do texto (unidades do desenho):", AutoSize = true, ForeColor = Text_, Margin = new Padding(3, 6, 3, 3) });
        hl.Controls.Add(_height);
        col.Controls.Add(hl);
        col.Controls.Add(new Label
        {
            Text = $"Sugestão: {ExportSettings.FormatNumber(suggestedHeight, 2)} (2,5 mm no papel × escala de anotação). As tabelas ficam lado a lado a partir do ponto clicado.",
            AutoSize = false, Width = 520, Height = 34, ForeColor = Muted, Margin = new Padding(3, 4, 3, 3),
        });
        col.Controls.Add(_errors);

        var buttons = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Bottom, Height = 46, BackColor = Back, Padding = new Padding(8) };
        var ok = Brand.Btn("Inserir no desenho", primary: true); ok.Width = 160;
        var cancel = Btn("Cancelar"); cancel.Width = 90;
        buttons.Controls.Add(ok); buttons.Controls.Add(cancel);
        Controls.Add(Brand.Header("Lista de material", version)); Controls.Add(buttons); Controls.Add(col);
        col.BringToFront();
        AcceptButton = ok; CancelButton = cancel;
        ok.Click += (_, _) => { if (Collect()) { DialogResult = DialogResult.OK; Close(); } };
        cancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };

        _detailed.Checked = settings.Detailed; _summary.Checked = settings.ProfileSummary; _bolts.Checked = settings.Bolts;
        _anchors.Checked = settings.Anchors; _plates.Checked = settings.Plates;
        _height.Text = ExportSettings.FormatNumber(settings.TextHeight > 0 ? settings.TextHeight : suggestedHeight, 2);
    }

    /// <summary>Altura do texto escolhida (a sugestão se o campo ficar igual a ela).</summary>
    public double ChosenTextHeight { get; private set; }

    public bool Collect()
    {
        var errors = new List<string>();
        Settings.Detailed = _detailed.Checked; Settings.ProfileSummary = _summary.Checked; Settings.Bolts = _bolts.Checked;
        Settings.Anchors = _anchors.Checked; Settings.Plates = _plates.Checked;
        if (!Settings.AnySelected) errors.Add("Marque ao menos uma tabela.");
        if (ExportSettings.TryParseNumber(_height.Text, out double h) && h > 0)
        {
            ChosenTextHeight = h;
            // Guarda só se o usuário mudou a sugestão; senão continua acompanhando a escala de anotação.
            Settings.TextHeight = Math.Abs(h - _suggested) < 1e-9 ? 0 : h;
        }
        else errors.Add("Altura do texto inválida.");
        LastErrors = errors;
        _errors.Text = string.Join("   •   ", errors);
        return errors.Count == 0;
    }

    private void SetAll(bool v) { foreach (CheckBox c in new[] { _detailed, _summary, _bolts, _anchors, _plates }) c.Checked = v; }

    private static CheckBox Check(string t) => new() { Text = t, AutoSize = true, ForeColor = Text_, Margin = new Padding(3, 5, 3, 5) };
    private static Label Header(string t) => Brand.SectionLabel(t);
    private static Button Btn(string t) => Brand.Btn(t);
}
