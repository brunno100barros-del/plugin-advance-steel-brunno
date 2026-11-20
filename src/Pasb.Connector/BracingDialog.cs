using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Pasb.Ui;

namespace Pasb.Connector;

/// <summary>Última configuração do contraventamento (%APPDATA%\PASB\bracing-settings.json).</summary>
public static class BracingSettings
{
    public static string DefaultPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PASB", "bracing-settings.json");

    public static BracingParams Load(string? path = null)
    {
        try { string p = path ?? DefaultPath; return File.Exists(p) ? BracingParams.FromJson(File.ReadAllText(p)) : new BracingParams(); }
        catch (Exception) { return new BracingParams(); }
    }

    public static void Save(BracingParams b, string? path = null)
    {
        try
        {
            string p = path ?? DefaultPath;
            Directory.CreateDirectory(Path.GetDirectoryName(p)!);
            File.WriteAllText(p, b.ToJson(), new UTF8Encoding(false));
        }
        catch (Exception) { }
    }
}

/// <summary>
/// Janela "Contraventamento em X" (identidade Coordix): todos os parâmetros editáveis, agrupados por parte,
/// com prévia em planta e validação ao vivo. OK = gerar com <see cref="Params"/>.
/// </summary>
public sealed class BracingDialog : Form
{
    public BracingParams Params { get; }
    public BracingResult Result { get; private set; }
    private readonly V3 _p1, _p2;
    private readonly List<Action> _readers = new();
    private readonly Panel _preview = new() { Dock = DockStyle.Fill, BackColor = Brand.BgDark };
    private readonly Label _status = new() { AutoSize = false, Dock = DockStyle.Fill, ForeColor = Brand.Muted, Font = Brand.Sans(8.5f) };
    private readonly Button _ok, _cancel = Brand.Btn("Cancelar"), _defaults = Brand.Btn("Valores do exemplo");
    private bool _loading = true;

