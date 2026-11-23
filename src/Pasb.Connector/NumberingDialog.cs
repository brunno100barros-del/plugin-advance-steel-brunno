using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Forms;
using Pasb.Model;
using Pasb.Ui;

namespace Pasb.Connector;

/// <summary>Opções da numeração lembradas em %APPDATA%\PASB\numbering-settings.json.</summary>
public static class NumberingSettings
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true, Converters = { new JsonStringEnumConverter() } };

    public static string DefaultPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PASB", "numbering-settings.json");

    public static ItemOptions Load(string? path = null)
    {
        try
        {
            string p = path ?? DefaultPath;
            if (!File.Exists(p)) return new ItemOptions();
            ItemOptions o = JsonSerializer.Deserialize<ItemOptions>(File.ReadAllText(p), Json) ?? new ItemOptions();
            foreach (ItemCategory c in Enum.GetValues<ItemCategory>()) if (!o.Order.Contains(c)) o.Order.Add(c);   // categorias novas no fim
            o.Order = o.Order.Distinct().ToList();
            return o;
        }
        catch (Exception) { return new ItemOptions(); }
    }

    public static void Save(ItemOptions o, string? path = null)
    {
        try
        {
            string p = path ?? DefaultPath;
            Directory.CreateDirectory(Path.GetDirectoryName(p)!);
            File.WriteAllText(p, JsonSerializer.Serialize(o, Json), new UTF8Encoding(false));
        }
        catch (Exception) { }
    }
}

/// <summary>
/// Janela "Numeração itemizada" (identidade Coordix): categorias e ordem, campo de destino, prefixo, início, ordenação,
/// filtros e prévia ao vivo. OK = numerar com <see cref="Options"/> e <see cref="Result"/>.
/// </summary>
public sealed class NumberingDialog : Form
{
    public ItemOptions Options { get; }
    public ItemResult Result { get; private set; }
    private readonly Extraction _model;
    private bool _loading = true;

    private readonly DataGridView _cats = new();
    private readonly CheckBox _groupBent = Check("Agrupar vigas curvas e chapas dobradas com as retas/planas");
    private readonly ComboBox _target = Combo(240);
    private readonly TextBox _prefix = new() { Width = 200 };
    private readonly NumericUpDown _start = new() { Minimum = 0, Maximum = 999999, Width = 100 };
    private readonly ComboBox _order = Combo(200), _sortBy = Combo(200);
    private readonly DataGridView _preview = new();
    private readonly Label _summary = new() { AutoSize = false, Dock = DockStyle.Fill, Height = 22, ForeColor = Brand.CopperDark, Font = Brand.Mono(8.5f) };
    private readonly ComboBox _fField = Combo(250), _fOp = Combo(120);
    private readonly TextBox _fValue = new() { Width = 124 };
    private readonly ListBox _filters = new() { Width = 252, Height = 170, IntegralHeight = false };
    private readonly Label _outCount = new() { AutoSize = true, ForeColor = Brand.Muted };
    private readonly Button _ok = Brand.Btn("Numerar", primary: true), _cancel = Brand.Btn("Cancelar");

