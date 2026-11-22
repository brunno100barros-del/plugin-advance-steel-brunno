using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using Autodesk.AdvanceSteel.CADAccess;
using Autodesk.AdvanceSteel.ConstructionTypes;
using Autodesk.AdvanceSteel.DocumentManagement;
using Autodesk.AdvanceSteel.Modelling;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using Pasb.Connector;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;
using AcColor = Autodesk.AutoCAD.Colors.Color;
using AcColorMethod = Autodesk.AutoCAD.Colors.ColorMethod;
using AcDoc = Autodesk.AutoCAD.ApplicationServices.Document;
using AcPoint = Autodesk.AutoCAD.Geometry.Point3d;
using AcTransaction = Autodesk.AutoCAD.DatabaseServices.Transaction;
using AcVector = Autodesk.AutoCAD.Geometry.Vector3d;
using AsMatrix = Autodesk.AdvanceSteel.Geometry.Matrix3d;
using AsPlane = Autodesk.AdvanceSteel.Geometry.Plane;
using AsPoint = Autodesk.AdvanceSteel.Geometry.Point3d;
using AsVector = Autodesk.AdvanceSteel.Geometry.Vector3d;

[assembly: CommandClass(typeof(Pasb.Diagnostics.AdvanceSteel.BracingCommand))]

namespace Pasb.Diagnostics.AdvanceSteel;

/// <summary>
/// Macros de contraventamento (docs/BRACING.md).
/// PASB_CONTRAVENTAMENTO: 2 pontos (cantos opostos) → janela de parâmetros → cria barras, chapas, parafusos, soldas,
/// porcas e arruelas (blocos próprios do PASB) no plano XY, na cota Z do primeiro ponto.
/// PASB_EDITAR_CONTRAVENTAMENTO: seleciona uma peça de um contraventamento PASB → reabre a janela com os parâmetros
/// guardados → apaga as peças antigas e gera de novo. Os parâmetros ficam no dicionário do desenho.
/// </summary>
public sealed class BracingCommand
{
    private const string RegApp = "PASB_CV";
    private const string DictName = "PASB_CONTRAVENTAMENTOS";

    [CommandMethod("PASB", "PASB_CONTRAVENTAMENTO", CommandFlags.Modal)]
    public void Create()
    {
        AcDoc? doc = AcApp.DocumentManager.MdiActiveDocument;
        if (doc == null) return;
        Editor ed = doc.Editor;
        try
        {
            if (!CheckUnits(doc, ed)) return;
            // Macro nativa registrada (Registrar-Macros-PASB.cmd): ligação de verdade, com caixa cinza e duplo clique.
            if (NativeJointInstalled())
            {
                JointsMenuCommand.StartJoint(doc, "PasbContraventamento");
                return;
            }
            ed.WriteMessage("\nPASB: a macro nativa não está registrada (rode Registrar-Macros-PASB.cmd com o Advance Steel fechado). Usando a versão simples, sem caixa de ligação.\n");
            PromptPointResult r1 = ed.GetPoint(new PromptPointOptions("\nPASB: ponto 1 (canto do contraventamento): "));
            if (r1.Status != PromptStatus.OK) return;
            PromptPointResult r2 = ed.GetCorner(new PromptCornerOptions("\nPASB: ponto 2 (canto oposto): ", r1.Value));
            if (r2.Status != PromptStatus.OK) return;
            V3 p1 = ToV3(r1.Value.TransformBy(ed.CurrentUserCoordinateSystem));
            V3 p2 = ToV3(r2.Value.TransformBy(ed.CurrentUserCoordinateSystem));

            BracingParams prm = BracingSettings.Load();
            BracingResult res;
            using (var dlg = new BracingDialog(p1, p2, prm, Version))
            {
                if (Autodesk.AutoCAD.ApplicationServices.Application.ShowModalDialog(dlg) != DialogResult.OK) { ed.WriteMessage("\nPASB: cancelado.\n"); return; }
                res = dlg.Result;
            }
            BracingSettings.Save(prm);
            string id = Guid.NewGuid().ToString("N");
            var (handles, warnings) = Generate(doc, res, prm, id);
            if (handles.Count == 0) { ed.WriteMessage("\nPASB: nada foi criado.\n"); return; }
            SaveRecord(doc, id, p1, p2, prm, handles);
            Report(ed, "criado", handles.Count, warnings);
        }
        catch (System.Exception ex)
        {
            ed.WriteMessage($"\nPASB: PASB_CONTRAVENTAMENTO falhou: {ex.GetType().Name}: {ex.Message}\n");
        }
    }

