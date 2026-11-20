using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Pasb.Georeferencing;
using Pasb.IfcExport;

namespace Pasb.Ui;

/// <summary>
/// Janela de exportação IFC (estilo semelhante ao SteelX).
/// Resultados: OK = exportar; Retry = o usuário pediu para selecionar o ponto base no modelo
/// (o comando esconde a janela, pede o ponto e abre a janela de novo com <see cref="Settings"/>); Cancel = cancelar.
/// </summary>
public sealed class ExportDialog : Form
{
    private static readonly Color Back = Color.FromArgb(37, 37, 38), Panel = Color.FromArgb(45, 45, 48),
        Field = Color.FromArgb(30, 30, 30), Text_ = Color.FromArgb(230, 230, 230), Muted = Color.FromArgb(160, 160, 160),
        Accent = Color.FromArgb(0, 122, 204);

    public ExportSettings Settings { get; }
    public bool PickRequested { get; private set; }
    public List<string> LastErrors { get; private set; } = new();

    private readonly RadioButton _ifc4 = Radio("IFC4"), _ifc2x3 = Radio("IFC2x3 (em breve)");
    private readonly RadioButton _scopeModel = Radio("Modelo inteiro"), _scopeSel = Radio("Seleção");
    private readonly TextBox _project = Box(), _site = Box(), _building = Box();
    private readonly Dictionary<string, CheckBox> _groups = new();
    private readonly RadioButton _geoNone = Radio("Sem georreferência"), _geoSirgas = Radio("SIRGAS 2000 / UTM"), _geoLocal = Radio("Coordenadas X, Y, Z da obra");
    private readonly NumericUpDown _zone = new() { Minimum = 1, Maximum = 60, Width = 60 };
    private readonly ComboBox _hemi = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 80 };
    private readonly TextBox _crsName = Box();
    private readonly TextBox _base = Box(readOnly: true);
    private readonly Button _pick = Btn("Selecionar...");
    private readonly TextBox _tx = Box(), _ty = Box(), _tz = Box(), _angle = Box();
    private readonly RadioButton _rot = Radio("Rotação (anti-horária, X do modelo → Leste)"), _azi = Radio("Azimute (horário, Norte → +Y do modelo)");
    private readonly TextBox _output = Box();
    private readonly Button _browse = Btn("...");
    private readonly Label _errors = new() { ForeColor = Color.FromArgb(255, 140, 120), AutoSize = false, Dock = DockStyle.Fill };
    private readonly Button _export = Btn("Exportar IFC"), _cancel = Btn("Cancelar");

    private static readonly (string key, string label)[] GroupLabels =
    {
        (ObjectGroups.Beams, "Vigas e pilares"), (ObjectGroups.Plates, "Chapas"), (ObjectGroups.Bolts, "Parafusos"),
        (ObjectGroups.Anchors, "Chumbadores"), (ObjectGroups.Welds, "Soldas"), (ObjectGroups.SpecialParts, "Peças especiais"),
        (ObjectGroups.Joints, "Ligações (grupos IFC)"), (ObjectGroups.Others, "Outros"),
    };

    public ExportDialog(ExportSettings settings, string version = "")
    {
        Settings = settings;
        Text = "PASB — Exportar para IFC" + (version.Length > 0 ? "  (" + version + ")" : "");
        BackColor = Back; ForeColor = Text_; Font = new Font("Segoe UI", 9f);
        FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(1060, 600);
        AcceptButton = _export; CancelButton = _cancel;

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 2, Padding = new Padding(10), BackColor = Back };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 260));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 260));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 110));
        Controls.Add(root);

        // Coluna 1: formato, escopo, projeto
        var c1 = Column();
        // Cada grupo de opções fica no próprio painel: botões de opção no mesmo contêiner se excluem mutuamente.
        c1.Controls.Add(Header("FORMATO")); c1.Controls.Add(Group(_ifc4, _ifc2x3));
        _ifc2x3.Enabled = false;
        c1.Controls.Add(Header("ESCOPO")); c1.Controls.Add(Group(_scopeModel, _scopeSel));
        c1.Controls.Add(Header("DADOS DO PROJETO"));
        c1.Controls.Add(Caption("Nome do projeto")); c1.Controls.Add(_project);
        c1.Controls.Add(Caption("Site")); c1.Controls.Add(_site);
        c1.Controls.Add(Caption("Edifício")); c1.Controls.Add(_building);
        root.Controls.Add(c1, 0, 0);

        // Coluna 2: objetos
        var c2 = Column();
        c2.Controls.Add(Header("OBJETOS PARA EXPORTAÇÃO"));
        foreach (var (key, label) in GroupLabels)
        {
            var cb = new CheckBox { Text = label, AutoSize = true, ForeColor = Text_, Margin = new Padding(3, 4, 3, 4) };
            _groups[key] = cb; c2.Controls.Add(cb);
        }
        var all = Btn("Todos"); var none = Btn("Nenhum");
        all.Click += (_, _) => { foreach (var cb in _groups.Values) cb.Checked = true; };
        none.Click += (_, _) => { foreach (var cb in _groups.Values) cb.Checked = false; };
        var row = new FlowLayoutPanel { AutoSize = true, BackColor = Panel, Margin = new Padding(0, 8, 0, 0) };
        row.Controls.Add(all); row.Controls.Add(none); c2.Controls.Add(row);
        c2.Controls.Add(Note("Todas as propriedades lidas do Advance Steel vão para Psets AS_<Classe>, sem filtro."));
        root.Controls.Add(c2, 1, 0);

        // Coluna 3: georreferência
        var c3 = Column();
        c3.Controls.Add(Header("GEORREFERÊNCIA"));
        c3.Controls.Add(Group(_geoNone, _geoSirgas, _geoLocal));
        _hemi.Items.AddRange(new object[] { "Sul", "Norte" });
        StyleField(_zone); StyleField(_hemi);
        c3.Controls.Add(Line(Caption("Zona UTM"), _zone, Caption("Hemisfério"), _hemi));
        c3.Controls.Add(Caption("Nome do sistema da obra")); c3.Controls.Add(_crsName);
        c3.Controls.Add(Caption("Ponto base no modelo (mm, coordenadas globais)"));
        _base.Width = 290;
        c3.Controls.Add(Line(_base, _pick));
        c3.Controls.Add(Caption("Coordenadas reais do ponto base (metros)"));
        _tx.Width = _ty.Width = _tz.Width = 105;
        c3.Controls.Add(Line(Caption("X / E"), _tx, Caption("Y / N"), _ty, Caption("Z"), _tz));
        c3.Controls.Add(Caption("Ângulo (graus)"));
        c3.Controls.Add(Group(_rot, _azi));
        _angle.Width = 120; c3.Controls.Add(_angle);
        root.Controls.Add(c3, 2, 0);

        // Rodapé: arquivo, erros, botões
        var foot = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 2, BackColor = Back };
        foot.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        foot.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        foot.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 40));
        foot.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 230));
        foot.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        foot.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var outLbl = Caption("Arquivo de saída"); outLbl.Anchor = AnchorStyles.Left;
        _output.Dock = DockStyle.Fill; _browse.Width = 34;
        foot.Controls.Add(outLbl, 0, 0); foot.Controls.Add(_output, 1, 0); foot.Controls.Add(_browse, 2, 0);
        var buttons = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Fill, BackColor = Back };
        _export.BackColor = Accent; _export.Width = 120; _cancel.Width = 90;
        buttons.Controls.Add(_export); buttons.Controls.Add(_cancel);
        foot.Controls.Add(buttons, 3, 0);
        foot.Controls.Add(_errors, 0, 1); foot.SetColumnSpan(_errors, 4);
        root.Controls.Add(foot, 0, 1); root.SetColumnSpan(foot, 3);

        // Eventos
        foreach (var rb in new[] { _geoNone, _geoSirgas, _geoLocal }) rb.CheckedChanged += (_, _) => UpdateEnabled();
        _pick.Click += (_, _) => { if (Collect(validate: false)) { PickRequested = true; DialogResult = DialogResult.Retry; Close(); } };
        _browse.Click += (_, _) => Browse();
        _export.Click += (_, _) => { if (Collect(validate: true)) { DialogResult = DialogResult.OK; Close(); } };
        _cancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };

        Load_(settings);
        UpdateEnabled();
    }

    // ------------------------------------------------------------------ dados ↔ controles

    private void Load_(ExportSettings s)
    {
        _ifc4.Checked = true;
        (s.Scope == ExportScope.Selection ? _scopeSel : _scopeModel).Checked = true;
        _project.Text = s.ProjectName; _site.Text = s.SiteName; _building.Text = s.BuildingName;
        foreach (var (key, _) in GroupLabels) _groups[key].Checked = s.Groups.GetValueOrDefault(key, true);
        (s.Geo switch { GeoChoice.Sirgas => _geoSirgas, GeoChoice.Local => _geoLocal, _ => _geoNone }).Checked = true;
        _zone.Value = Math.Clamp(s.UtmZone, 1, 60);
        _hemi.SelectedIndex = s.Hemisphere == Hemisphere.North ? 1 : 0;
        _crsName.Text = s.LocalCrsName;
        _base.Text = $"X: {ExportSettings.FormatNumber(s.BaseX, 3)}   Y: {ExportSettings.FormatNumber(s.BaseY, 3)}   Z: {ExportSettings.FormatNumber(s.BaseZ, 3)}";
        _tx.Text = ExportSettings.FormatNumber(s.TargetX); _ty.Text = ExportSettings.FormatNumber(s.TargetY); _tz.Text = ExportSettings.FormatNumber(s.TargetZ);
        (s.AngleMode == AngleInput.Azimuth ? _azi : _rot).Checked = true;
        _angle.Text = ExportSettings.FormatNumber(s.AngleDegrees, 8);
        _output.Text = s.OutputPath;
    }

    /// <summary>Copia os controles para <see cref="Settings"/>. Com validate, mostra erros e retorna false se houver.</summary>
    public bool Collect(bool validate)
    {
        var errors = new List<string>();
        ExportSettings s = Settings;
        s.Format = "IFC4";
        s.Scope = _scopeSel.Checked ? ExportScope.Selection : ExportScope.Model;
        s.ProjectName = _project.Text.Trim(); s.SiteName = _site.Text.Trim(); s.BuildingName = _building.Text.Trim();
        foreach (var (key, _) in GroupLabels) s.Groups[key] = _groups[key].Checked;
        s.Geo = _geoSirgas.Checked ? GeoChoice.Sirgas : _geoLocal.Checked ? GeoChoice.Local : GeoChoice.None;
        s.UtmZone = (int)_zone.Value;
        s.Hemisphere = _hemi.SelectedIndex == 1 ? Hemisphere.North : Hemisphere.South;
        s.LocalCrsName = _crsName.Text.Trim();
        Num(_tx, "X / E", v => s.TargetX = v, errors);
        Num(_ty, "Y / N", v => s.TargetY = v, errors);
        Num(_tz, "Z / Cota", v => s.TargetZ = v, errors);
        Num(_angle, "Ângulo", v => s.AngleDegrees = v, errors);
        s.AngleMode = _azi.Checked ? AngleInput.Azimuth : AngleInput.Rotation;
        s.OutputPath = _output.Text.Trim();

        if (validate) errors.AddRange(s.Validate());
        LastErrors = errors;
        _errors.Text = string.Join("   •   ", errors);
        return errors.Count == 0 || !validate && errors.All(e => !e.StartsWith("Valor", StringComparison.Ordinal));
    }

    private static void Num(TextBox box, string name, Action<double> set, List<string> errors)
    {
        if (ExportSettings.TryParseNumber(box.Text, out double v)) set(v);
        else errors.Add($"Valor inválido em {name}.");
    }

    private void UpdateEnabled()
    {
        bool geo = !_geoNone.Checked;
        _zone.Enabled = _hemi.Enabled = _geoSirgas.Checked;
        _crsName.Enabled = _geoLocal.Checked;
        foreach (Control c in new Control[] { _pick, _tx, _ty, _tz, _angle, _rot, _azi }) c.Enabled = geo;
    }

    private void Browse()
    {
        using var dlg = new SaveFileDialog
        {
            Filter = "IFC (*.ifc)|*.ifc", DefaultExt = "ifc", AddExtension = true, OverwritePrompt = true,
            FileName = string.IsNullOrWhiteSpace(_output.Text) ? "modelo.ifc" : Path.GetFileName(_output.Text),
            InitialDirectory = SafeDir(_output.Text),
        };
        if (dlg.ShowDialog(this) == DialogResult.OK) _output.Text = dlg.FileName;
    }

    private static string SafeDir(string path)
    {
        try { string? d = Path.GetDirectoryName(path); if (!string.IsNullOrEmpty(d) && Directory.Exists(d)) return d; } catch (Exception) { }
        return Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
    }

    // ------------------------------------------------------------------ estilo

    private static FlowLayoutPanel Column() => new()
    {
        Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true,
        BackColor = Panel, Padding = new Padding(10), Margin = new Padding(4),
    };

    private static Label Header(string t) => new() { Text = t, AutoSize = true, ForeColor = Muted, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold), Margin = new Padding(3, 12, 3, 4) };
    private static Label Caption(string t) => new() { Text = t, AutoSize = true, ForeColor = Text_, Margin = new Padding(3, 6, 3, 2) };
    private static Label Note(string t) => new() { Text = t, AutoSize = false, Width = 220, Height = 48, ForeColor = Muted, Margin = new Padding(3, 10, 3, 3) };
    private static RadioButton Radio(string t) => new() { Text = t, AutoSize = true, ForeColor = Text_, Margin = new Padding(3, 3, 3, 3) };

    private static TextBox Box(bool readOnly = false)
    {
        var b = new TextBox { Width = 220, ReadOnly = readOnly };
        StyleField(b);
        return b;
    }

    private static void StyleField(Control c) { c.BackColor = Field; c.ForeColor = Text_; }

    private static Button Btn(string t) => new()
    {
        Text = t, AutoSize = true, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(62, 62, 66), ForeColor = Text_,
        Margin = new Padding(3), Padding = new Padding(6, 2, 6, 2),
    };

    private static FlowLayoutPanel Group(params RadioButton[] radios)
    {
        var p = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, BackColor = Panel, Margin = new Padding(0) };
        foreach (RadioButton r in radios) p.Controls.Add(r);
        return p;
    }

    private static FlowLayoutPanel Line(params Control[] controls)
    {
        var p = new FlowLayoutPanel { AutoSize = true, BackColor = Panel, WrapContents = false, Margin = new Padding(0) };
        foreach (Control c in controls) p.Controls.Add(c);
        return p;
    }
}
