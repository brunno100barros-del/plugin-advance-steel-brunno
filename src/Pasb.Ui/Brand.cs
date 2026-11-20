using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Linq;
using System.Windows.Forms;

namespace Pasb.Ui;

/// <summary>
/// Identidade visual Coordix · BIMOFFICE (paleta, tipografia IBM Plex e símbolo do olho).
/// Fontes: IBM Plex Sans / Plex Mono quando instaladas; senão Segoe UI / Consolas.
/// </summary>
public static class Brand
{
    // Paleta (tema escuro)
    public static readonly Color Ink = Hex("#12151a");          // brand / painéis
    public static readonly Color BgDark = Hex("#0b0d10");       // fundo
    public static readonly Color BgLight = Hex("#e8eaed");      // texto principal no escuro
    public static readonly Color Copper = Hex("#a84d1c");       // accent (tema claro)
    public static readonly Color CopperDark = Hex("#de7d43");   // accent no tema escuro
    public static readonly Color Iris = Hex("#9a5a32");
    public static readonly Color IrisRing = Hex("#4a2a18");
    public static readonly Color Pupil = Hex("#140c08");
    public static readonly Color Muted = Hex("#8a93a3");        // rótulos técnicos
    public static readonly Color Line = Hex("#252a33");         // fios finos
    public static readonly Color Field = Hex("#0b0d10");
    public static readonly Color Button = Hex("#1c2028");
    // Severidade
    public static readonly Color P1 = Hex("#c22d26");
    public static readonly Color Ok = Hex("#287a4b");
    public static readonly Color Info = Hex("#2d65a3");
    public static readonly Color ErrorText = Hex("#ff8a80");

    public static readonly string SansFamily = Pick("IBM Plex Sans", "Segoe UI");
    public static readonly string MonoFamily = Pick("IBM Plex Mono", "Consolas");

    public static Font Sans(float size, FontStyle style = FontStyle.Regular) => new(SansFamily, size, style);
    public static Font Mono(float size, FontStyle style = FontStyle.Regular) => new(MonoFamily, size, style);

    /// <summary>Rótulo técnico: Plex Mono, maiúsculas espaçadas (ex.: "GEORREFERÊNCIA").</summary>
    public static string Spaced(string s) => string.Join(" ", s.ToUpperInvariant().ToCharArray()).Replace("   ", "  ");

    public static Color Hex(string h) => ColorTranslator.FromHtml(h);

    private static string Pick(string wanted, string fallback)
    {
        try
        {
            using var fonts = new InstalledFontCollection();
            return fonts.Families.Any(f => string.Equals(f.Name, wanted, StringComparison.OrdinalIgnoreCase)) ? wanted : fallback;
        }
        catch (Exception) { return fallback; }
    }

    /// <summary>
    /// Desenha o símbolo Coordix (olho: contorno amendoado, cílios, íris cobre, pupila e o reflexo da janela)
    /// dentro de <paramref name="r"/>. <paramref name="stroke"/> é a cor do traço (claro no tema escuro).
    /// </summary>
    public static void DrawEye(Graphics g, RectangleF r, Color stroke, bool lashes = true)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        float w = r.Width, h = r.Height, x0 = r.X, y0 = r.Y;
        PointF P(float px, float py) => new(x0 + px * w, y0 + py * h);
        float lw = Math.Max(1.2f, w * 0.022f);

        // Íris, anel e pupila
        var c = P(0.43f, 0.60f);
        float ri = h * 0.30f;
        using (var ring = new SolidBrush(IrisRing)) g.FillEllipse(ring, c.X - ri * 1.06f, c.Y - ri * 1.06f, ri * 2.12f, ri * 2.12f);
        using (var iris = new SolidBrush(Iris)) g.FillEllipse(iris, c.X - ri, c.Y - ri, ri * 2, ri * 2);
        float rp = ri * 0.40f;
        using (var pupil = new SolidBrush(Pupil)) g.FillEllipse(pupil, c.X - rp, c.Y - rp + ri * 0.05f, rp * 2, rp * 2);
        // Reflexo da janela (o traço-chave)
        using (var win = new SolidBrush(Color.White))
        {
            g.FillRectangle(win, c.X - ri * 0.45f, c.Y - ri * 0.62f, ri * 0.30f, ri * 0.24f);
            g.FillRectangle(win, c.X - ri * 0.08f, c.Y - ri * 0.62f, ri * 0.55f, ri * 0.24f);
        }