    [CommandMethod("PASB", "PASB_EDITAR_CONTRAVENTAMENTO", CommandFlags.Modal | CommandFlags.UsePickSet | CommandFlags.Redraw)]
    public void Edit()
    {
        AcDoc? doc = AcApp.DocumentManager.MdiActiveDocument;
        if (doc == null) return;
        Editor ed = doc.Editor;
        try
        {
            if (!CheckUnits(doc, ed)) return;
            // 1) peça já selecionada (clique antes do comando ou menu do botão direito); 2) senão, pedir uma peça.
            string? id = null;
            PromptSelectionResult implied = ed.SelectImplied();
            if (implied.Status == PromptStatus.OK)
                foreach (ObjectId oid in implied.Value.GetObjectIds()) { id = FindBracingId(doc, oid); if (id != null) break; }
            if (id == null)
            {
                var opt = new PromptEntityOptions("\nPASB: selecione uma peça do contraventamento: ");
                PromptEntityResult pick = ed.GetEntity(opt);
                if (pick.Status != PromptStatus.OK) return;
                id = FindBracingId(doc, pick.ObjectId);
            }
            ed.SetImpliedSelection(Array.Empty<ObjectId>());
            if (id == null || LoadRecord(doc, id) is not { } rec)
            {
                int n = CountRecords(doc);
                ed.WriteMessage(n == 0
                    ? "\nPASB: este desenho não tem contraventamento gerado pelo PASB (gere com PASB > Contraventamento em X).\n"
                    : $"\nPASB: essa peça não pertence a nenhum dos {n} contraventamento(s) PASB deste desenho. Clique numa barra, chapa ou no esticador.\n");
                return;
            }

            BracingParams prm = rec.prm;
            BracingResult res;
            using (var dlg = new BracingDialog(rec.p1, rec.p2, prm, Version, editing: true))
            {
                if (Autodesk.AutoCAD.ApplicationServices.Application.ShowModalDialog(dlg) != DialogResult.OK) { ed.WriteMessage("\nPASB: edição cancelada; nada mudou.\n"); return; }
                res = dlg.Result;
            }
            BracingSettings.Save(prm);
            int erased = EraseParts(doc, rec.handles);
            var (handles, warnings) = Generate(doc, res, prm, id);
            SaveRecord(doc, id, rec.p1, rec.p2, prm, handles);
            ed.WriteMessage($"\nPASB: {erased} peça(s) antiga(s) apagada(s).");
            Report(ed, "regerado", handles.Count, warnings);
        }
        catch (System.Exception ex)
        {
            ed.WriteMessage($"\nPASB: PASB_EDITAR_CONTRAVENTAMENTO falhou: {ex.GetType().Name}: {ex.Message}\n");
        }
    }

    // ------------------------------------------------------------------ geração

