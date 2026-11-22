using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.GraphicsSystem;
using Autodesk.AutoCAD.Runtime;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;
using AcDoc = Autodesk.AutoCAD.ApplicationServices.Document;
using AcView = Autodesk.AutoCAD.GraphicsSystem.View;
using GsModel = Autodesk.AutoCAD.GraphicsSystem.Model;
using Font = System.Drawing.Font;
using FlowDirection = System.Windows.Forms.FlowDirection;

[assembly: CommandClass(typeof(Pasb.Diagnostics.AdvanceSteel.RenderCommand))]

namespace Pasb.Diagnostics.AdvanceSteel;

/// <summary>
/// PASB_RENDERIZAR: abre uma janela com o modelo renderizado na vista atual (render feito na memória, com o motor
/// gráfico do próprio AutoCAD; nada é gravado). Na janela: renderizar de novo, salvar a imagem, copiar e inserir na prancha.
/// PASB_VISTA_REALISTA: cria, no layout atual, uma janela de vista com estilo Realista que sai assim na impressão.
/// </summary>
public sealed class RenderCommand
{
    [CommandMethod("PASB", "PASB_RENDERIZAR", CommandFlags.Modal)]
    public void Render()
    {
        AcDoc? doc = AcApp.DocumentManager.MdiActiveDocument;
        if (doc == null) return;
        if (Convert.ToInt32(AcApp.GetSystemVariable("TILEMODE")) == 0 && Convert.ToInt32(AcApp.GetSystemVariable("CVPORT")) == 1)
        {
            doc.Editor.WriteMessage("\nPASB: renderize a partir do modelo (aba Modelo ou de dentro de uma janela de vista).\n");
            return;
        }
        int vport = Convert.ToInt32(AcApp.GetSystemVariable("CVPORT"));
        using var win = new RenderWindow(doc, vport);
        Autodesk.AutoCAD.ApplicationServices.Application.ShowModalDialog(win);
        if (win.InsertRequested is { } file) InsertImage(doc, file);
    }