    public BracingDialog(V3 p1, V3 p2, BracingParams p, string version = "", bool editing = false)
    {
        _p1 = p1; _p2 = p2; Params = p;
        _ok = Brand.Btn(editing ? "Regerar" : "Gerar", primary: true);
        Text = "PASB — Contraventamento em X" + (version.Length > 0 ? "  (" + version + ")" : "");
        BackColor = Brand.BgDark; ForeColor = Brand.BgLight; Font = Brand.Sans(9f);
        FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(1240, 800);
        AcceptButton = _ok; CancelButton = _cancel;

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, Padding = new Padding(10), BackColor = Brand.BgDark };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 760));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
        Controls.Add(Brand.Header(editing ? "Editar contraventamento" : "Contraventamento em X", version));
        Controls.Add(root);
        root.BringToFront();

        // ---- parâmetros (duas colunas de grupos)
        var cols = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, BackColor = Brand.BgDark, Margin = new Padding(0) };
        cols.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        cols.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        var c1 = Column(); var c2 = Column();
        cols.Controls.Add(c1, 0, 0); cols.Controls.Add(c2, 1, 0);
        root.Controls.Add(cols, 0, 0);

        Section(c1, "Diagonais");
        Choice(c1, "Montagem", new[] { "X (as duas diagonais)", "Só a diagonal 1 (ponto 1 → ponto 2)", "Só a diagonal 2" }, () => (int)p.Diagonals, v => p.Diagonals = (BracingDiagonals)v);
        Section(c1, "Barra (tirante)");
        Text_(c1, "Perfil", () => p.RodProfile, v => p.RodProfile = v);
        Num(c1, "Diâmetro (mm)", () => p.RodDiameter, v => p.RodDiameter = v);
        Text_(c1, "Material", () => p.RodMaterial, v => p.RodMaterial = v);
        Section(c1, "Gusset (soldado no ponto)");
        Num(c1, "Espessura", () => p.GussetThickness, v => p.GussetThickness = v);
        Num(c1, "Comprimento", () => p.GussetLength, v => p.GussetLength = v);
        Num(c1, "Largura", () => p.GussetWidth, v => p.GussetWidth = v);
        Num(c1, "Recuo do ponto ao gusset", () => p.GussetSetback, v => p.GussetSetback = v);
        Num(c1, "Apoio da barra no gusset", () => p.RodBearingOnGusset, v => p.RodBearingOnGusset = v);
        Text_(c1, "Material das chapas", () => p.PlateMaterial, v => p.PlateMaterial = v);
        Section(c1, "Chapa de base + parafuso");
        Check(c1, "Chapa de base", () => p.BasePlate, v => p.BasePlate = v);
        Num(c1, "Espessura", () => p.BaseThickness, v => p.BaseThickness = v);
        Num(c1, "Comprimento", () => p.BaseLength, v => p.BaseLength = v);
        Num(c1, "Largura", () => p.BaseWidth, v => p.BaseWidth = v);
        Num(c1, "Centro a partir do ponto", () => p.BaseCenterFromPoint, v => p.BaseCenterFromPoint = v);
        Check(c1, "Parafuso", () => p.Bolt, v => p.Bolt = v);
        Num(c1, "Diâmetro", () => p.BoltDiameter, v => p.BoltDiameter = v);
        Num(c1, "Comprimento", () => p.BoltLength, v => p.BoltLength = v);
        Text_(c1, "Norma", () => p.BoltStandard, v => p.BoltStandard = v);
        Text_(c1, "Classe", () => p.BoltGrade, v => p.BoltGrade = v);
        Num(c1, "Distância ao início do gusset", () => p.BoltFromGussetStart, v => p.BoltFromGussetStart = v);

        Section(c2, "Esticador");
        Choice(c2, "Posição medida em", new[] { "% do comprimento da diagonal", "mm a partir do canto 1" }, () => (int)p.TurnbuckleMode, v => p.TurnbuckleMode = (TurnbucklePositionMode)v);
        Num(c2, "Posição do centro", () => p.TurnbucklePosition, v => p.TurnbucklePosition = v);
        Text_(c2, "Perfil da cantoneira", () => p.CleatProfile, v => p.CleatProfile = v);
        Num(c2, "Comprimento da cantoneira", () => p.CleatLength, v => p.CleatLength = v);
        Text_(c2, "Material", () => p.CleatMaterial, v => p.CleatMaterial = v);
        Num(c2, "Chapa de topo: espessura", () => p.EndPlateThickness, v => p.EndPlateThickness = v);
        Num(c2, "Chapa de topo: comprimento", () => p.EndPlateLength, v => p.EndPlateLength = v);
        Num(c2, "Chapa de topo: largura", () => p.EndPlateWidth, v => p.EndPlateWidth = v);
        Num(c2, "Recuo da chapa de topo", () => p.EndPlateInset, v => p.EndPlateInset = v);
        Num(c2, "Comprimento da rosca", () => p.ThreadLength, v => p.ThreadLength = v);
        Num(c2, "Rosca dentro do esticador", () => p.ThreadPenetration, v => p.ThreadPenetration = v);
        Section(c2, "Emenda barra lisa ↔ roscada");
        Check(c2, "Talas laterais", () => p.SpliceBars, v => p.SpliceBars = v);
        Num(c2, "Comprimento das talas", () => p.SpliceLength, v => p.SpliceLength = v);
        Section(c2, "Porcas e arruelas (blocos PASB)");
        Check(c2, "Criar porcas e arruelas", () => p.NutsAndWashers, v => p.NutsAndWashers = v);
        Num(c2, "Porca: entre faces", () => p.NutAcrossFlats, v => p.NutAcrossFlats = v);
        Num(c2, "Porca: altura", () => p.NutHeight, v => p.NutHeight = v);
        Num(c2, "Arruela: Ø externo", () => p.WasherOuterDiameter, v => p.WasherOuterDiameter = v);
        Num(c2, "Arruela: Ø interno", () => p.WasherInnerDiameter, v => p.WasherInnerDiameter = v);
        Num(c2, "Arruela: espessura", () => p.WasherThickness, v => p.WasherThickness = v);
        Section(c2, "Soldas (cordão, mm)");
        Num(c2, "Barra no gusset", () => p.WeldRodGusset, v => p.WeldRodGusset = v);
        Num(c2, "Emenda", () => p.WeldSplice, v => p.WeldSplice = v);
        Num(c2, "Chapa de topo na cantoneira", () => p.WeldEndPlate, v => p.WeldEndPlate = v);

        // ---- prévia em planta
        var right = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, BackColor = Brand.Ink, Padding = new Padding(10), Margin = new Padding(4) };
        right.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        right.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        right.RowStyles.Add(new RowStyle(SizeType.Absolute, 120));
        right.Controls.Add(Brand.SectionLabel("Prévia em planta (XY)"), 0, 0);
        right.Controls.Add(_preview, 0, 1);
        right.Controls.Add(_status, 0, 2);
        root.Controls.Add(right, 1, 0);
        _preview.Paint += (_, e) => DrawPreview(e.Graphics);
        _preview.Resize += (_, _) => _preview.Invalidate();

        // ---- rodapé
        var buttons = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Fill, BackColor = Brand.BgDark };
        _ok.Width = 130; _cancel.Width = 100;
        buttons.Controls.Add(_ok); buttons.Controls.Add(_cancel); buttons.Controls.Add(_defaults);
        root.Controls.Add(buttons, 0, 1); root.SetColumnSpan(buttons, 2);
        _ok.Click += (_, _) => { Recalc(); if (Result.Errors.Count == 0) { DialogResult = DialogResult.OK; Close(); } };
        _cancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
        _defaults.Click += (_, _) =>
        {
            if (MessageBox.Show(this, "Voltar todos os campos aos valores do exemplo analisado?", "PASB", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            string keep = Params.ToJson();
            var d = new BracingParams();
            foreach (var prop in typeof(BracingParams).GetProperties()) prop.SetValue(Params, prop.GetValue(d));
            _loading = true; foreach (Control c in c1.Controls.Cast<Control>().Concat(c2.Controls.Cast<Control>())) Reload(c); _loading = false;
            Recalc();
        };

        _loading = false;
        Result = new BracingResult(Array.Empty<BracingPart>(), Array.Empty<string>(), Array.Empty<(V3, V3)>());
        Recalc();
    }

    /// <summary>Lê os campos e recalcula a geometria (público para testes).</summary>
    public void Recalc()
    {
        if (_loading) return;
        foreach (Action r in _readers) r();
        Result = BracingGeometry.Build(_p1, _p2, Params);
        double l = (new V3(_p2.X, _p2.Y, 0) - new V3(_p1.X, _p1.Y, 0)).Length;
        if (Result.Errors.Count > 0)
        {
            _status.ForeColor = Brand.ErrorText;
            _status.Text = string.Join(Environment.NewLine, Result.Errors.Select(e => "• " + e));
        }
        else
        {
            _status.ForeColor = Brand.Muted;
            var counts = Result.Parts.GroupBy(x => x.Kind).ToDictionary(g => g.Key, g => g.Count());
            int n(BracingPartKind k) => counts.GetValueOrDefault(k);
            _status.Text = $"Diagonal: {l:0} mm · ângulo {Math.Atan2(_p2.Y - _p1.Y, _p2.X - _p1.X) * 180 / Math.PI:0.0}° · Z = {_p1.Z:0.###}" + Environment.NewLine +
                           $"{Result.Parts.Count} peças: {n(BracingPartKind.Rod) + n(BracingPartKind.ThreadedRod) + n(BracingPartKind.SpliceBar)} barras, {n(BracingPartKind.Cleat)} esticador(es), " +
                           $"{n(BracingPartKind.Gusset) + n(BracingPartKind.BasePlate) + n(BracingPartKind.EndPlate)} chapas, {n(BracingPartKind.Bolt)} parafuso(s), " +
                           $"{n(BracingPartKind.Nut)} porcas, {n(BracingPartKind.Washer)} arruelas, {n(BracingPartKind.Weld)} soldas.";
        }
        _ok.Enabled = Result.Errors.Count == 0;
        _preview.Invalidate();
    }

    // ------------------------------------------------------------------ prévia

    private void DrawPreview(Graphics g)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Brand.BgDark);
        double minX = Math.Min(_p1.X, _p2.X), maxX = Math.Max(_p1.X, _p2.X), minY = Math.Min(_p1.Y, _p2.Y), maxY = Math.Max(_p1.Y, _p2.Y);
        double w = Math.Max(1, maxX - minX), h = Math.Max(1, maxY - minY);
        float pad = 24;
        double k = Math.Min((_preview.Width - 2 * pad) / w, (_preview.Height - 2 * pad) / h);
        PointF P(V3 v) => new((float)(pad + (v.X - minX) * k), (float)(_preview.Height - pad - (v.Y - minY) * k));
        using (var frame = new Pen(Brand.Line, 1) { DashStyle = DashStyle.Dash })
            g.DrawRectangle(frame, P(new V3(minX, maxY, 0)).X, P(new V3(minX, maxY, 0)).Y, (float)(w * k), (float)(h * k));
        using var thin = new Pen(Brand.Muted, 1);
        foreach (var (a, b) in Result.DiagonalLines) g.DrawLine(thin, P(a), P(b));
        foreach (BracingPart part in Result.Parts)
        {
            switch (part.Kind)
            {
                case BracingPartKind.Rod: case BracingPartKind.ThreadedRod:
                    using (var pen = new Pen(part.Kind == BracingPartKind.Rod ? Brand.BgLight : Brand.CopperDark, 2.2f)) g.DrawLine(pen, P(part.Start), P(part.End));
                    break;
                case BracingPartKind.Cleat:
                    using (var pen = new Pen(Brand.Copper, 6f)) g.DrawLine(pen, P(part.Start), P(part.End));
                    break;
                case BracingPartKind.Gusset: case BracingPartKind.BasePlate:
                    using (var b = new SolidBrush(Color.FromArgb(part.Kind == BracingPartKind.Gusset ? 170 : 70, Brand.Iris)))
                        g.FillPolygon(b, part.Polygon.Select(P).ToArray());
                    break;
                case BracingPartKind.Bolt:
                    var c = P(part.Origin); g.FillEllipse(Brushes.Gold, c.X - 3, c.Y - 3, 6, 6);
                    break;
            }
        }
        using var dot = new SolidBrush(Brand.CopperDark);
        using var font = Brand.Mono(8f);
        foreach (var (pt, label) in new[] { (_p1, "1"), (_p2, "2") })
        {
            var c = P(pt); g.FillEllipse(dot, c.X - 4, c.Y - 4, 8, 8);
            g.DrawString(label, font, dot, c.X + 6, c.Y - 14);
        }
    }

    // ------------------------------------------------------------------ campos

    private static FlowLayoutPanel Column() => new()
    {
        Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true,
        BackColor = Brand.Ink, Padding = new Padding(10), Margin = new Padding(4),
    };

    private static void Section(Control parent, string title) => parent.Controls.Add(Brand.SectionLabel(title));

    private FlowLayoutPanel Row(Control parent, string label)
    {
        var row = new FlowLayoutPanel { AutoSize = true, WrapContents = false, BackColor = Brand.Ink, Margin = new Padding(0, 1, 0, 1) };
        row.Controls.Add(new Label { Text = label, AutoSize = false, Width = 165, Height = 24, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Brand.BgLight });
        parent.Controls.Add(row);
        return row;
    }

    private void Num(Control parent, string label, Func<double> get, Action<double> set)
    {
        var box = new TextBox { Width = 120, BackColor = Brand.Field, ForeColor = Brand.BgLight, Text = Fmt(get()) };
        box.Tag = (Action)(() => box.Text = Fmt(get()));
        Row(parent, label).Controls.Add(box);
        _readers.Add(() => { if (ExportSettings.TryParseNumber(box.Text, out double v)) { set(v); box.BackColor = Brand.Field; } else box.BackColor = Brand.Hex("#4a1c1c"); });
        box.TextChanged += (_, _) => Recalc();
    }

    private void Text_(Control parent, string label, Func<string> get, Action<string> set)
    {
        var box = new TextBox { Width = 175, BackColor = Brand.Field, ForeColor = Brand.BgLight, Text = get() };
        box.Tag = (Action)(() => box.Text = get());
        Row(parent, label).Controls.Add(box);
        _readers.Add(() => set(box.Text.Trim()));
        box.TextChanged += (_, _) => Recalc();
    }

    private void Check(Control parent, string label, Func<bool> get, Action<bool> set)
    {
        var cb = new CheckBox { Text = label, AutoSize = true, ForeColor = Brand.CopperDark, Checked = get(), Margin = new Padding(3, 6, 3, 2) };
        cb.Tag = (Action)(() => cb.Checked = get());
        parent.Controls.Add(cb);
        _readers.Add(() => set(cb.Checked));
        cb.CheckedChanged += (_, _) => Recalc();
    }

    private void Choice(Control parent, string label, string[] options, Func<int> get, Action<int> set)
    {
        var combo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 250, BackColor = Brand.Field, ForeColor = Brand.BgLight, FlatStyle = FlatStyle.Flat };
        combo.Items.AddRange(options);
        combo.SelectedIndex = Math.Clamp(get(), 0, options.Length - 1);
        combo.Tag = (Action)(() => combo.SelectedIndex = Math.Clamp(get(), 0, options.Length - 1));
        parent.Controls.Add(new Label { Text = label, AutoSize = true, ForeColor = Brand.Muted, Margin = new Padding(3, 4, 3, 0) });
        parent.Controls.Add(combo);
        _readers.Add(() => set(combo.SelectedIndex));
        combo.SelectedIndexChanged += (_, _) => Recalc();
    }

    private static void Reload(Control c)
    {
        if (c.Tag is Action a) a();
        foreach (Control child in c.Controls) Reload(child);
    }

    private static string Fmt(double v) => v.ToString("0.###", CultureInfo.GetCultureInfo("pt-BR"));
}