        using var pen = new Pen(stroke, lw) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        // Pálpebra superior (mais grossa no canto externo) e inferior (aberta no canto interno)
        using (var upper = new GraphicsPath())
        {
            upper.AddBezier(P(0.06f, 0.70f), P(0.22f, 0.25f), P(0.62f, 0.15f), P(0.92f, 0.52f));
            g.DrawPath(pen, upper);
        }
        using (var thick = new Pen(stroke, lw * 1.8f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            g.DrawBezier(thick, P(0.62f, 0.27f), P(0.76f, 0.32f), P(0.86f, 0.42f), P(0.92f, 0.54f));
        g.DrawBezier(pen, P(0.08f, 0.80f), P(0.30f, 0.98f), P(0.62f, 0.96f), P(0.80f, 0.76f));

        if (!lashes) return;
        // Cílios: lâminas finas que afinam na ponta e varrem para fora (canto externo à direita)
        using var brush = new SolidBrush(stroke);
        const int n = 11;
        for (int i = 0; i < n; i++)
        {
            float t = 0.20f + i * (0.72f / (n - 1));
            var b = Bez(P(0.06f, 0.70f), P(0.22f, 0.25f), P(0.62f, 0.15f), P(0.92f, 0.52f), t);
            float k = (t - 0.20f) / 0.72f;                                   // 0 = canto interno, 1 = externo
            float len = h * (0.22f + 0.20f * (float)Math.Sin(k * Math.PI * 0.85));
            double ang = -Math.PI / 2 - 0.35 + k * 1.55;                     // de quase vertical a quase horizontal
            var tip = new PointF(b.X + len * (float)Math.Cos(ang), b.Y + len * (float)Math.Sin(ang));
            // curvatura para fora: controle deslocado para trás (sentido do canto interno)
            var ctrl = new PointF((b.X + tip.X) / 2 - len * 0.30f * (float)Math.Cos(ang + Math.PI / 2),
                                  (b.Y + tip.Y) / 2 - len * 0.30f * (float)Math.Sin(ang + Math.PI / 2));
            float half = Math.Max(0.7f, lw * 0.85f);
            var nx = (float)Math.Cos(ang + Math.PI / 2) * half; var ny = (float)Math.Sin(ang + Math.PI / 2) * half;
            using var path = new GraphicsPath();
            path.AddBezier(new PointF(b.X - nx, b.Y - ny), ctrl, ctrl, tip);
            path.AddBezier(tip, ctrl, ctrl, new PointF(b.X + nx, b.Y + ny));
            path.CloseFigure();
            g.FillPath(brush, path);
        }
    }

    private static PointF Bez(PointF a, PointF b, PointF c, PointF d, float t)
    {
        float u = 1 - t;
        return new PointF(u * u * u * a.X + 3 * u * u * t * b.X + 3 * u * t * t * c.X + t * t * t * d.X,
                          u * u * u * a.Y + 3 * u * u * t * b.Y + 3 * u * t * t * c.Y + t * t * t * d.Y);
    }

    /// <summary>Cabeçalho das janelas: símbolo + "COORDIX" + "BIMOFFICE · &lt;título&gt;" + fio fino inferior.</summary>
    public static Control Header(string section, string version = "")
    {
        var p = new Panel { Dock = DockStyle.Top, Height = 64, BackColor = BgDark };
        p.Paint += (_, e) =>
        {
            Graphics g = e.Graphics;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            DrawEye(g, new RectangleF(14, 8, 62, 48), BgLight);
            using (var f = Sans(15f, FontStyle.Bold)) using (var b = new SolidBrush(BgLight))
                g.DrawString(Spaced("Coordix"), f, b, 86, 9);
            using (var f = Mono(8f)) using (var b = new SolidBrush(CopperDark))
                g.DrawString(Spaced("BIMOFFICE") + "  ·  " + Spaced(section), f, b, 88, 38);
            if (version.Length > 0)
                using (var f = Mono(8f)) using (var b = new SolidBrush(Muted))
                {
                    string v = "PASB " + version;
                    SizeF s = g.MeasureString(v, f);
                    g.DrawString(v, f, b, p.Width - s.Width - 14, 24);
                }
            using var line = new Pen(Line, 1);
            g.DrawLine(line, 0, p.Height - 1, p.Width, p.Height - 1);
        };
        p.Resize += (_, _) => p.Invalidate();
        return p;
    }

    /// <summary>Título de seção: Plex Mono, maiúsculas espaçadas, cinza técnico.</summary>
    public static Label SectionLabel(string t) => new()
    {
        Text = Spaced(t), AutoSize = true, ForeColor = Muted, Font = Mono(8f), Margin = new Padding(3, 12, 3, 6),
    };

    /// <summary>Botão plano; <paramref name="primary"/> usa o cobre.</summary>
    public static Button Btn(string t, bool primary = false)
    {
        var b = new Button
        {
            Text = t, AutoSize = true, FlatStyle = FlatStyle.Flat, BackColor = primary ? Copper : Button, ForeColor = BgLight,
            Margin = new Padding(3), Padding = new Padding(8, 3, 8, 3), Font = Sans(9f, primary ? FontStyle.Bold : FontStyle.Regular),
            Cursor = Cursors.Hand,
        };
        b.FlatAppearance.BorderColor = primary ? CopperDark : Line;
        b.FlatAppearance.MouseOverBackColor = primary ? CopperDark : Hex("#262b35");
        return b;
    }

    /// <summary>Ícone do app (PNG 512×512 ou outro tamanho): símbolo sobre o cartão escuro com raio suave.</summary>
    public static Bitmap AppIcon(int size)
    {
        var bmp = new Bitmap(size, size);
        using Graphics g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Color.Transparent);
        float r = size * 0.18f;
        using (var path = Rounded(new RectangleF(size * 0.02f, size * 0.02f, size * 0.96f, size * 0.96f), r))
        using (var fill = new SolidBrush(Ink))
            g.FillPath(fill, path);
        DrawEye(g, new RectangleF(size * 0.10f, size * 0.20f, size * 0.80f, size * 0.62f), BgLight);
        return bmp;
    }

