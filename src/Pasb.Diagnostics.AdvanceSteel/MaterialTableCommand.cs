using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Autodesk.AdvanceSteel.CADAccess;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Pasb.Connector;
using Pasb.Model;
using Pasb.Ui;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;
using AcDoc = Autodesk.AutoCAD.ApplicationServices.Document;
using AsId = Autodesk.AdvanceSteel.CADLink.Database.ObjectId;

[assembly: CommandClass(typeof(Pasb.Diagnostics.AdvanceSteel.MaterialTableCommand))]

namespace Pasb.Diagnostics.AdvanceSteel;

/// <summary>
/// PASB_LISTA_MATERIAL: janela com as tabelas a inserir (detalhada, resumo de perfis, parafusos, chumbadores, chapas);
/// lê o modelo pelo caminho validado (sem alterá-lo) e insere as tabelas do AutoCAD lado a lado a partir do ponto clicado.
/// É o único comando do PASB que grava no desenho, e só grava as tabelas (Ctrl+Z desfaz).
/// </summary>
public sealed class MaterialTableCommand
{
    [CommandMethod("PASB", "PASB_LISTA_MATERIAL", CommandFlags.Modal)]
    public void Insert()
    {
        AcDoc? doc = AcApp.DocumentManager.MdiActiveDocument;
        if (doc == null) return;
        Editor ed = doc.Editor;
        try
        {
            // 1) Opções
            bool model = doc.Database.TileMode;
            double suggested = 2.5 * (model ? AnnoScale() : 1.0);
            MaterialListSettings settings = MaterialListSettings.Load();
            double h;
            using (var dlg = new MaterialListDialog(settings, suggested, Version))
            {
                if (Autodesk.AutoCAD.ApplicationServices.Application.ShowModalDialog(dlg) != DialogResult.OK) { ed.WriteMessage("\nPASB: cancelado.\n"); return; }
                h = dlg.ChosenTextHeight;
            }
            settings.Save();
            var kinds = new List<MaterialTableKind>();
            if (settings.Detailed) kinds.Add(MaterialTableKind.Detailed);
            if (settings.ProfileSummary) kinds.Add(MaterialTableKind.ProfileSummary);
            if (settings.Plates) kinds.Add(MaterialTableKind.Plates);
            if (settings.Bolts) kinds.Add(MaterialTableKind.Bolts);
            if (settings.Anchors) kinds.Add(MaterialTableKind.Anchors);

            // 2) Leitura (transação descartada, DBMOD conferido) — antes de qualquer gravação.
            var (dir, safe) = Commands.Run(ed, "PASB_LISTA_MATERIAL", () =>
            {
                DatabaseManager.GetModelObjectIds(out AsId[] ids);
                return (ids ?? Array.Empty<AsId>(), 0);
            }, Pasb.Diagnostics.Core.ReadProfile.Export);
            if (!safe)
            {
                ed.WriteMessage("\nPASB: lista cancelada: a verificação de integridade da leitura falhou. Feche o desenho SEM SALVAR.\n");
                return;
            }
            Extraction x = DiagnosticImporter.ImportDirectory(dir);
            string docName = Path.GetFileName(doc.Name);
            var tables = new List<MaterialTable>();
            foreach (MaterialTableKind k in kinds)
            {
                MaterialTable t = MaterialTables.Build(x, k, docName);
                if (t.IsEmpty) ed.WriteMessage($"\nPASB: {MaterialTables.KindName(k)}: nada no modelo; tabela não inserida.");
                else tables.Add(t);
            }
            if (tables.Count == 0) { ed.WriteMessage("\nPASB: nenhuma tabela com conteúdo para inserir.\n"); return; }

            // 3) Ponto e gravação (só as tabelas, no espaço atual, lado a lado)
            PromptPointResult pr = ed.GetPoint(new PromptPointOptions($"\nPASB: clique no ponto de inserção ({tables.Count} tabela(s), canto superior esquerdo): "));
            if (pr.Status != PromptStatus.OK) { ed.WriteMessage("\nPASB: cancelado.\n"); return; }
            Point3d at = pr.Value.TransformBy(ed.CurrentUserCoordinateSystem);
            Vector3d xDir = Vector3d.XAxis.TransformBy(ed.CurrentUserCoordinateSystem).GetNormal();

            using (doc.LockDocument())
            using (Autodesk.AutoCAD.DatabaseServices.Transaction tr = doc.Database.TransactionManager.StartTransaction())
            {
                var space = (BlockTableRecord)tr.GetObject(doc.Database.CurrentSpaceId, OpenMode.ForWrite);
                Point3d p = at;
                foreach (MaterialTable mt in tables)
                {
                    Table t = BuildTable(doc.Database, mt, h);
                    t.Position = p;
                    space.AppendEntity(t);
                    tr.AddNewlyCreatedDBObject(t, true);
                    t.GenerateLayout();
                    p += xDir * (mt.Columns.Sum(c => c.WidthFactor) * h + 4 * h);
                }
                tr.Commit();
            }
            ed.WriteMessage($"\nPASB: {tables.Count} tabela(s) inserida(s): {string.Join(", ", tables.Select(t => MaterialTables.KindName(t.Kind)))}. (Ctrl+Z desfaz.)\n");
        }
        catch (System.Exception ex)
        {
            ed.WriteMessage($"\nPASB: PASB_LISTA_MATERIAL falhou: {ex.GetType().Name}: {ex.Message}\n");
        }
    }