    /// <summary>Cria as peças do Advance Steel. Se alguma barra falhar (ex.: perfil inexistente), nada é gravado.</summary>
    private static (List<string> handles, List<string> warnings) Generate(AcDoc doc, BracingResult res, BracingParams p, string id)
    {
        var warnings = new List<string>();
        var handles = new List<string>();
        string nutBlock = "", washerBlock = "";
        if (p.NutsAndWashers)
        {
            (nutBlock, washerBlock) = EnsureBlocks(doc, p);
        }

        var made = new FilerObject?[res.Parts.Count];
        using (doc.LockDocument())
        using (var access = new DocumentAccess(null, false))
        {
            bool abort = false;
            for (int i = 0; i < res.Parts.Count && !abort; i++)
            {
                BracingPart part = res.Parts[i];
                try
                {
                    made[i] = part.Kind switch
                    {
                        BracingPartKind.Rod or BracingPartKind.ThreadedRod or BracingPartKind.SpliceBar or BracingPartKind.Cleat => MakeBar(part),
                        BracingPartKind.Gusset or BracingPartKind.BasePlate or BracingPartKind.EndPlate => MakePlate(part),
                        BracingPartKind.Nut => MakeSpecial(part, nutBlock),
                        BracingPartKind.Washer => MakeSpecial(part, washerBlock),
                        _ => null,   // parafusos e soldas depois, quando as peças que eles ligam já existem
                    };
                }
                catch (System.Exception ex)
                {
                    string what = $"{part.Role} (diagonal {part.Diagonal}): {ex.Message}";
                    if (part.Kind is BracingPartKind.Rod or BracingPartKind.ThreadedRod or BracingPartKind.Cleat or BracingPartKind.Gusset)
                    {
                        warnings.Add("FALHA, nada foi gravado: " + what + (part.Profile.Length > 0 ? $" (perfil \"{part.Profile}\" existe no catálogo?)" : ""));
                        abort = true;
                    }
                    else warnings.Add("não criado: " + what);
                }
            }
            if (abort) { access.DiscardAllChanges(); return (new List<string>(), warnings); }

            for (int i = 0; i < res.Parts.Count; i++)
            {
                BracingPart part = res.Parts[i];
                if (part.Kind is not (BracingPartKind.Bolt or BracingPartKind.Weld)) continue;
                FilerObject[] objs = part.Connects.Select(k => made[k]).Where(o => o != null).Cast<FilerObject>().ToArray();
                if (objs.Length < 2) { warnings.Add($"{part.Role} (diagonal {part.Diagonal}) sem as peças que liga; não criado."); continue; }
                try { made[i] = part.Kind == BracingPartKind.Bolt ? MakeBolt(part, objs, p) : MakeWeld(part, objs); }
                catch (System.Exception ex) { warnings.Add($"{part.Role} (diagonal {part.Diagonal}) não criado: {ex.Message}"); }
            }
            access.Commit();
        }
        foreach (FilerObject? o in made)
        {
            if (o == null) continue;
            try { handles.Add(o.Handle); } catch (System.Exception) { }
        }
        TagEntities(doc, handles, id);
        return (handles, warnings);
    }

    private static FilerObject MakeBar(BracingPart part)
    {
        var beam = new StraightBeam(part.Profile, P(part.Start), P(part.End), AsVector.kZAxis);
        beam.WriteToDb();
        beam.Material = part.Material;
        beam.Role = part.Role;
        return beam;
    }

    private static FilerObject MakePlate(BracingPart part)
    {
        var plane = new AsPlane(P(part.Polygon[0]), Vec(part.Normal));
        var plate = new Plate(plane, part.Polygon.Select(P).ToArray(), part.Thickness);
        plate.WriteToDb();
        plate.Portioning = 1;            // chapa do lado -normal do plano (como no exemplo analisado)
        plate.Material = part.Material;
        plate.Role = part.Role;
        return plate;
    }

    private static FilerObject MakeBolt(BracingPart part, FilerObject[] objs, BracingParams p)
    {
        V3 n = V3.Cross(new V3(0, 0, 1), part.Axis).Unit;
        var bolt = new FinitRectScrewBoltPattern(P(part.Origin), P(part.Origin), Vec(part.Axis), Vec(n));
        bolt.WriteToDb();
        bolt.Nx = 1; bolt.Ny = 1;
        bolt.ScrewDiameter = p.BoltDiameter;
        if (p.BoltStandard.Length > 0) bolt.Standard = p.BoltStandard;
        if (p.BoltGrade.Length > 0) bolt.Grade = p.BoltGrade;
        bolt.Connect(objs, AtomicElement.eAssemblyLocation.kOnSite);
        return bolt;
    }

