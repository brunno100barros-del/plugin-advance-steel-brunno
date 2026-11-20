using System;
using System.IO;
using System.Linq;
using Autodesk.AdvanceSteel.CADAccess;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Pasb.Connector;
using Pasb.Model;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;
using AcDoc = Autodesk.AutoCAD.ApplicationServices.Document;
using AsId = Autodesk.AdvanceSteel.CADLink.Database.ObjectId;

[assembly: CommandClass(typeof(Pasb.Diagnostics.AdvanceSteel.MaterialTableCommand))]

namespace Pasb.Diagnostics.AdvanceSteel;

/// <summary>
/// PASB_LISTA_MATERIAL: lê o modelo pelo caminho validado (sem alterá-lo) e, depois, insere UMA tabela do AutoCAD
/// com a lista de material no ponto que o usuário clicar. É o único comando do PASB que grava no desenho,
/// e só grava a tabela (pode ser desfeita com Ctrl+Z ou apagada).
/// </summary>
public sealed class MaterialTableCommand
{
    private static readonly string[] Headers =
        { "Item", "Tipo", "Descrição", "Material", "Compr. (mm)", "Qtde", "Peso unit. (kg)", "Peso total (kg)", "Pintura (m²)" };

    /// <summary>Largura das colunas em múltiplos da altura do texto.</summary>
    private static readonly double[] Widths = { 5, 8, 14, 10, 10, 6, 11, 11, 10 };

    [CommandMethod("PASB", "PASB_LISTA_MATERIAL", CommandFlags.Modal)]
    public void Insert()
    {
        AcDoc? doc = AcApp.DocumentManager.MdiActiveDocument;
        if (doc == null) return;
        Editor ed = doc.Editor;
        try
        {
            // 1) Leitura (transação descartada, DBMOD conferido) — antes de qualquer gravação.
            var (dir, safe) = Commands.Run(ed, "PASB_LISTA_MATERIAL", () =>
            {
                DatabaseManager.GetModelObjectIds(out AsId[] ids);
                return (ids ?? Array.Empty<AsId>(), 0);
            });
            if (!safe)
            {
                ed.WriteMessage("\nPASB: lista cancelada: a verificação de integridade da leitura falhou. Feche o desenho SEM SALVAR.\n");
                return;
            }
            Extraction x = DiagnosticImporter.ImportDirectory(dir);
            MaterialListResult m = MaterialList.Build(x);
            if (m.Rows.Count == 0) { ed.WriteMessage("\nPASB: nenhum perfil, chapa ou parafuso encontrado no modelo.\n"); return; }

            // 2) Altura do texto: 2,5 mm no papel, convertida pela escala de anotação no espaço modelo.
            bool model = doc.Database.TileMode || doc.Database.CurrentSpaceId == SymbolUtilityServices.GetBlockModelSpaceId(doc.Database);
            double scale = model ? AnnoScale() : 1.0;
            var ho = new PromptDoubleOptions($"\nPASB: altura do texto da tabela <{scale * 2.5:0.##}>: ")
            { AllowNegative = false, AllowZero = false, AllowNone = true, DefaultValue = scale * 2.5, UseDefaultValue = true };
            PromptDoubleResult hr = ed.GetDouble(ho);
            if (hr.Status != PromptStatus.OK && hr.Status != PromptStatus.None) { ed.WriteMessage("\nPASB: cancelado.\n"); return; }
            double h = hr.Status == PromptStatus.OK ? hr.Value : scale * 2.5;

            PromptPointResult pr = ed.GetPoint(new PromptPointOptions("\nPASB: clique no ponto de inserção da tabela (canto superior esquerdo): "));
            if (pr.Status != PromptStatus.OK) { ed.WriteMessage("\nPASB: cancelado.\n"); return; }
            Point3d at = pr.Value.TransformBy(ed.CurrentUserCoordinateSystem);

            // 3) Gravação: só a tabela, no espaço atual.
            using (doc.LockDocument())
            using (Autodesk.AutoCAD.DatabaseServices.Transaction tr = doc.Database.TransactionManager.StartTransaction())
            {
                var space = (BlockTableRecord)tr.GetObject(doc.Database.CurrentSpaceId, OpenMode.ForWrite);
                Table t = BuildTable(m, Path.GetFileName(doc.Name), h);
                t.Position = at;
                space.AppendEntity(t);
                tr.AddNewlyCreatedDBObject(t, true);
                t.GenerateLayout();
                tr.Commit();
            }
            ed.WriteMessage($"\nPASB: tabela inserida: {m.Rows.Count} item(ns), {MaterialList.Fmt(m.TotalKg, 2)} kg. (Ctrl+Z desfaz.)\n");
        }
        catch (System.Exception ex)
        {
            ed.WriteMessage($"\nPASB: PASB_LISTA_MATERIAL falhou: {ex.GetType().Name}: {ex.Message}\n");
        }
    }