    private static Table BuildTable(Database db, MaterialTable mt, double h)
    {
        int cols = mt.Columns.Count;
        int rows = 2 + mt.Rows.Count + (mt.Total != null ? 1 : 0) + mt.Notes.Count;   // título, cabeçalho, itens, total, notas
        var t = new Table { TableStyle = db.Tablestyle };
        t.SetSize(rows, cols);
        for (int c = 0; c < cols; c++) t.Columns[c].Width = mt.Columns[c].WidthFactor * h;
        for (int r = 0; r < rows; r++) t.Rows[r].Height = h * 2.0;

        void Set(int r, int c, string text, CellAlignment al)
        {
            Cell cell = t.Cells[r, c];
            cell.TextString = text;
            cell.TextHeight = h;
            cell.Alignment = al;
        }
        CellAlignment Al(int c) => mt.Columns[c].Numeric ? CellAlignment.MiddleRight : (c == 0 ? CellAlignment.MiddleCenter : CellAlignment.MiddleLeft);

        Merge(t, 0, 0, cols - 1);
        Set(0, 0, mt.Title, CellAlignment.MiddleCenter);
        t.Cells[0, 0].TextHeight = h * 1.4;
        t.Rows[0].Height = h * 2.6;
        for (int c = 0; c < cols; c++) Set(1, c, mt.Columns[c].Header, CellAlignment.MiddleCenter);

        int row = 2;
        foreach (string[] r in mt.Rows)
        {
            for (int c = 0; c < cols; c++) Set(row, c, r[c], Al(c));
            row++;
        }
        if (mt.Total != null)
        {
            for (int c = 0; c < cols; c++) Set(row, c, mt.Total[c], c == 0 ? CellAlignment.MiddleLeft : Al(c));
            row++;
        }
        foreach (string note in mt.Notes)
        {
            Merge(t, row, 0, cols - 1);
            Set(row, 0, "Nota: " + note, CellAlignment.MiddleLeft);
            t.Cells[row, 0].TextHeight = h * 0.8;
            row++;
        }
        return t;
    }

    private static void Merge(Table t, int row, int c0, int c1)
    {
        try { t.MergeCells(CellRange.Create(t, row, c0, row, c1)); } catch (System.Exception) { /* já mesclada pelo estilo */ }
    }

    /// <summary>Escala de anotação atual (1:20 → 20). Sem informação válida, 1.</summary>
    private static double AnnoScale()
    {
        try
        {
            double s = Convert.ToDouble(AcApp.GetSystemVariable("CANNOSCALEVALUE"));
            return s > 0 && s <= 1 ? 1.0 / s : 1.0;
        }
        catch (System.Exception) { return 1.0; }
    }

    private static string Version =>
        typeof(MaterialTableCommand).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion?.Split('+')[0] ?? "";
}