    /// <summary>Render na memória da vista <paramref name="vport"/>. Realista = estilo visual; completo = materiais e luzes.</summary>
    internal static Bitmap RenderView(AcDoc doc, int vport, Size size, bool full, Color background)
    {
        Database db = doc.Database;
        Manager gsm = doc.GraphicsManager;
        var kd = new KernelDescriptor();
        kd.addRequirement(full ? KernelDescriptor.RapidRTRendering3D : KernelDescriptor.Drawing3D);
        GraphicsKernel kernel = Manager.AcquireGraphicsKernel(kd);
        using (doc.LockDocument())
        using (var view = new AcView())
        {
            gsm.SetViewFromViewport(view, vport);
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                var styles = (DBDictionary)tr.GetObject(db.VisualStyleDictionaryId, OpenMode.ForRead);
                if (styles.Contains("Realistic")) view.VisualStyleId = styles.GetAt("Realistic");
                tr.Commit();
            }
            using Device dev = gsm.CreateAutoCADOffScreenDevice(kernel);
            dev.OnSize(size);
            dev.DeviceRenderType = full ? RendererType.FullRender : RendererType.Default;
            dev.BackgroundColor = background;
            dev.Add(view);
            dev.Update();
            using GsModel model = gsm.CreateAutoCADModel(kernel);
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                var space = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForRead);
                view.Add(space, model);
                tr.Commit();
            }
            dev.Update();
            Bitmap? img = full ? view.RenderToImage() : null;
            return img ?? view.GetSnapshot(new Rectangle(0, 0, size.Width, size.Height));
        }
    }

    /// <summary>Insere a imagem salva (referência de imagem do AutoCAD) num ponto e largura escolhidos no espaço atual.</summary>
    private static void InsertImage(AcDoc doc, string file)
    {
        Editor ed = doc.Editor;
        PromptPointResult p1 = ed.GetPoint(new PromptPointOptions("\nPASB: canto inferior esquerdo da imagem na prancha: "));
        if (p1.Status != PromptStatus.OK) return;
        PromptPointResult p2 = ed.GetCorner(new PromptCornerOptions("\nPASB: canto oposto (define a largura): ", p1.Value));
        if (p2.Status != PromptStatus.OK) return;
        double width = Math.Abs(p2.Value.X - p1.Value.X);
        if (width < 1e-6) return;
        Matrix3d ucs = ed.CurrentUserCoordinateSystem;
        Point3d a = new Point3d(Math.Min(p1.Value.X, p2.Value.X), Math.Min(p1.Value.Y, p2.Value.Y), 0).TransformBy(ucs);
        Database db = doc.Database;
        using (doc.LockDocument())
        using (Transaction tr = db.TransactionManager.StartTransaction())
        {
            ObjectId dictId = RasterImageDef.GetImageDictionary(db);
            if (dictId.IsNull) dictId = RasterImageDef.CreateImageDictionary(db);
            var dict = (DBDictionary)tr.GetObject(dictId, OpenMode.ForWrite);
            string name = "PASB_RENDER_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var def = new RasterImageDef { SourceFileName = file };
            def.Load();
            ObjectId defId = dict.SetAt(name, def);
            tr.AddNewlyCreatedDBObject(def, true);
            Vector2d px = def.Size;
            double height = width * px.Y / Math.Max(1, px.X);
            var img = new RasterImage { ImageDefId = defId };
            img.Orientation = new CoordinateSystem3d(a, Vector3d.XAxis.TransformBy(ucs) * width, Vector3d.YAxis.TransformBy(ucs) * height);
            img.ShowImage = true;
            var space = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
            space.AppendEntity(img);
            tr.AddNewlyCreatedDBObject(img, true);
            img.AssociateRasterDef(def);
            tr.Commit();
        }
        ed.WriteMessage($"\nPASB: imagem inserida ({file}).\n");
    }

    // ------------------------------------------------------------------ vista realista na prancha

    [CommandMethod("PASB", "PASB_VISTA_REALISTA", CommandFlags.Modal)]
    public void RealisticViewport()
    {
        AcDoc? doc = AcApp.DocumentManager.MdiActiveDocument;
        if (doc == null) return;
        Editor ed = doc.Editor;
        Database db = doc.Database;
        if (Convert.ToInt32(AcApp.GetSystemVariable("TILEMODE")) == 1)
        {
            ed.WriteMessage("\nPASB: abra a aba de layout (prancha) onde a vista vai ficar e rode o comando de novo.\n");
            return;
        }
        if (Convert.ToInt32(AcApp.GetSystemVariable("CVPORT")) != 1) ed.SwitchToPaperSpace();
        PromptPointResult p1 = ed.GetPoint(new PromptPointOptions("\nPASB: 1º canto da vista realista: "));
        if (p1.Status != PromptStatus.OK) return;
        PromptPointResult p2 = ed.GetCorner(new PromptCornerOptions("\nPASB: canto oposto: ", p1.Value));
        if (p2.Status != PromptStatus.OK) return;
        double w = Math.Abs(p2.Value.X - p1.Value.X), h = Math.Abs(p2.Value.Y - p1.Value.Y);
        if (w < 1e-6 || h < 1e-6) return;
        var center = new Point3d((p1.Value.X + p2.Value.X) / 2, (p1.Value.Y + p2.Value.Y) / 2, 0);

        using (doc.LockDocument())
        using (Transaction tr = db.TransactionManager.StartTransaction())
        {
            // Mesma vista que está na aba Modelo (direção, alvo, centro e altura).
            var vpt = (ViewportTable)tr.GetObject(db.ViewportTableId, OpenMode.ForRead);
            ViewportTableRecord? model = vpt.Has("*Active") ? (ViewportTableRecord)tr.GetObject(vpt["*Active"], OpenMode.ForRead) : null;
            var vp = new Viewport { CenterPoint = center, Width = w, Height = h };
            var space = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
            space.AppendEntity(vp);
            tr.AddNewlyCreatedDBObject(vp, true);
            if (model != null)
            {
                vp.ViewDirection = model.ViewDirection;
                vp.ViewTarget = model.Target;
                vp.TwistAngle = model.ViewTwist;
                vp.ViewCenter = model.CenterPoint;
                vp.ViewHeight = Math.Max(model.Height, model.Width * h / w);
            }
            var styles = (DBDictionary)tr.GetObject(db.VisualStyleDictionaryId, OpenMode.ForRead);
            if (styles.Contains("Realistic")) vp.VisualStyleId = styles.GetAt("Realistic");
            vp.ShadePlot = ShadePlotType.AsDisplayed;
            vp.On = true;
            tr.Commit();
        }
        ed.Regen();
        ed.WriteMessage("\nPASB: vista realista criada (estilo Realista, impressão \"Como exibido\"). Para ajustar o enquadramento, dê duplo clique dentro dela.\n");
    }
}