    private static Table BuildTable(MaterialListResult m, string docName, double h)
    {
        int cols = Headers.Length;
        int rows = 2 + m.Rows.Count + 1 + m.Notes.Count;   // título, cabeçalho, itens, total, notas
        var t = new Table();
        t.TableStyle = HostApplicationServices.WorkingDatabase.Tablestyle;
        t.SetSize(rows, cols);
        for (int c = 0; c < cols; c++) t.Columns[c].Width = Widths[c] * h;
        for (int r = 0; r < rows; r++) t.Rows[r].Height = h * 2.0;

        void Set(int r, int c, string text, CellAlignment al = CellAlignment.MiddleCenter)
        {
            Cell cell = t.Cells[r, c];
            cell.TextString = text;
            cell.TextHeight = h;
            cell.Alignment = al;
        }

        Merge(t, 0, 0, cols - 1);
        Set(0, 0, "LISTA DE MATERIAL — " + docName);
        t.Cells[0, 0].TextHeight = h * 1.4;
        t.Rows[0].Height = h * 2.6;
        for (int c = 0; c < cols; c++) Set(1, c, Headers[c]);

        int row = 2;
        foreach (MaterialRow r in m.Rows)
        {
            Set(row, 0, r.Item.ToString());
            Set(row, 1, r.Kind);
            Set(row, 2, r.Description, CellAlignment.MiddleLeft);
            Set(row, 3, r.Material);
            Set(row, 4, MaterialList.Fmt(r.LengthMm, 1), CellAlignment.MiddleRight);
            Set(row, 5, r.Quantity.ToString());
            Set(row, 6, MaterialList.Fmt(r.UnitKg, 2), CellAlignment.MiddleRight);
            Set(row, 7, MaterialList.Fmt(r.TotalKg, 2), CellAlignment.MiddleRight);
            Set(row, 8, MaterialList.Fmt(r.TotalPaintM2, 3), CellAlignment.MiddleRight);
            row++;
        }

        Merge(t, row, 0, 3);
        Set(row, 0, "TOTAL", CellAlignment.MiddleRight);
        Set(row, 4, MaterialList.Fmt(m.TotalBeamLengthMm, 1), CellAlignment.MiddleRight);
        Set(row, 5, m.Rows.Sum(r => r.Quantity).ToString());
        Set(row, 7, MaterialList.Fmt(m.TotalKg, 2), CellAlignment.MiddleRight);
        Set(row, 8, MaterialList.Fmt(m.TotalPaintM2, 3), CellAlignment.MiddleRight);
        row++;

        foreach (string note in m.Notes)
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
            object v = AcApp.GetSystemVariable("CANNOSCALEVALUE");
            double s = Convert.ToDouble(v);
            return s > 0 && s <= 1 ? 1.0 / s : 1.0;
        }
        catch (System.Exception) { return 1.0; }
    }
}
