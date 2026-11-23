using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Pasb.IfcImport.Model;

namespace Pasb.Ui;

/// <summary>Resultado da janela: o que importar e como.</summary>
public sealed class ImportChoice
{
    public string IfcFile { get; init; } = "";
    public IfcScan Scan { get; init; } = new();
    public List<string> Types { get; init; } = new();
    public List<string> StoreyKeys { get; init; } = new();
    /// <summary>Cor (ACI) de cada nível escolhido, pela chave do nível.</summary>
    public Dictionary<string, short> StoreyColors { get; init; } = new();
    /// <summary>Nome de exibição de cada nível, pela chave.</summary>
    public Dictionary<string, string> StoreyNames { get; init; } = new();
    public ImportSettings Settings { get; init; } = new();
}

/// <summary>
/// Janela "Importar IFC" (identidade Coordix). À esquerda, o conteúdo do arquivo: níveis (com cor por nível) e tipos;
/// à direita, arquivo, geometria (Solid3d ou Mesh), blocos, layers, cores e opções avançadas.
/// A leitura do arquivo é feita por <c>scanner</c> (o plugin chama o Pasb.IfcImport.exe) fora da thread da janela.
/// </summary>
public sealed class ImportDialog : Form
{
    public ImportSettings Settings { get; }
    public ImportChoice? Choice { get; private set; }
    public IfcScan? Scan { get; private set; }
    public List<string> LastErrors { get; private set; } = new();

    private readonly Func<string, Action<int, string>, CancellationToken, IfcScan> _scanner;
    private CancellationTokenSource? _scanCts;
    private readonly CoordixProgressBar _bar = new() { Width = 640, Height = 10, Visible = false, Margin = new Padding(3, 4, 3, 2) };
    private readonly Label _barText = new() { AutoSize = false, Width = 640, Height = 18, ForeColor = Brand.CopperDark, Font = Brand.Mono(8.5f), Visible = false };
    private readonly System.Windows.Forms.Timer _clockTimer = new() { Interval = 500 };
    private readonly System.Diagnostics.Stopwatch _clock = new();
    private string _stageText = "";
    private int _pct = -1;
    private readonly Func<short, short?>? _pickColor;
    private string _file = "";

    private readonly TextBox _fileBox = new() { ReadOnly = true, Width = 290 };
    private readonly Label _status = new() { AutoSize = false, Width = 500, Height = 36, ForeColor = Brand.Muted };
    private readonly DataGridView _storeys = Grid();
    private readonly DataGridView _types = Grid();
    private readonly RadioButton _solid = Radio("Importar como Solid3d (recomendado)"), _mesh = Radio("Importar como Mesh");
    private readonly CheckBox _failMesh = Check("Importar falhas como mesh (automático)");
    private readonly CheckBox _blocks = Check("Agrupar geometria repetida em blocos");
    private readonly RadioButton _layType = Radio("Separar por tipo IFC"), _layIfc = Radio("Separar por layer do IFC (se houver)"),
        _layStorey = Radio("Separar por nível"), _laySingle = Radio("Tudo na mesma layer:");
    private readonly TextBox _singleLayer = new() { Width = 120 };
    private readonly RadioButton _colOrig = Radio("Manter cores originais do IFC"), _colStorey = Radio("Cor por nível (cores da lista de níveis)"),
        _colSingle = Radio("Cor única:");
    private readonly NumericUpDown _singleColor = new() { Minimum = 1, Maximum = 255, Width = 60 };
    private readonly CheckBox _openings = Check("Incluir aberturas (IfcOpeningElement)"), _spaces = Check("Incluir espaços (IfcSpace)");
    private readonly Label _errors = new() { ForeColor = Brand.ErrorText, AutoSize = false, Dock = DockStyle.Fill };
    private readonly Button _import = Brand.Btn("Importar", primary: true), _cancel = Brand.Btn("Cancelar"), _open = Brand.Btn("Abrir...");