    public NumberingDialog(Extraction model, ItemOptions options, string version = "")
    {
        _model = model; Options = options;
        Text = "PASB — Numeração itemizada" + (version.Length > 0 ? "  (" + version + ")" : "");
        BackColor = Brand.BgDark; ForeColor = Brand.BgLight; Font = Brand.Sans(9f);
        FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(1200, 780);
        AcceptButton = _ok; CancelButton = _cancel;

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 2, Padding = new Padding(10), BackColor = Brand.BgDark };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 320));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 290));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
        Controls.Add(Brand.Header("Numeração itemizada", version));
        Controls.Add(root);
        root.BringToFront();

        // ---- esquerda: categorias e opções
        var left = Column();
        left.Controls.Add(Brand.SectionLabel("Categorias"));
        StyleGrid(_cats, rowHeaders: false);
        _cats.Width = 270; _cats.Height = 4 * 28 + 4; _cats.ColumnHeadersVisible = false; _cats.Dock = DockStyle.None;
        _cats.Columns.Add(new DataGridViewCheckBoxColumn { Width = 30 });
        _cats.Columns.Add(new DataGridViewTextBoxColumn { AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, ReadOnly = true });
        _cats.Columns.Add(new DataGridViewButtonColumn { Text = "↑", UseColumnTextForButtonValue = true, Width = 34, FlatStyle = FlatStyle.Flat });
        _cats.Columns.Add(new DataGridViewButtonColumn { Text = "↓", UseColumnTextForButtonValue = true, Width = 34, FlatStyle = FlatStyle.Flat });
        _cats.CellContentClick += (_, e) => { if (e.RowIndex >= 0 && e.ColumnIndex >= 2) MoveCategory(e.RowIndex, e.ColumnIndex == 2 ? -1 : 1); };
        left.Controls.Add(new Label { Text = "Marque e use ↑ ↓ para a ordem da numeração.", AutoSize = true, ForeColor = Brand.Muted, Font = Brand.Sans(8f) });
        left.Controls.Add(_cats);
        left.Controls.Add(_groupBent);
        left.Controls.Add(Brand.SectionLabel("Gravar em"));
        left.Controls.Add(_target);
        left.Controls.Add(Note("Numeração preliminar: prefixo e número vão nos campos nativos do Advance Steel. Os demais campos recebem a marca completa (prefixo + número)."));
        left.Controls.Add(Brand.SectionLabel("Prefixo"));
        left.Controls.Add(_prefix);
        left.Controls.Add(Brand.SectionLabel("Começar em"));
        left.Controls.Add(_start);
        left.Controls.Add(Brand.SectionLabel("Ordenação"));
        left.Controls.Add(_order); left.Controls.Add(_sortBy);
        root.Controls.Add(left, 0, 0);

        // ---- centro: prévia
        var mid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, BackColor = Brand.Ink, Padding = new Padding(12), Margin = new Padding(4) };
        mid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        mid.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        mid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        mid.Controls.Add(Brand.SectionLabel("Prévia"), 0, 0);
        StyleGrid(_preview, rowHeaders: false);
        _preview.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Marca", Width = 90 });
        _preview.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Tipo", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
        _preview.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Categoria", Width = 110 });
        _preview.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Qtd", Width = 60, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleRight } });
        foreach (DataGridViewColumn c in _preview.Columns) { c.ReadOnly = true; c.SortMode = DataGridViewColumnSortMode.NotSortable; }
        _preview.DefaultCellStyle.Font = Brand.Mono(9f);
        mid.Controls.Add(_preview, 0, 1);
        mid.Controls.Add(_summary, 0, 2);
        root.Controls.Add(mid, 1, 0);

        // ---- direita: filtros
        var right = Column();
        right.Controls.Add(Brand.SectionLabel("Filtros"));
        right.Controls.Add(new Label { Text = "Tirar da numeração as peças em que:", AutoSize = true, ForeColor = Brand.Muted, Font = Brand.Sans(8f) });
        right.Controls.Add(_fField);
        var opLine = new FlowLayoutPanel { AutoSize = true, BackColor = Brand.Ink, WrapContents = false, Margin = new Padding(0) };
        opLine.Controls.Add(_fOp); opLine.Controls.Add(_fValue);
        right.Controls.Add(opLine);
        var add = Brand.Btn("Adicionar"); var clear = Brand.Btn("Limpar todos");
        var btnLine = new FlowLayoutPanel { AutoSize = true, BackColor = Brand.Ink, WrapContents = false, Margin = new Padding(0, 4, 0, 4) };
        btnLine.Controls.Add(add); btnLine.Controls.Add(clear);
        right.Controls.Add(btnLine);
        right.Controls.Add(_filters);
        right.Controls.Add(_outCount);
        var showOut = Brand.Btn("Mostrar o que saiu");
        right.Controls.Add(showOut);
        right.Controls.Add(Note("Só são numeradas as peças que passam em TODOS os filtros (E). Peça que fica fora não é numerada e o campo dela não é alterado. Duplo clique num filtro remove."));
        root.Controls.Add(right, 2, 0);

        // ---- rodapé
        var buttons = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Fill, BackColor = Brand.BgDark };
        _ok.Width = 130; _cancel.Width = 100;
        buttons.Controls.Add(_ok); buttons.Controls.Add(_cancel);
        root.Controls.Add(buttons, 0, 1); root.SetColumnSpan(buttons, 3);

        foreach (Control c in new Control[] { _prefix, _start, _fValue, _filters, _target, _order, _sortBy, _fField, _fOp })
        { c.BackColor = Brand.Field; c.ForeColor = Brand.BgLight; }

        // Combos
        foreach (ItemTarget t in Enum.GetValues<ItemTarget>()) _target.Items.Add(new Item<ItemTarget>(t, ItemNumbering.TargetName(t)));
        _order.Items.AddRange(new object[] { "Crescente", "Decrescente" });
        _sortBy.Items.AddRange(new object[] { "Por nome (tipo)", "Por quantidade" });
        foreach (FilterField f in Enum.GetValues<FilterField>()) _fField.Items.Add(new Item<FilterField>(f, ItemNumbering.FieldName(f)));
        foreach (FilterOp o in Enum.GetValues<FilterOp>()) _fOp.Items.Add(new Item<FilterOp>(o, ItemNumbering.OpName(o)));
        _fField.SelectedIndex = 0; _fOp.SelectedIndex = 1;

        // Eventos
        _cats.CurrentCellDirtyStateChanged += (_, _) => { if (_cats.IsCurrentCellDirty) _cats.CommitEdit(DataGridViewDataErrorContexts.Commit); };
        _cats.CellValueChanged += (_, _) => Refresh_();
        _groupBent.CheckedChanged += (_, _) => Refresh_();
        _target.SelectedIndexChanged += (_, _) => Refresh_();
        _prefix.TextChanged += (_, _) => Refresh_();
        _start.ValueChanged += (_, _) => Refresh_();
        _order.SelectedIndexChanged += (_, _) => Refresh_();
        _sortBy.SelectedIndexChanged += (_, _) => Refresh_();
        _fOp.SelectedIndexChanged += (_, _) => _fValue.Enabled = _fOp.SelectedItem is Item<FilterOp> sel && sel.Value is not (FilterOp.IsEmpty or FilterOp.IsNotEmpty);
        add.Click += (_, _) => AddFilter();
        _fValue.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) { AddFilter(); e.SuppressKeyPress = true; } };
        clear.Click += (_, _) => { Options.Filters.Clear(); LoadFilters(); Refresh_(); };
        _filters.DoubleClick += (_, _) => { if (_filters.SelectedIndex >= 0) { Options.Filters.RemoveAt(_filters.SelectedIndex); LoadFilters(); Refresh_(); } };
        showOut.Click += (_, _) => ShowExcluded();
        _ok.Click += (_, _) => { Collect(); if (Result.Rows.Count == 0) { MessageBox.Show(this, "Nenhuma peça para numerar com essas opções.", "PASB", MessageBoxButtons.OK, MessageBoxIcon.Information); return; } DialogResult = DialogResult.OK; Close(); };
        _cancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };

        // Valores iniciais
        foreach (ItemCategory c in Options.Order)
        {
            int r = _cats.Rows.Add(Options.Enabled.Contains(c), ItemNumbering.CategoryName(c));
            _cats.Rows[r].Tag = c;
        }
        _groupBent.Checked = Options.GroupBent;
        _target.SelectedIndex = (int)Options.Target;
        _prefix.Text = Options.Prefix;
        _start.Value = Math.Clamp(Options.Start, 0, 999999);
        _order.SelectedIndex = Options.Descending ? 1 : 0;
        _sortBy.SelectedIndex = Options.SortBy == ItemSortBy.Quantity ? 1 : 0;
        LoadFilters();
        _loading = false;
        Result = new ItemResult(Array.Empty<ItemRow>(), Array.Empty<ItemExcluded>());
        Refresh_();
    }

    // ------------------------------------------------------------------ dados ↔ controles

    /// <summary>Copia os controles para <see cref="Options"/> e recalcula <see cref="Result"/> (público para testes).</summary>
    public void Collect()
    {
        Options.Order = _cats.Rows.Cast<DataGridViewRow>().Select(r => (ItemCategory)r.Tag!).ToList();
        Options.Enabled = _cats.Rows.Cast<DataGridViewRow>().Where(r => r.Cells[0].Value is true).Select(r => (ItemCategory)r.Tag!).ToHashSet();
        Options.GroupBent = _groupBent.Checked;
        Options.Target = ((Item<ItemTarget>)_target.SelectedItem!).Value;
        Options.Prefix = _prefix.Text.Trim();
        Options.Start = (int)_start.Value;
        Options.Descending = _order.SelectedIndex == 1;
        Options.SortBy = _sortBy.SelectedIndex == 1 ? ItemSortBy.Quantity : ItemSortBy.Name;
        Result = ItemNumbering.Compute(_model, Options);
    }

    private void Refresh_()
    {
        if (_loading) return;
        Collect();
        _preview.Rows.Clear();
        foreach (ItemRow r in Result.Rows) _preview.Rows.Add(r.Mark, r.Type, ItemNumbering.CategoryName(r.Category), r.Quantity);
        int byFilter = Result.Excluded.Count(e => e.Reason.StartsWith("filtro", StringComparison.Ordinal));
        _summary.Text = $"{Result.Rows.Count} item(ns) · {Result.Parts} peça(s) a numerar · {Result.Excluded.Count} fora";
        _outCount.Text = $"{byFilter} peça(s) fora pelos filtros";
        _ok.Enabled = Result.Rows.Count > 0;
    }

    private void MoveCategory(int row, int delta)
    {
        int to = row + delta;
        if (to < 0 || to >= _cats.Rows.Count) return;
        DataGridViewRow r = _cats.Rows[row];
        _cats.Rows.RemoveAt(row);
        _cats.Rows.Insert(to, r);
        _cats.ClearSelection();
        Refresh_();
    }

    private void AddFilter()
    {
        var f = new ItemFilter(((Item<FilterField>)_fField.SelectedItem!).Value, ((Item<FilterOp>)_fOp.SelectedItem!).Value, _fValue.Text.Trim());
        if (f.Op is not (FilterOp.IsEmpty or FilterOp.IsNotEmpty) && f.Value.Length == 0) { _fValue.Focus(); return; }
        Options.Filters.Add(f);
        _fValue.Clear();
        LoadFilters();
        Refresh_();
    }

    private void LoadFilters()
    {
        _filters.Items.Clear();
        foreach (ItemFilter f in Options.Filters) _filters.Items.Add(f.ToString());
    }

    private void ShowExcluded()
    {
        var sb = new StringBuilder();
        if (Result.Excluded.Count == 0) sb.Append("Nenhuma peça ficou de fora.");
        foreach (var g in Result.Excluded.GroupBy(e => e.Reason))
        {
            sb.AppendLine(g.Key + $" — {g.Count()} peça(s):");
            foreach (ItemExcluded e in g.Take(40)) sb.AppendLine($"   {ItemNumbering.CategoryName(e.Category)} {e.Type}  (handle {e.Handle})");
            if (g.Count() > 40) sb.AppendLine($"   ... e mais {g.Count() - 40}");
            sb.AppendLine();
        }
        MessageBox.Show(this, sb.ToString(), "PASB — Peças fora da numeração", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    // ------------------------------------------------------------------ estilo

    private sealed record Item<T>(T Value, string Text) { public override string ToString() => Text; }

    private static FlowLayoutPanel Column() => new()
    {
        Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true,
        BackColor = Brand.Ink, Padding = new Padding(12), Margin = new Padding(4),
    };

    private static ComboBox Combo(int w) => new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = w, FlatStyle = FlatStyle.Flat };
    private static CheckBox Check(string t) => new() { Text = t, AutoSize = false, Width = 270, Height = 36, ForeColor = Brand.BgLight, Margin = new Padding(3, 6, 3, 2) };
    private static Label Note(string t) => new() { Text = t, AutoSize = false, Width = 260, Height = 62, ForeColor = Brand.Muted, Font = Brand.Sans(8f), Margin = new Padding(3, 4, 3, 4) };

    private static void StyleGrid(DataGridView g, bool rowHeaders)
    {
        g.Dock = DockStyle.Fill; g.AllowUserToAddRows = false; g.AllowUserToDeleteRows = false; g.AllowUserToResizeRows = false;
        g.RowHeadersVisible = rowHeaders; g.SelectionMode = DataGridViewSelectionMode.FullRowSelect; g.MultiSelect = false;
        g.BackgroundColor = Brand.BgDark; g.BorderStyle = BorderStyle.None; g.GridColor = Brand.Line; g.EnableHeadersVisualStyles = false;
        g.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing; g.ColumnHeadersHeight = 28;
        g.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        g.ColumnHeadersDefaultCellStyle.BackColor = Brand.Ink; g.ColumnHeadersDefaultCellStyle.ForeColor = Brand.Muted;
        g.ColumnHeadersDefaultCellStyle.SelectionBackColor = Brand.Ink; g.ColumnHeadersDefaultCellStyle.Font = Brand.Mono(8f);
        g.DefaultCellStyle.BackColor = Brand.BgDark; g.DefaultCellStyle.ForeColor = Brand.BgLight;
        g.DefaultCellStyle.SelectionBackColor = Brand.Hex("#262b35"); g.DefaultCellStyle.SelectionForeColor = Brand.BgLight;
        g.RowTemplate.Height = 26;
    }
}