    private static FilerObject MakeWeld(BracingPart part, FilerObject[] objs)
    {
        V3 x = part.Axis.Unit;
        V3 y = Math.Abs(x.Z) > 0.9 ? new V3(1, 0, 0) : V3.Cross(new V3(0, 0, 1), x).Unit;
        var weld = new WeldPoint(P(part.Origin), Vec(x), Vec(y));
        weld.WriteToDb();
        weld.Thickness = part.Size;
        weld.Connect(objs, AtomicElement.eAssemblyLocation.kInShop);
        return weld;
    }

    private static FilerObject MakeSpecial(BracingPart part, string block)
    {
        V3 z = part.Axis.Unit;
        V3 x = Math.Abs(z.Z) > 0.9 ? new V3(1, 0, 0) : V3.Cross(new V3(0, 0, 1), z).Unit;
        V3 y = V3.Cross(z, x);
        var cs = new AsMatrix();
        cs.SetCoordSystem(P(part.Origin), Vec(x), Vec(y), Vec(z));
        var sp = new SpecialPart(cs);
        sp.SetBlock(block, 1.0);
        sp.WriteToDb();
        sp.Role = part.Role;
        return sp;
    }

    // ------------------------------------------------------------------ blocos de porca e arruela (próprios do PASB)

    private static (string nut, string washer) EnsureBlocks(AcDoc doc, BracingParams p)
    {
        string f(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);
        string nut = $"PASB_PORCA_S{f(p.NutAcrossFlats)}_H{f(p.NutHeight)}_D{f(p.WasherInnerDiameter)}";
        string washer = $"PASB_ARRUELA_D{f(p.WasherOuterDiameter)}_d{f(p.WasherInnerDiameter)}_t{f(p.WasherThickness)}";
        using (doc.LockDocument())
        using (AcTransaction tr = doc.Database.TransactionManager.StartTransaction())
        {
            var bt = (BlockTable)tr.GetObject(doc.Database.BlockTableId, OpenMode.ForRead);
            if (!bt.Has(nut)) DefineBlock(tr, bt, nut, NutSolid(p.NutAcrossFlats, p.NutHeight, p.WasherInnerDiameter / 2));
            if (!bt.Has(washer)) DefineBlock(tr, bt, washer, RingSolid(p.WasherOuterDiameter / 2, p.WasherInnerDiameter / 2, p.WasherThickness));
            tr.Commit();
        }
        return (nut, washer);
    }

    private static void DefineBlock(AcTransaction tr, BlockTable bt, string name, Solid3d solid)
    {
        bt.UpgradeOpen();
        var btr = new BlockTableRecord { Name = name, Origin = AcPoint.Origin };
        bt.Add(btr);
        tr.AddNewlyCreatedDBObject(btr, true);
        solid.Layer = "0";
        solid.Color = AcColor.FromColorIndex(AcColorMethod.ByBlock, 0);
        btr.AppendEntity(solid);
        tr.AddNewlyCreatedDBObject(solid, true);
    }

    /// <summary>Porca sextavada (entre faces <paramref name="s"/>, altura <paramref name="h"/>) com furo, centrada na origem, eixo Z.</summary>
    private static Solid3d NutSolid(double s, double h, double holeR)
    {
        double rc = s / Math.Sqrt(3);   // raio do círculo circunscrito
        var pts = new Autodesk.AutoCAD.Geometry.Point3dCollection();
        for (int i = 0; i < 6; i++) { double a = Math.PI / 6 + i * Math.PI / 3; pts.Add(new AcPoint(rc * Math.Cos(a), rc * Math.Sin(a), -h / 2)); }
        using var pl = new Polyline3d(Poly3dType.SimplePoly, pts, true);
        using DBObjectCollection regions = Region.CreateFromCurves(new DBObjectCollection { pl });
        var nut = new Solid3d();
        using (var reg = (Region)regions[0]) nut.Extrude(reg, h, 0);
        using var hole = new Solid3d();
        hole.CreateFrustum(h * 1.2, holeR, holeR, holeR);
        nut.BooleanOperation(BooleanOperationType.BoolSubtract, hole);
        return nut;
    }