/// <summary>Janela do render: imagem, renderizar de novo, salvar, copiar e inserir na prancha.</summary>
internal sealed class RenderWindow : Form
{
    private readonly AcDoc _doc;
    private readonly int _vport;
    private readonly PictureBox _pic = new() { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.FromArgb(40, 44, 52) };
    private readonly ComboBox _mode = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 230 };
    private readonly ComboBox _size = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 130 };
    private readonly ComboBox _bg = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 90 };
    private readonly Label _status = new() { AutoSize = true, Padding = new Padding(0, 6, 0, 0) };
    private Bitmap? _image;

    /// <summary>Arquivo salvo que o usuário pediu para inserir na prancha (o comando insere depois que a janela fecha).</summary>
    public string? InsertRequested { get; private set; }

    public RenderWindow(AcDoc doc, int vport)
    {
        _doc = doc; _vport = vport;
        Text = "PASB — Renderizar";
        StartPosition = FormStartPosition.CenterScreen;
        Size = new Size(1100, 760);
        MinimumSize = new Size(700, 450);
        Font = new Font("Segoe UI", 9f);
        KeyPreview = true;

        _mode.Items.AddRange(new object[] { "Realista (rápido)", "Render completo (materiais e luzes)" });
        _mode.SelectedIndex = 0;
        _size.Items.AddRange(new object[] { "1280 x 720", "1920 x 1080", "2560 x 1440", "3840 x 2160" });
        _size.SelectedIndex = 1;
        _bg.Items.AddRange(new object[] { "Branco", "Cinza", "Preto" });
        _bg.SelectedIndex = 0;

        var render = new Button { Text = "Renderizar", AutoSize = true };
        var save = new Button { Text = "Salvar imagem...", AutoSize = true };
        var copy = new Button { Text = "Copiar", AutoSize = true };
        var insert = new Button { Text = "Inserir na prancha...", AutoSize = true };
        var close = new Button { Text = "Fechar", AutoSize = true, DialogResult = DialogResult.Cancel };
        render.Click += (_, _) => DoRender();
        save.Click += (_, _) => Save(insertAfter: false);
        copy.Click += (_, _) => { if (_image != null) { Clipboard.SetImage(_image); _status.Text = "Imagem copiada para a área de transferência."; } };
        insert.Click += (_, _) => Save(insertAfter: true);
        CancelButton = close;

        var bar = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(8), WrapContents = true };
        bar.Controls.AddRange(new Control[] { new Label { Text = "Modo:", AutoSize = true, Padding = new Padding(0, 6, 0, 0) }, _mode,
            new Label { Text = "Tamanho:", AutoSize = true, Padding = new Padding(8, 6, 0, 0) }, _size,
            new Label { Text = "Fundo:", AutoSize = true, Padding = new Padding(8, 6, 0, 0) }, _bg, render });
        var bottom = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, Padding = new Padding(8), FlowDirection = FlowDirection.LeftToRight };
        bottom.Controls.AddRange(new Control[] { save, copy, insert, close, _status });
        Controls.Add(_pic);
        Controls.Add(bar);
        Controls.Add(bottom);
        Shown += (_, _) => DoRender();
    }

    private void DoRender()
    {
        string[] wh = ((string)_size.SelectedItem!).Split('x');
        var size = new Size(int.Parse(wh[0].Trim()), int.Parse(wh[1].Trim()));
        Color bg = _bg.SelectedIndex switch { 1 => Color.FromArgb(128, 128, 128), 2 => Color.Black, _ => Color.White };
        bool full = _mode.SelectedIndex == 1;
        _status.Text = full ? "Renderizando (materiais e luzes)... pode levar alguns segundos." : "Renderizando...";
        Cursor = Cursors.WaitCursor;
        Refresh();
        var t0 = DateTime.Now;
        try
        {
            Bitmap img = RenderCommand.RenderView(_doc, _vport, size, full, bg);
            _pic.Image = null;
            _image?.Dispose();
            _image = img;
            _pic.Image = img;
            _status.Text = $"Pronto: {size.Width} x {size.Height}, {(DateTime.Now - t0).TotalSeconds:0.0} s. Nada foi salvo; use \"Salvar imagem...\" se quiser.";
        }
        catch (System.Exception ex)
        {
            StartupLog.Write("Render falhou: " + ex);
            if (full)
            {
                // Render completo indisponível nesta máquina: mostra pelo menos a vista realista.
                try
                {
                    Bitmap img = RenderCommand.RenderView(_doc, _vport, size, false, bg);
                    _pic.Image = null; _image?.Dispose(); _image = img; _pic.Image = img;
                    _status.Text = "O render completo falhou (" + ex.Message + "); mostrando a vista realista.";
                    return;
                }
                catch (System.Exception ex2) { StartupLog.Write("Render realista falhou: " + ex2); }
            }
            _status.Text = "Falhou: " + ex.Message;
        }
        finally { Cursor = Cursors.Default; }
    }

    private void Save(bool insertAfter)
    {
        if (_image == null) return;
        using var dlg = new SaveFileDialog
        {
            Title = insertAfter ? "Salvar a imagem para inserir na prancha" : "Salvar imagem",
            Filter = "PNG (*.png)|*.png|JPEG (*.jpg)|*.jpg",
            FileName = "Render " + DateTime.Now.ToString("yyyy-MM-dd HH-mm") + ".png",
        };
        string dwg = _doc.Database.Filename;
        try { if (!string.IsNullOrEmpty(dwg)) dlg.InitialDirectory = System.IO.Path.GetDirectoryName(dwg); } catch (System.Exception) { }
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        _image.Save(dlg.FileName, dlg.FilterIndex == 2 ? ImageFormat.Jpeg : ImageFormat.Png);
        _status.Text = "Salvo em " + dlg.FileName;
        if (insertAfter) { InsertRequested = dlg.FileName; DialogResult = DialogResult.OK; Close(); }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { _pic.Image = null; _image?.Dispose(); }
        base.Dispose(disposing);
    }
}
