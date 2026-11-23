using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Pasb.Ui;

/// <summary>Barra de progresso Coordix: trilho escuro com preenchimento cobre. Valor de 0 a 100; -1 = indeterminada (faixa que corre).</summary>
public sealed class CoordixProgressBar : Control
{
    private int _value;
    private int _marquee;
    private readonly Timer _anim = new() { Interval = 40 };

    public CoordixProgressBar()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        Height = 10;
        _anim.Tick += (_, _) => { _marquee = (_marquee + 6) % Math.Max(1, Width + 80); Invalidate(); };
    }

    /// <summary>0–100; -1 para "trabalhando, sem porcentagem".</summary>
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public int Value
    {
        get => _value;
        set
        {
            int v = Math.Clamp(value, -1, 100);
            if (v == _value) return;
            _value = v;
            _anim.Enabled = v < 0 && Visible;
            Invalidate();
        }
    }

    protected override void OnVisibleChanged(EventArgs e) { base.OnVisibleChanged(e); _anim.Enabled = _value < 0 && Visible; }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Parent?.BackColor ?? Brand.Ink);
        var track = new Rectangle(0, 0, Width - 1, Height - 1);
        using (var b = new SolidBrush(Brand.BgDark)) g.FillRectangle(b, track);
        using (var p = new Pen(Brand.Line)) g.DrawRectangle(p, track);
        using var fill = new SolidBrush(Brand.CopperDark);
        if (_value < 0)
        {
            int w = 80, x = _marquee - w;
            g.FillRectangle(fill, Math.Max(1, x), 1, Math.Min(w, Width - 2 - Math.Max(1, x)), Height - 2);
        }
        else if (_value > 0)
        {
            g.FillRectangle(fill, 1, 1, (Width - 2) * _value / 100, Height - 2);
        }
    }

    protected override void Dispose(bool disposing) { if (disposing) _anim.Dispose(); base.Dispose(disposing); }
}

/// <summary>
/// Janela de progresso da importação: etapa, barra, porcentagem e tempo decorrido (o relógio anda mesmo quando a
/// porcentagem para — assim dá para ver que não travou) e botão Cancelar.
/// </summary>
public sealed class ImportProgressForm : Form
{
    private readonly Label _file = new() { AutoSize = false, Width = 520, Height = 20, ForeColor = Brand.Muted, Font = Brand.Mono(8.5f) };
    private readonly Label _stage = new() { AutoSize = false, Width = 520, Height = 22, ForeColor = Brand.BgLight, Font = Brand.Sans(10f) };
    private readonly CoordixProgressBar _bar = new() { Width = 520, Height = 12, Margin = new Padding(3, 6, 3, 6) };
    private readonly Label _pct = new() { AutoSize = false, Width = 520, Height = 20, ForeColor = Brand.CopperDark, Font = Brand.Mono(9f) };
    private readonly Button _cancel = Brand.Btn("Cancelar");
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly Timer _tick = new() { Interval = 500 };
    private int _value = -1;

    /// <summary>true depois que o usuário clicou em Cancelar.</summary>
    public bool CancelRequested { get; private set; }

    public ImportProgressForm(string fileName, string version = "")
    {
        Text = "PASB — Importando IFC";
        BackColor = Brand.BgDark; ForeColor = Brand.BgLight; Font = Brand.Sans(9f);
        FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false; ControlBox = false;
        StartPosition = FormStartPosition.CenterScreen; ShowInTaskbar = false; TopMost = true;
        ClientSize = new Size(580, 250);
        var body = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, BackColor = Brand.Ink, Padding = new Padding(26, 14, 14, 10) };
        _file.Text = fileName;
        body.Controls.Add(_file); body.Controls.Add(_stage); body.Controls.Add(_bar); body.Controls.Add(_pct);
        _cancel.Margin = new Padding(3, 8, 3, 3);
        body.Controls.Add(_cancel);
        Controls.Add(Brand.Header("Importando IFC", version));
        Controls.Add(body);
        body.BringToFront();
        _cancel.Click += (_, _) =>
        {
            CancelRequested = true; _cancel.Enabled = false; _cancel.Text = "Cancelando...";
            _stage.Text = "Cancelando: nada será desenhado.";
        };
        _tick.Tick += (_, _) => UpdateText();
        _tick.Start();
        Report(-1, "Iniciando");
    }

    /// <summary>Atualiza etapa e porcentagem (chamar na thread da janela).</summary>
    public void Report(int percent, string stage)
    {
        _value = percent;
        _bar.Value = percent;
        if (!CancelRequested && !string.IsNullOrEmpty(stage)) _stage.Text = stage;
        UpdateText();
    }

    private void UpdateText()
    {
        TimeSpan t = _clock.Elapsed;
        string time = t.TotalHours >= 1 ? t.ToString(@"h\:mm\:ss") : t.ToString(@"mm\:ss");
        _pct.Text = (_value >= 0 ? $"{_value}%" : "trabalhando...") + "   ·   " + time + " decorridos";
    }

    protected override void Dispose(bool disposing) { if (disposing) _tick.Dispose(); base.Dispose(disposing); }
}