    /// <summary>Arruela (anel) centrada na origem, eixo Z.</summary>
    private static Solid3d RingSolid(double rOut, double rIn, double t)
    {
        var ring = new Solid3d();
        ring.CreateFrustum(t, rOut, rOut, rOut);
        using var hole = new Solid3d();
        hole.CreateFrustum(t * 1.2, rIn, rIn, rIn);
        ring.BooleanOperation(BooleanOperationType.BoolSubtract, hole);
        return ring;
    }

    // ------------------------------------------------------------------ parâmetros guardados no desenho

    private static void TagEntities(AcDoc doc, List<string> handles, string id)
    {
        using (doc.LockDocument())
        using (AcTransaction tr = doc.Database.TransactionManager.StartTransaction())
        {
            EnsureRegApp(tr, doc.Database);
            foreach (string h in handles)
            {
                if (!TryId(doc.Database, h, out ObjectId oid)) continue;
                try
                {
                    var ent = (Entity)tr.GetObject(oid, OpenMode.ForWrite);
                    ent.XData = new ResultBuffer(new TypedValue((int)DxfCode.ExtendedDataRegAppName, RegApp), new TypedValue((int)DxfCode.ExtendedDataAsciiString, id));
                }
                catch (System.Exception) { }
            }
            tr.Commit();
        }
    }

    private static string? ReadTag(AcDoc doc, ObjectId oid)
    {
        try
        {
            using AcTransaction tr = doc.Database.TransactionManager.StartTransaction();
            var ent = (Entity)tr.GetObject(oid, OpenMode.ForRead);
            using ResultBuffer? rb = ent.GetXDataForApplication(RegApp);
            return rb?.AsArray().Skip(1).Select(v => v.Value as string).FirstOrDefault(s => !string.IsNullOrEmpty(s));
        }
        catch (System.Exception) { return null; }
    }

    /// <summary>
    /// Contraventamento ao qual a peça pertence: pela marca XData e, se o Advance Steel não a guardou, procurando o
    /// handle da peça na lista de peças de cada contraventamento salvo no desenho.
    /// </summary>
    private static string? FindBracingId(AcDoc doc, ObjectId oid)
    {
        string? tag = ReadTag(doc, oid);
        if (tag != null && LoadRecord(doc, tag) != null) return tag;
        long h = oid.Handle.Value;
        foreach (var (id, handles) in AllRecords(doc))
            if (handles.Any(s => ParseHandle(s) == h)) return id;
        return null;
    }

    private static int CountRecords(AcDoc doc) => AllRecords(doc).Count;

    private static List<(string id, List<string> handles)> AllRecords(AcDoc doc)
    {
        var list = new List<(string, List<string>)>();
        try
        {
            using AcTransaction tr = doc.Database.TransactionManager.StartTransaction();
            var nod = (DBDictionary)tr.GetObject(doc.Database.NamedObjectsDictionaryId, OpenMode.ForRead);
            if (!nod.Contains(DictName)) return list;
            var dict = (DBDictionary)tr.GetObject(nod.GetAt(DictName), OpenMode.ForRead);
            foreach (DBDictionaryEntry e in dict)
            {
                if (tr.GetObject(e.Value, OpenMode.ForRead) is not Xrecord xr || xr.Data == null) continue;
                var handles = new List<string>(); bool inHandles = false;
                foreach (TypedValue t in xr.Data.AsArray())
                {
                    if (t.Value is not string s) continue;
                    if (s == "##HANDLES##") { inHandles = true; continue; }
                    if (inHandles) handles.Add(s);
                }
                list.Add((e.Key, handles));
            }
        }
        catch (System.Exception) { }
        return list;
    }

    private static long ParseHandle(string s)
    {
        try { return Convert.ToInt64(s.Trim(), 16); } catch (System.Exception) { return -1; }
    }