    public ImportDialog(ImportSettings settings, Func<string, Action<int, string>, CancellationToken, IfcScan> scanner, Func<short, short?>? pickColor = null, string version = "")
    {
        Settings = settings; _scanner = scanner; _pickColor = pickColor;
        Text = "PASB — Importar IFC" + (version.Length > 0 ? "  (" + version + ")" : "");
        BackColor = Brand.BgDark; ForeColor = Brand.BgLight; Font = Brand.Sans(9f);
        FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(1120, 860);
        AcceptButton = _import; CancelButton = _cancel;

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, Padding = new Padding(10), BackColor = Brand.BgDark };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 420));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
        Controls.Add(Brand.Header("Importar IFC", version));
        Controls.Add(root);
        root.BringToFront();

        // ---- esquerda: conteúdo do arquivo
        var left = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 8, BackColor = Brand.Ink, Padding = new Padding(12), Margin = new Padding(4) };
        left.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        left.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        left.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        left.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        left.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        left.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        left.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        left.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        left.Controls.Add(Brand.SectionLabel("Conteúdo do arquivo"), 0, 0);
        _status.Text = "Clique em \"Abrir...\" para escolher um arquivo IFC. Os níveis e os tipos que ele contém aparecem aqui.";
        left.Controls.Add(_status, 0, 1);
        left.Controls.Add(_bar, 0, 2);
        left.Controls.Add(_barText, 0, 3);
        left.Controls.Add(HeaderRow("Níveis", "Clique na cor para trocar", out var stAll, out var stNone), 0, 4);
        left.Controls.Add(_storeys, 0, 5);
        left.Controls.Add(HeaderRow("Tipos", "Desmarque o que não quiser importar", out var tyAll, out var tyNone), 0, 6);
        left.Controls.Add(_types, 0, 7);
        _clockTimer.Tick += (_, _) => UpdateBarText();
        root.Controls.Add(left, 0, 0);
        stAll.Click += (_, _) => CheckAll(_storeys, true); stNone.Click += (_, _) => CheckAll(_storeys, false);
        tyAll.Click += (_, _) => CheckAll(_types, true); tyNone.Click += (_, _) => CheckAll(_types, false);

        _storeys.Columns.Add(new DataGridViewCheckBoxColumn { Name = "on", HeaderText = "", Width = 32 });
        _storeys.Columns.Add(new DataGridViewTextBoxColumn { Name = "color", HeaderText = "Cor", Width = 52, ReadOnly = true });
        _storeys.Columns.Add(new DataGridViewTextBoxColumn { Name = "name", HeaderText = "Nível", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, ReadOnly = true });
        _storeys.Columns.Add(new DataGridViewTextBoxColumn { Name = "elev", HeaderText = "Cota (m)", Width = 90, ReadOnly = true, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleRight } });
        _storeys.Columns.Add(new DataGridViewTextBoxColumn { Name = "count", HeaderText = "Elementos", Width = 90, ReadOnly = true, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleRight } });
        _storeys.CellClick += (_, e) => { if (e.RowIndex >= 0 && e.ColumnIndex == 1) PickStoreyColor(e.RowIndex); };

        _types.Columns.Add(new DataGridViewCheckBoxColumn { Name = "on", HeaderText = "", Width = 32 });
        _types.Columns.Add(new DataGridViewTextBoxColumn { Name = "label", HeaderText = "Tipo", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, ReadOnly = true });
        _types.Columns.Add(new DataGridViewTextBoxColumn { Name = "ifc", HeaderText = "Classe IFC", Width = 190, ReadOnly = true });
        _types.Columns.Add(new DataGridViewTextBoxColumn { Name = "count", HeaderText = "Qtde", Width = 70, ReadOnly = true, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleRight } });

        // ---- direita: opções
        var right = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, BackColor = Brand.Ink, Padding = new Padding(12), Margin = new Padding(4) };
        right.Controls.Add(Brand.SectionLabel("Arquivo"));
        right.Controls.Add(Line(_fileBox, _open));
        right.Controls.Add(Brand.SectionLabel("Geometria"));
        right.Controls.Add(Group(_solid, _mesh));
        right.Controls.Add(Note("Sólido plota e cota no Advance Steel (mais lento e mais pesado). Mesh importa muito mais rápido, mas não plota: serve de referência visual para a modelagem."));
        _failMesh.Margin = new Padding(22, 2, 3, 2); right.Controls.Add(_failMesh);
        right.Controls.Add(_blocks);
        right.Controls.Add(Note("Peças repetidas (esquadrias, montantes, parafusos) viram um bloco só: o DWG fica menor e mais leve. Antes de detalhar no Advance Steel, explodir os blocos (EXPLODIR)."));
        right.Controls.Add(Brand.SectionLabel("Layers"));
        right.Controls.Add(Group(_layType, _layIfc, _layStorey));
        right.Controls.Add(Line(_laySingle, _singleLayer));
        right.Controls.Add(Brand.SectionLabel("Cores"));
        right.Controls.Add(Group(_colOrig, _colStorey));
        right.Controls.Add(Line(_colSingle, _singleColor, new Label { Text = "ACI", AutoSize = true, ForeColor = Brand.Muted, Margin = new Padding(3, 7, 3, 3) }));
        right.Controls.Add(Brand.SectionLabel("Avançado"));
        right.Controls.Add(_openings); right.Controls.Add(_spaces);
        root.Controls.Add(right, 1, 0);
        // Grupos de rádio independentes: cada grupo precisa do próprio contêiner (laySingle e colSingle ficam fora dos painéis).
        _laySingle.CheckedChanged += (_, _) => { if (_laySingle.Checked) { _layType.Checked = _layIfc.Checked = _layStorey.Checked = false; } UpdateEnabled(); };
        foreach (var rb in new[] { _layType, _layIfc, _layStorey }) rb.CheckedChanged += (_, _) => { if (rb.Checked) _laySingle.Checked = false; UpdateEnabled(); };
        _colSingle.CheckedChanged += (_, _) => { if (_colSingle.Checked) { _colOrig.Checked = _colStorey.Checked = false; } UpdateEnabled(); };
        foreach (var rb in new[] { _colOrig, _colStorey }) rb.CheckedChanged += (_, _) => { if (rb.Checked) _colSingle.Checked = false; UpdateEnabled(); };
        _solid.CheckedChanged += (_, _) => UpdateEnabled();

        // ---- rodapé
        var foot = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, BackColor = Brand.BgDark };
        foot.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        foot.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 260));
        var buttons = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Fill, BackColor = Brand.BgDark };
        _import.Width = 130; _cancel.Width = 100;
        buttons.Controls.Add(_import); buttons.Controls.Add(_cancel);
        foot.Controls.Add(_errors, 0, 0); foot.Controls.Add(buttons, 1, 0);
        root.Controls.Add(foot, 0, 1); root.SetColumnSpan(foot, 2);

        _open.Click += (_, _) => Browse();
        _import.Click += (_, _) => { if (Collect()) { DialogResult = DialogResult.OK; Close(); } };
        _cancel.Click += (_, _) => { _scanCts?.Cancel(); DialogResult = DialogResult.Cancel; Close(); };
        FormClosed += (_, _) => { _scanCts?.Cancel(); _clockTimer.Stop(); };

        LoadSettings();
    }

    // ------------------------------------------------------------------ arquivo

    private void Browse()
    {
        using var dlg = new OpenFileDialog
        {
            Filter = "IFC (*.ifc)|*.ifc|Todos os arquivos (*.*)|*.*", Title = "Escolher arquivo IFC",
            InitialDirectory = Directory.Exists(Settings.LastFolder) ? Settings.LastFolder : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        Settings.LastFolder = Path.GetDirectoryName(dlg.FileName) ?? "";
        StartScan(dlg.FileName);
    }

    /// <summary>Lê o arquivo em segundo plano e preenche as listas.</summary>
    public void StartScan(string file)
    {
        _file = file; _fileBox.Text = Path.GetFileName(file);
        _status.Text = "Lendo " + Path.GetFileName(file) + "..."; _status.ForeColor = Brand.CopperDark;
        _storeys.Rows.Clear(); _types.Rows.Clear(); Scan = null;
        _import.Enabled = _open.Enabled = false;
        _scanCts?.Cancel();
        var cts = _scanCts = new CancellationTokenSource();
        _pct = -1; _stageText = "Iniciando o leitor de IFC";
        _bar.Value = -1; _bar.Visible = _barText.Visible = true;
        _clock.Restart(); _clockTimer.Start(); UpdateBarText();
        Action<int, string> report = (pct, stage) =>
        {
            if (IsDisposed || cts.IsCancellationRequested) return;
            try { BeginInvoke(new Action(() => { _pct = pct; _stageText = stage; _bar.Value = pct; UpdateBarText(); })); } catch (InvalidOperationException) { }
        };
        Task.Run(() => _scanner(file, report, cts.Token)).ContinueWith(t =>
        {
            if (IsDisposed || cts.IsCancellationRequested) return;
            BeginInvoke(new Action(() =>
            {
                _clockTimer.Stop(); _bar.Visible = _barText.Visible = false;
                _open.Enabled = true; _import.Enabled = true;
                if (t.Exception != null) { _status.Text = "Não foi possível ler o arquivo: " + t.Exception.GetBaseException().Message; _status.ForeColor = Brand.ErrorText; return; }
                LoadScan(t.Result);
                _status.Text += $"   (lido em {_clock.Elapsed.TotalSeconds:0.0} s)";
            }));
        });
    }

    /// <summary>Mostra o resumo do arquivo (público para testes).</summary>
    public void LoadScan(IfcScan scan)
    {
        Scan = scan;
        if (string.IsNullOrEmpty(_file)) { _file = scan.File; _fileBox.Text = Path.GetFileName(scan.File); }
        int elements = scan.Types.Where(t => !IsAdvanced(t.Name)).Sum(t => t.Count);
        _status.Text = $"{scan.Schema} · {scan.ProjectName ?? "projeto sem nome"} · {scan.Storeys.Count} nível(is) · {elements} elemento(s)"
                       + (scan.Warnings.Count > 0 ? "   ⚠ " + string.Join(" ", scan.Warnings) : "");
        _status.ForeColor = Brand.Muted;

        _storeys.Rows.Clear();
        int i = 0;
        foreach (ScanStorey s in scan.Storeys)
        {
            short aci = Settings.StoreyColors.TryGetValue(s.Name, out short c) ? c : ImportNaming.StoreyColor(i);
            string elev = s.Elevation is double e ? (e / scan.UnitsPerMetre).ToString("N3", CultureInfo.GetCultureInfo("pt-BR")) : "—";
            int r = _storeys.Rows.Add(true, aci.ToString(), s.Name, elev, s.Count);
            _storeys.Rows[r].Tag = s;
            PaintSwatch(_storeys.Rows[r].Cells[1], aci);
            i++;
        }
        _types.Rows.Clear();
        foreach (ScanType t in scan.Types.Where(t => !IsAdvanced(t.Name)))
        {
            int r = _types.Rows.Add(true, FriendlyName(t.Name), t.Name, t.Count);
            _types.Rows[r].Tag = t.Name;
        }
        _openings.Text = $"Incluir aberturas (IfcOpeningElement){Count(scan, "IfcOpeningElement")}";
        _spaces.Text = $"Incluir espaços (IfcSpace){Count(scan, "IfcSpace")}";
    }

    private void UpdateBarText()
    {
        TimeSpan t = _clock.Elapsed;
        string time = t.TotalHours >= 1 ? t.ToString(@"h\:mm\:ss") : t.ToString(@"mm\:ss");
        _barText.Text = (_pct >= 0 ? $"{_pct}%" : "trabalhando...") + "  ·  " + _stageText + "  ·  " + time;
    }

    private static string Count(IfcScan s, string type) => s.Types.FirstOrDefault(t => t.Name == type) is { } t ? $" — {t.Count}" : " — nenhum";

    private static bool IsAdvanced(string type) => type is "IfcOpeningElement" or "IfcSpace";

    private void PickStoreyColor(int row)
    {
        DataGridViewCell cell = _storeys.Rows[row].Cells[1];
        short cur = short.TryParse(cell.Value?.ToString(), out short v) ? v : (short)7;
        short? next = _pickColor != null ? _pickColor(cur) : NextPaletteColor(cur);
        if (next is not short n) return;
        cell.Value = n.ToString();
        PaintSwatch(cell, n);
    }

    private static short NextPaletteColor(short cur)
    {
        int i = Array.IndexOf(ImportNaming.StoreyPalette, cur);
        return ImportNaming.StoreyPalette[(i + 1) % ImportNaming.StoreyPalette.Length];
    }

    private static void PaintSwatch(DataGridViewCell cell, short aci)
    {
        Color c = Aci.ToColor(aci);
        cell.Style.BackColor = c; cell.Style.SelectionBackColor = c;
        bool dark = c.R * 0.299 + c.G * 0.587 + c.B * 0.114 < 140;
        cell.Style.ForeColor = cell.Style.SelectionForeColor = dark ? Color.White : Color.Black;
        cell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
    }

    // ------------------------------------------------------------------ opções

    private void LoadSettings()
    {
        ImportSettings s = Settings;
        (s.Geometry == ImportGeometry.Mesh ? _mesh : _solid).Checked = true;
        _failMesh.Checked = s.FailuresAsMesh; _blocks.Checked = s.Blocks;
        (s.LayerMode switch { ImportLayerMode.ByIfcType => _layType, ImportLayerMode.ByIfcLayer => _layIfc, ImportLayerMode.Single => _laySingle, _ => _layStorey }).Checked = true;
        _singleLayer.Text = s.SingleLayer;
        (s.ColorMode switch { ImportColorMode.Original => _colOrig, ImportColorMode.Single => _colSingle, _ => _colStorey }).Checked = true;
        _singleColor.Value = Math.Clamp(s.SingleColor, (short)1, (short)255);
        _openings.Checked = s.IncludeOpenings; _spaces.Checked = s.IncludeSpaces;
        foreach (Control c in new Control[] { _fileBox, _singleLayer, _singleColor }) { c.BackColor = Brand.Field; c.ForeColor = Brand.BgLight; }
        UpdateEnabled();
    }

    private void UpdateEnabled()
    {
        _failMesh.Enabled = _solid.Checked;
        _singleLayer.Enabled = _laySingle.Checked;
        _singleColor.Enabled = _colSingle.Checked;
    }

    /// <summary>Valida e monta <see cref="Choice"/>. Retorna false (com erros no rodapé) se faltar algo.</summary>
    public bool Collect()
    {
        var errors = new List<string>();
        ImportSettings s = Settings;
        s.Geometry = _mesh.Checked ? ImportGeometry.Mesh : ImportGeometry.Solid;
        s.FailuresAsMesh = _failMesh.Checked; s.Blocks = _blocks.Checked;
        s.LayerMode = _layType.Checked ? ImportLayerMode.ByIfcType : _layIfc.Checked ? ImportLayerMode.ByIfcLayer : _laySingle.Checked ? ImportLayerMode.Single : ImportLayerMode.ByStorey;
        s.SingleLayer = _singleLayer.Text.Trim();
        s.ColorMode = _colOrig.Checked ? ImportColorMode.Original : _colSingle.Checked ? ImportColorMode.Single : ImportColorMode.ByStorey;
        s.SingleColor = (short)_singleColor.Value;
        s.IncludeOpenings = _openings.Checked; s.IncludeSpaces = _spaces.Checked;

        var storeyColors = new Dictionary<string, short>();
        var storeyNames = new Dictionary<string, string>();
        var storeyKeys = new List<string>();
        foreach (DataGridViewRow r in _storeys.Rows)
        {
            if (r.Tag is not ScanStorey st) continue;
            short aci = short.TryParse(r.Cells[1].Value?.ToString(), out short v) ? v : (short)7;
            s.StoreyColors[st.Name] = aci;                        // lembra a cor pelo nome do nível
            storeyColors[st.Key] = aci; storeyNames[st.Key] = st.Name;
            if (r.Cells[0].Value is true) storeyKeys.Add(st.Key);
        }
        var types = _types.Rows.Cast<DataGridViewRow>().Where(r => r.Cells[0].Value is true).Select(r => (string)r.Tag!).ToList();

        if (Scan == null) errors.Add("Abra um arquivo IFC.");
        else
        {
            if (storeyKeys.Count == 0 && _storeys.Rows.Count > 0) errors.Add("Marque ao menos um nível.");
            if (types.Count == 0 && !s.IncludeOpenings && !s.IncludeSpaces) errors.Add("Marque ao menos um tipo.");
        }
        if (s.LayerMode == ImportLayerMode.Single && ImportNaming.Layer("", s.SingleLayer) != s.SingleLayer)
            errors.Add("Nome de layer inválido (não use < > / \\ \" : ; ? * | , = `).");
        if (s.LayerMode == ImportLayerMode.Single && s.SingleLayer.Length == 0) errors.Add("Informe o nome da layer.");

        LastErrors = errors;
        _errors.Text = string.Join("   •   ", errors);
        if (errors.Count > 0) return false;

        // Aberturas e espaços entram pelas opções avançadas, não pela lista de tipos.
        if (s.IncludeOpenings) types.Add("IfcOpeningElement");
        if (s.IncludeSpaces) types.Add("IfcSpace");
        Choice = new ImportChoice
        {
            IfcFile = _file, Scan = Scan!, Types = types, StoreyKeys = storeyKeys,
            StoreyColors = storeyColors, StoreyNames = storeyNames, Settings = s,
        };
        return true;
    }

    // ------------------------------------------------------------------ nomes amigáveis

    private static readonly Dictionary<string, string> Friendly = new(StringComparer.Ordinal)
    {
        ["IfcBeam"] = "Vigas", ["IfcColumn"] = "Pilares", ["IfcSlab"] = "Lajes", ["IfcWall"] = "Paredes", ["IfcWallStandardCase"] = "Paredes",
        ["IfcMember"] = "Barras / montantes", ["IfcPlate"] = "Chapas / placas", ["IfcDoor"] = "Portas", ["IfcWindow"] = "Janelas",
        ["IfcStair"] = "Escadas", ["IfcStairFlight"] = "Lances de escada", ["IfcRamp"] = "Rampas", ["IfcRampFlight"] = "Lances de rampa",
        ["IfcRailing"] = "Guarda-corpos", ["IfcRoof"] = "Coberturas", ["IfcFooting"] = "Fundações", ["IfcPile"] = "Estacas",
        ["IfcCovering"] = "Revestimentos / forros", ["IfcCurtainWall"] = "Fachadas-cortina", ["IfcBuildingElementProxy"] = "Outros (proxy)",
        ["IfcFurnishingElement"] = "Mobiliário", ["IfcFurniture"] = "Mobiliário", ["IfcMechanicalFastener"] = "Parafusos / chumbadores",
        ["IfcFastener"] = "Fixadores / soldas", ["IfcReinforcingBar"] = "Armaduras", ["IfcReinforcingMesh"] = "Telas", ["IfcTendon"] = "Cordoalhas",
        ["IfcFlowSegment"] = "Tubulações / dutos", ["IfcFlowFitting"] = "Conexões", ["IfcFlowTerminal"] = "Terminais", ["IfcPipeSegment"] = "Tubos",
        ["IfcDuctSegment"] = "Dutos", ["IfcCableCarrierSegment"] = "Eletrocalhas", ["IfcSite"] = "Terreno", ["IfcGeographicElement"] = "Elementos do terreno",
        ["IfcElementAssembly"] = "Conjuntos", ["IfcDiscreteAccessory"] = "Acessórios", ["IfcChimney"] = "Chaminés", ["IfcShadingDevice"] = "Brises",
    };

    public static string FriendlyName(string ifc) => Friendly.TryGetValue(ifc, out string? n) ? n : ifc.StartsWith("Ifc", StringComparison.Ordinal) ? ifc[3..] : ifc;

    // ------------------------------------------------------------------ estilo

    private static DataGridView Grid()
    {
        var g = new DataGridView
        {
            Dock = DockStyle.Fill, AllowUserToAddRows = false, AllowUserToDeleteRows = false, AllowUserToResizeRows = false,
            RowHeadersVisible = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false,
            BackgroundColor = Brand.BgDark, BorderStyle = BorderStyle.None, GridColor = Brand.Line, EnableHeadersVisualStyles = false,
            ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing, ColumnHeadersHeight = 28,
            CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal, Margin = new Padding(0, 2, 0, 8),
        };
        g.ColumnHeadersDefaultCellStyle.BackColor = Brand.Ink;
        g.ColumnHeadersDefaultCellStyle.ForeColor = Brand.Muted;
        g.ColumnHeadersDefaultCellStyle.Font = Brand.Mono(8f);
        g.ColumnHeadersDefaultCellStyle.SelectionBackColor = Brand.Ink;
        g.ColumnHeadersDefaultCellStyle.SelectionForeColor = Brand.Muted;
        g.DefaultCellStyle.BackColor = Brand.BgDark;
        g.DefaultCellStyle.ForeColor = Brand.BgLight;
        g.DefaultCellStyle.SelectionBackColor = Brand.Hex("#262b35");
        g.DefaultCellStyle.SelectionForeColor = Brand.BgLight;
        g.RowTemplate.Height = 26;
        // Marcar/desmarcar com um clique.
        g.CurrentCellDirtyStateChanged += (_, _) => { if (g.IsCurrentCellDirty) g.CommitEdit(DataGridViewDataErrorContexts.Commit); };
        return g;
    }

    private static Control HeaderRow(string title, string hint, out Button all, out Button none)
    {
        var p = new FlowLayoutPanel { AutoSize = true, BackColor = Brand.Ink, Margin = new Padding(0, 6, 0, 0), WrapContents = false };
        var l = Brand.SectionLabel(title); l.Margin = new Padding(3, 8, 10, 3);
        all = Brand.Btn("Todos"); none = Brand.Btn("Nenhum");
        all.Padding = none.Padding = new Padding(4, 0, 4, 0);
        p.Controls.Add(l); p.Controls.Add(all); p.Controls.Add(none);
        p.Controls.Add(new Label { Text = hint, AutoSize = true, ForeColor = Brand.Muted, Margin = new Padding(10, 9, 3, 3) });
        return p;
    }

    private static void CheckAll(DataGridView g, bool on) { foreach (DataGridViewRow r in g.Rows) r.Cells[0].Value = on; }
    private static RadioButton Radio(string t) => new() { Text = t, AutoSize = true, ForeColor = Brand.BgLight, Margin = new Padding(3, 3, 3, 3) };
    private static CheckBox Check(string t) => new() { Text = t, AutoSize = true, ForeColor = Brand.BgLight, Margin = new Padding(3, 4, 3, 2) };
    private static Label Note(string t) => new() { Text = t, AutoSize = false, Width = 340, Height = 48, ForeColor = Brand.Muted, Font = Brand.Sans(8f), Margin = new Padding(22, 2, 3, 6) };

    private static FlowLayoutPanel Group(params RadioButton[] radios)
    {
        var p = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, BackColor = Brand.Ink, Margin = new Padding(0) };
        foreach (RadioButton r in radios) p.Controls.Add(r);
        return p;
    }

    private static FlowLayoutPanel Line(params Control[] controls)
    {
        var p = new FlowLayoutPanel { AutoSize = true, BackColor = Brand.Ink, WrapContents = false, Margin = new Padding(0) };
        foreach (Control c in controls) p.Controls.Add(c);
        return p;
    }
}