    /// <summary>
    /// Ícone de botão da faixa de opções em PNG: cartão escuro (#12151a) com fio fino e a sigla em cobre (Plex Mono),
    /// ou o símbolo do olho (<paramref name="eye"/>). <paramref name="dot"/> desenha um ponto de estado no canto.
    /// </summary>
    public static byte[] RibbonIconPng(string glyph, int size, bool eye = false, Color? dot = null)
    {
        using var bmp = new Bitmap(size, size);
        using (Graphics g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            g.Clear(Color.Transparent);
            var rc = new RectangleF(0.5f, 0.5f, size - 1.5f, size - 1.5f);
            using (var path = Rounded(rc, size * 0.16f))
            {
                using (var fill = new SolidBrush(Ink)) g.FillPath(fill, path);
                using var border = new Pen(eye ? Line : Copper, size >= 32 ? 1.2f : 1f);
                g.DrawPath(border, path);
            }
            if (eye) DrawEye(g, new RectangleF(size * 0.06f, size * 0.20f, size * 0.88f, size * 0.66f), BgLight, lashes: size >= 24);
            else
            {
                bool small = size < 24;
                string text = small && glyph.Length > 2 ? glyph.Substring(0, 1) : glyph;   // 16 px: só a inicial
                float left = small ? 0f : size * 0.20f;                                   // espaço da marca lateral
                float fs = text.Length >= 3 ? size * 0.27f : text.Length == 2 ? size * 0.36f : size * 0.55f;
                using var f = new Font(MonoFamily, fs, FontStyle.Bold, GraphicsUnit.Pixel);
                using var b = new SolidBrush(CopperDark);
                SizeF s = g.MeasureString(text, f, PointF.Empty, StringFormat.GenericTypographic);
                g.DrawString(text, f, b, left + (size - left - s.Width) / 2, (size - s.Height) / 2, StringFormat.GenericTypographic);
                if (!small)
                {
                    // marca lateral de 3 px (identidade): fio cobre à esquerda
                    using var mark = new SolidBrush(CopperDark);
                    g.FillRectangle(mark, size * 0.10f, size * 0.28f, Math.Max(2f, size * 0.07f), size * 0.44f);
                }
            }
            if (dot is Color dc)
            {
                float d = size * 0.30f;
                using var db = new SolidBrush(dc);
                using var ring = new Pen(Ink, Math.Max(1f, size * 0.05f));
                g.FillEllipse(db, size - d - 1, size - d - 1, d, d);
                g.DrawEllipse(ring, size - d - 1, size - d - 1, d, d);
            }
        }
        using var ms = new System.IO.MemoryStream();
        bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
        return ms.ToArray();
    }

    private static GraphicsPath Rounded(RectangleF rc, float r)
    {
        var p = new GraphicsPath();
        float d = r * 2;
        p.AddArc(rc.X, rc.Y, d, d, 180, 90);
        p.AddArc(rc.Right - d, rc.Y, d, d, 270, 90);
        p.AddArc(rc.Right - d, rc.Bottom - d, d, d, 0, 90);
        p.AddArc(rc.X, rc.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }
}