    // ------------------------------------------------------------------ menu do botão direito

    private static Autodesk.AutoCAD.Windows.ContextMenuExtension? _menu;

    /// <summary>Acrescenta "PASB: Editar contraventamento" ao menu do botão direito de qualquer objeto selecionado. Nunca lança.</summary>
    public static void RegisterContextMenu()
    {
        try
        {
            if (_menu != null) return;
            _menu = new Autodesk.AutoCAD.Windows.ContextMenuExtension { Title = "PASB" };
            var item = new Autodesk.AutoCAD.Windows.MenuItem("PASB: Editar contraventamento");
            item.Click += (_, _) => AcApp.DocumentManager.MdiActiveDocument?.SendStringToExecute("_PASB_EDITAR_CONTRAVENTAMENTO ", true, false, false);
            _menu.MenuItems.Add(item);
            Autodesk.AutoCAD.ApplicationServices.Application.AddObjectContextMenuExtension(RXObject.GetClass(typeof(Entity)), _menu);
        }
        catch (System.Exception ex) { StartupLog.Write("Menu do contraventamento falhou: " + ex); }
    }

    private static void SaveRecord(AcDoc doc, string id, V3 p1, V3 p2, BracingParams prm, List<string> handles)
    {
        using (doc.LockDocument())
        using (AcTransaction tr = doc.Database.TransactionManager.StartTransaction())
        {
            var nod = (DBDictionary)tr.GetObject(doc.Database.NamedObjectsDictionaryId, OpenMode.ForWrite);
            DBDictionary dict;
            if (nod.Contains(DictName)) dict = (DBDictionary)tr.GetObject(nod.GetAt(DictName), OpenMode.ForWrite);
            else { dict = new DBDictionary(); nod.SetAt(DictName, dict); tr.AddNewlyCreatedDBObject(dict, true); }
            var values = new List<TypedValue>
            {
                new((int)DxfCode.Int16, (short)1),
                new((int)DxfCode.XCoordinate, new AcPoint(p1.X, p1.Y, p1.Z)),
                new((int)DxfCode.XCoordinate + 1, new AcPoint(p2.X, p2.Y, p2.Z)),
            };
            string json = prm.ToJson();
            for (int i = 0; i < json.Length; i += 1000) values.Add(new TypedValue((int)DxfCode.Text, json.Substring(i, Math.Min(1000, json.Length - i))));
            values.Add(new TypedValue((int)DxfCode.Text, "##HANDLES##"));
            foreach (string h in handles) values.Add(new TypedValue((int)DxfCode.Text, h));
            var xr = new Xrecord { Data = new ResultBuffer(values.ToArray()) };
            if (dict.Contains(id)) { var old = tr.GetObject(dict.GetAt(id), OpenMode.ForWrite); old.Erase(); dict.Remove(id); }
            dict.SetAt(id, xr);
            tr.AddNewlyCreatedDBObject(xr, true);
            tr.Commit();
        }
    }

    private static (V3 p1, V3 p2, BracingParams prm, List<string> handles)? LoadRecord(AcDoc doc, string id)
    {
        using AcTransaction tr = doc.Database.TransactionManager.StartTransaction();
        var nod = (DBDictionary)tr.GetObject(doc.Database.NamedObjectsDictionaryId, OpenMode.ForRead);
        if (!nod.Contains(DictName)) return null;
        var dict = (DBDictionary)tr.GetObject(nod.GetAt(DictName), OpenMode.ForRead);
        if (!dict.Contains(id)) return null;
        var xr = (Xrecord)tr.GetObject(dict.GetAt(id), OpenMode.ForRead);
        TypedValue[] v = xr.Data.AsArray();
        var p1 = (AcPoint)v[1].Value; var p2 = (AcPoint)v[2].Value;
        var json = new StringBuilder(); var handles = new List<string>(); bool inHandles = false;
        foreach (TypedValue t in v.Skip(3))
        {
            string s = (string)t.Value;
            if (s == "##HANDLES##") { inHandles = true; continue; }
            if (inHandles) handles.Add(s); else json.Append(s);
        }
        return (ToV3(p1), ToV3(p2), BracingParams.FromJson(json.ToString()), handles);
    }

    private static int EraseParts(AcDoc doc, List<string> handles)
    {
        int n = 0;
        using (doc.LockDocument())
        using (AcTransaction tr = doc.Database.TransactionManager.StartTransaction())
        {
            // Soldas e parafusos primeiro (dependem das outras peças).
            var ents = new List<(int order, ObjectId id)>();
            foreach (string h in handles)
                if (TryId(doc.Database, h, out ObjectId oid) && !oid.IsErased)
                {
                    string dxf = oid.ObjectClass?.DxfName ?? "";
                    ents.Add((dxf.Contains("WELD", StringComparison.OrdinalIgnoreCase) || dxf.Contains("BOLT", StringComparison.OrdinalIgnoreCase) ? 0 : 1, oid));
                }
            foreach (var (_, oid) in ents.OrderBy(e => e.order))
            {
                try { var ent = tr.GetObject(oid, OpenMode.ForWrite, false); if (!ent.IsErased) { ent.Erase(); n++; } } catch (System.Exception) { }
            }
            tr.Commit();
        }
        return n;
    }

    // ------------------------------------------------------------------ utilitários

    /// <summary>PasbJoints.dll está na pasta do Advance Steel (onde ele carrega as ligações registradas).</summary>
    private static bool NativeJointInstalled()
    {
        try
        {
            string advs = System.IO.Path.GetDirectoryName(typeof(Autodesk.AdvanceSteel.CADAccess.FilerObject).Assembly.Location) ?? "";
            return System.IO.File.Exists(System.IO.Path.Combine(advs, "PasbJoints.dll"));
        }
        catch (System.Exception) { return false; }
    }

    private static bool CheckUnits(AcDoc doc, Editor ed)
    {
        string u = doc.Database.Insunits.ToString();
        if (string.Equals(u, "Millimeters", StringComparison.OrdinalIgnoreCase)) return true;
        ed.WriteMessage($"\nPASB: o contraventamento usa medidas em milímetros, mas o desenho está em \"{u}\". Use um desenho em milímetros.\n");
        return false;
    }

    private static void Report(Editor ed, string verb, int count, List<string> warnings)
    {
        ed.WriteMessage($"\nPASB: contraventamento {verb}: {count} peça(s). Para mudar algo: PASB > Editar contraventamento.");
        foreach (string w in warnings.Take(15)) ed.WriteMessage("\nPASB: aviso: " + w);
        if (warnings.Count > 15) ed.WriteMessage($"\nPASB: ... e mais {warnings.Count - 15} aviso(s).");
        ed.WriteMessage("\n");
        if (warnings.Any(w => w.StartsWith("FALHA", StringComparison.Ordinal)))
            MessageBox.Show(string.Join(Environment.NewLine, warnings.Take(10)), "PASB — Contraventamento", MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }

    private static bool TryId(Database db, string handle, out ObjectId id)
    {
        id = ObjectId.Null;
        try { return db.TryGetObjectId(new Handle(Convert.ToInt64(handle, 16)), out id) && !id.IsNull; }
        catch (System.Exception) { return false; }
    }

    private static void EnsureRegApp(AcTransaction tr, Database db)
    {
        var rat = (RegAppTable)tr.GetObject(db.RegAppTableId, OpenMode.ForRead);
        if (rat.Has(RegApp)) return;
        rat.UpgradeOpen();
        var r = new RegAppTableRecord { Name = RegApp };
        rat.Add(r);
        tr.AddNewlyCreatedDBObject(r, true);
    }

    private static V3 ToV3(AcPoint p) => new(p.X, p.Y, p.Z);
    private static AsPoint P(V3 v) => new(v.X, v.Y, v.Z);
    private static AsVector Vec(V3 v) => new(v.X, v.Y, v.Z);

    private static string Version =>
        typeof(BracingCommand).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion?.Split('+')[0] ?? "";
}
