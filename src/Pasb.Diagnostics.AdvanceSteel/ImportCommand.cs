using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Windows.Forms;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Pasb.IfcImport.Model;
using Pasb.Ui;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;
using AcColor = Autodesk.AutoCAD.Colors.Color;
using AcColorMethod = Autodesk.AutoCAD.Colors.ColorMethod;
using AcDoc = Autodesk.AutoCAD.ApplicationServices.Document;
using AcTransaction = Autodesk.AutoCAD.DatabaseServices.Transaction;

[assembly: CommandClass(typeof(Pasb.Diagnostics.AdvanceSteel.ImportCommand))]

namespace Pasb.Diagnostics.AdvanceSteel;

/// <summary>
/// PASB_IMPORTAR_IFC (docs/IFC-IMPORT.md): janela de importação → Pasb.IfcImport.exe (xBIM, fora do AutoCAD) gera as malhas →
/// este comando desenha sólidos (Solid3d) ou malhas (SubDMesh), com blocos para peças repetidas, layers e cores por nível.
/// Grava no desenho somente os objetos importados, layers e blocos (Ctrl+Z desfaz a importação inteira).
/// </summary>
public sealed class ImportCommand
{
    private const string RegApp = "PASB_IFC";

    [CommandMethod("PASB", "PASB_IMPORTAR_IFC", CommandFlags.Modal)]
    public void Import()
    {
        AcDoc? doc = AcApp.DocumentManager.MdiActiveDocument;
        if (doc == null) return;
        Editor ed = doc.Editor;
        try
        {
            string exe = EnginePath();
            if (!File.Exists(exe)) { ed.WriteMessage($"\nPASB: leitor de IFC não encontrado ({exe}). Reinstale o plugin.\n"); return; }

            // 1) Janela
            Aci.Resolver = i => AcColor.FromColorIndex(AcColorMethod.ByAci, i).ColorValue;
            ImportSettings settings = ImportSettings.Load();
            ImportChoice choice;
            using (var dlg = new ImportDialog(settings, f => Scan(exe, f), PickAci, Version))
            {
                if (Autodesk.AutoCAD.ApplicationServices.Application.ShowModalDialog(dlg) != DialogResult.OK || dlg.Choice == null)
                { ed.WriteMessage("\nPASB: importação cancelada.\n"); return; }
                choice = dlg.Choice;
            }
            settings.Save();

            // 2) Leitura e triangulação fora do AutoCAD
            var clock = Stopwatch.StartNew();
            ed.WriteMessage($"\nPASB: lendo {Path.GetFileName(choice.IfcFile)} (pode demorar em arquivos grandes)...");
            MeshPackage pkg = Convert(exe, choice);
            ed.WriteMessage($"\nPASB: {pkg.Instances.Count} elemento(s), {pkg.Geometries.Count} geometria(s) lidas em {clock.Elapsed.TotalSeconds:0.0} s. Desenhando...");

            // 3) Desenho
            ImportStats st = Draw(doc, pkg, choice);
            ed.WriteMessage($"\nPASB: importação concluída em {clock.Elapsed.TotalSeconds:0.0} s: {st.Summary()}");
            foreach (string w in pkg.Warnings.Take(10)) ed.WriteMessage("\nPASB: aviso: " + w);
            if (pkg.Warnings.Count > 10) ed.WriteMessage($"\nPASB: ... e mais {pkg.Warnings.Count - 10} aviso(s).");
            ed.WriteMessage("\nPASB: Ctrl+Z desfaz a importação inteira.\n");
        }
        catch (System.Exception ex)
        {
            ed.WriteMessage($"\nPASB: PASB_IMPORTAR_IFC falhou: {ex.GetType().Name}: {ex.Message}\n");
        }
    }

    // ------------------------------------------------------------------ leitor externo

    private static string EnginePath() =>
        Path.Combine(Path.GetDirectoryName(typeof(ImportCommand).Assembly.Location)!, "IfcImport", "Pasb.IfcImport.exe");

    private static IfcScan Scan(string exe, string ifc)
    {
        string outp = TempFile(".json");
        try
        {
            RunEngine(exe, null, "scan", ifc, outp);
            return ScanJson.Deserialize(File.ReadAllText(outp));
        }
        finally { TryDelete(outp); }
    }

    private static MeshPackage Convert(string exe, ImportChoice c)
    {
        string req = TempFile(".json"), outp = TempFile(".pasbmesh");
        try
        {
            var r = new ConvertRequest
            {
                IfcFile = c.IfcFile, Types = c.Types, Storeys = c.StoreyKeys,
                IncludeOpenings = c.Settings.IncludeOpenings, IncludeSpaces = c.Settings.IncludeSpaces,
            };
            File.WriteAllText(req, JsonSerializer.Serialize(r, ScanJson.Options), new UTF8Encoding(false));
            using (var pm = new ProgressMeter())
            {
                pm.Start("PASB: lendo IFC");
                pm.SetLimit(100);
                int last = 0;
                RunEngine(exe, pct => { while (last < pct) { pm.MeterProgress(); last++; } }, "convert", req, outp);
                pm.Stop();
            }
            using var fs = File.OpenRead(outp);
            return MeshPackage.Read(fs);
        }
        finally { TryDelete(req); TryDelete(outp); }
    }

    /// <summary>Roda o Pasb.IfcImport.exe sem travar o AutoCAD; repassa "PROGRESSO n" e lança com a mensagem de erro.</summary>
    private static void RunEngine(string exe, Action<int>? progress, params string[] args)
    {
        var psi = new ProcessStartInfo(exe)
        {
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8,
        };
        foreach (string a in args) psi.ArgumentList.Add(a);
        using Process p = Process.Start(psi) ?? throw new InvalidOperationException("não foi possível iniciar o leitor de IFC");
        var err = new StringBuilder();
        var pending = new System.Collections.Concurrent.ConcurrentQueue<int>();
        p.OutputDataReceived += (_, e) => { if (e.Data?.StartsWith("PROGRESSO ") == true && int.TryParse(e.Data[10..], out int v)) pending.Enqueue(v); };
        p.ErrorDataReceived += (_, e) => { if (e.Data != null) lock (err) err.AppendLine(e.Data); };
        p.BeginOutputReadLine(); p.BeginErrorReadLine();
        while (!p.WaitForExit(100))
        {
            while (pending.TryDequeue(out int v)) progress?.Invoke(v);
            System.Windows.Forms.Application.DoEvents();
        }
        p.WaitForExit();
        while (pending.TryDequeue(out int v)) progress?.Invoke(v);
        if (p.ExitCode != 0)
        {
            string msg = string.Join(" ", err.ToString().Split('\n').Select(l => l.Trim())
                .Where(l => l.Length > 0 && !l.Contains("xbim InternalServices") && !l.Contains("XbimServices") && !l.StartsWith("//")));
            throw new InvalidOperationException("leitor de IFC: " + (msg.Length > 0 ? msg : "código " + p.ExitCode));
        }
    }

    private static short? PickAci(short current)
    {
        var cd = new Autodesk.AutoCAD.Windows.ColorDialog
        {
            Color = AcColor.FromColorIndex(AcColorMethod.ByAci, current),
            IncludeByBlockByLayer = false,
        };
        if (cd.ShowModal() != true) return null;
        return cd.Color.IsByAci ? cd.Color.ColorIndex : current;
    }

    // ------------------------------------------------------------------ desenho

    private sealed class ImportStats
    {
        public int Solids, Meshes, FallbackMeshes, Skipped, BlockDefs, BlockRefs;
        public readonly SortedSet<string> Layers = new(StringComparer.OrdinalIgnoreCase);
        public string Summary() =>
            $"{Solids} sólido(s), {Meshes} malha(s)" + (FallbackMeshes > 0 ? $" ({FallbackMeshes} como malha por falha na conversão em sólido)" : "") +
            (BlockDefs > 0 ? $", {BlockRefs} inserção(ões) de {BlockDefs} bloco(s)" : "") +
            (Skipped > 0 ? $", {Skipped} não importado(s)" : "") + $"; layers: {string.Join(", ", Layers)}.";
    }

    private static ImportStats Draw(AcDoc doc, MeshPackage pkg, ImportChoice choice)
    {
        ImportSettings s = choice.Settings;
        Database db = doc.Database;
        double unit = 1.0 / ExportCommand.UnitToMeters(db.Insunits.ToString(), out _);   // metros → unidades do desenho
        var st = new ImportStats();
        var geoms = pkg.Geometries.ToDictionary(g => g.Id);
        // Blocos só para geometrias usadas por mais de uma instância nesta importação.
        var uses = pkg.Instances.GroupBy(i => i.GeometryId).ToDictionary(g => g.Key, g => g.Count());
        string stem = ImportNaming.Layer("", Path.GetFileNameWithoutExtension(choice.IfcFile));

        using (doc.LockDocument())
        using (AcTransaction tr = db.TransactionManager.StartTransaction())
        using (var pm = new ProgressMeter())
        {
            pm.Start("PASB: desenhando IFC");
            pm.SetLimit(Math.Max(1, pkg.Instances.Count));
            EnsureRegApp(tr, db);
            var space = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
            TempSpace = space; TempTr = tr;
            var layers = new Dictionary<string, ObjectId>(StringComparer.OrdinalIgnoreCase);
            var blocks = new Dictionary<int, ObjectId>();

            foreach (MeshInstance inst in pkg.Instances)
            {
                pm.MeterProgress();
                if (!geoms.TryGetValue(inst.GeometryId, out MeshGeometry? g)) { st.Skipped++; continue; }
                string layer = LayerName(inst, choice);
                short? layerColor = s.ColorMode == ImportColorMode.ByStorey && s.LayerMode == ImportLayerMode.ByStorey
                    ? choice.StoreyColors.GetValueOrDefault(inst.StoreyKey, (short)7) : null;
                EnsureLayer(tr, db, layers, layer, layerColor);
                st.Layers.Add(layer);
                Matrix3d xf = ToMatrix(inst.Transform, unit);

                Entity? ent;
                if (s.Blocks && uses[inst.GeometryId] > 1)
                {
                    if (!blocks.TryGetValue(g.Id, out ObjectId btrId))
                    {
                        Entity? def = MakeEntity(g, unit, s, st);
                        if (def == null) { st.Skipped++; continue; }
                        def.Layer = "0";
                        def.Color = AcColor.FromColorIndex(AcColorMethod.ByBlock, 0);
                        btrId = DefineBlock(tr, db, UniqueBlockName(tr, db, $"IFC_{stem}_{inst.IfcType[3..]}_{g.Id}"), def);
                        blocks[g.Id] = btrId; st.BlockDefs++;
                    }
                    ent = new BlockReference(Point3d.Origin, btrId);
                    st.BlockRefs++;
                }
                else
                {
                    ent = MakeEntity(g, unit, s, st);
                    if (ent == null) { st.Skipped++; continue; }
                }

                ent.SetDatabaseDefaults(db);
                ent.Layer = layer;
                ApplyColor(ent, inst, choice, layerColor != null);
                ent.TransformBy(xf);
                space.AppendEntity(ent);
                tr.AddNewlyCreatedDBObject(ent, true);
                ent.XData = new ResultBuffer(
                    new TypedValue((int)DxfCode.ExtendedDataRegAppName, RegApp),
                    new TypedValue((int)DxfCode.ExtendedDataAsciiString, inst.IfcType),
                    new TypedValue((int)DxfCode.ExtendedDataAsciiString, inst.GlobalId ?? ""),
                    new TypedValue((int)DxfCode.ExtendedDataAsciiString, Trim(inst.Name ?? "")),
                    new TypedValue((int)DxfCode.ExtendedDataAsciiString, Trim(choice.StoreyNames.GetValueOrDefault(inst.StoreyKey, "Sem nível"))));
            }
            pm.Stop();
            TempSpace = null; TempTr = null;
            tr.Commit();
        }
        return st;
    }

    /// <summary>Cria o Solid3d (ou a malha) a partir dos triângulos, em coordenadas locais da geometria.</summary>
    private static Entity? MakeEntity(MeshGeometry g, double unit, ImportSettings s, ImportStats st)
    {
        var pts = new Point3dCollection();
        for (int k = 0; k < g.VertexCount; k++) pts.Add(new Point3d(g.Vertices[3 * k] * unit, g.Vertices[3 * k + 1] * unit, g.Vertices[3 * k + 2] * unit));
        var faces = new Int32Collection();
        for (int t = 0; t < g.TriangleCount; t++) { faces.Add(3); faces.Add(g.Triangles[3 * t]); faces.Add(g.Triangles[3 * t + 1]); faces.Add(g.Triangles[3 * t + 2]); }
        var mesh = new SubDMesh();
        try { mesh.SetSubDMesh(pts, faces, 0); }
        catch (System.Exception) { mesh.Dispose(); return null; }

        if (s.Geometry == ImportGeometry.Mesh) { st.Meshes++; return mesh; }
        try
        {
            Solid3d solid = mesh.ConvertToSolid(false, true);
            mesh.Dispose();
            st.Solids++;
            return solid;
        }
        catch (System.Exception)
        {
            // Segunda tentativa com a malha gravada no desenho (algumas versões só convertem objetos do banco de dados).
            if (TempSpace != null && TempTr != null)
            {
                try
                {
                    TempSpace.AppendEntity(mesh);
                    TempTr.AddNewlyCreatedDBObject(mesh, true);
                    Solid3d solid = mesh.ConvertToSolid(false, true);
                    mesh.Erase();
                    st.Solids++;
                    return solid;
                }
                catch (System.Exception)
                {
                    if (!mesh.IsErased && mesh.ObjectId.IsValid) mesh.Erase();
                    if (!s.FailuresAsMesh) return null;
                    // A malha original já está no banco (apagada): cria uma cópia nova para devolver.
                    var copy = new SubDMesh();
                    copy.SetSubDMesh(pts, faces, 0);
                    st.Meshes++; st.FallbackMeshes++;
                    return copy;
                }
            }
            if (s.FailuresAsMesh) { st.Meshes++; st.FallbackMeshes++; return mesh; }
            mesh.Dispose();
            return null;
        }
    }

    // Espaço e transação da importação em andamento (para a conversão em sólido com objeto no banco).
    [ThreadStatic] private static BlockTableRecord? TempSpace;
    [ThreadStatic] private static AcTransaction? TempTr;

    private static Matrix3d ToMatrix(double[] m, double unit) => new(new[]
    {
        m[0], m[4], m[8], m[12] * unit,
        m[1], m[5], m[9], m[13] * unit,
        m[2], m[6], m[10], m[14] * unit,
        0, 0, 0, 1,
    });

    private static string LayerName(MeshInstance i, ImportChoice c) => c.Settings.LayerMode switch
    {
        ImportLayerMode.Single => c.Settings.SingleLayer,
        ImportLayerMode.ByIfcType => ImportNaming.Layer("IFC_", ImportDialog.FriendlyName(i.IfcType)),
        ImportLayerMode.ByIfcLayer => i.IfcLayer != null ? ImportNaming.Layer("IFC_", i.IfcLayer) : ImportNaming.Layer("IFC_", ImportDialog.FriendlyName(i.IfcType)),
        _ => ImportNaming.Layer("IFC_", c.StoreyNames.GetValueOrDefault(i.StoreyKey, "Sem nível")),
    };

    private static void ApplyColor(Entity e, MeshInstance i, ImportChoice c, bool layerCarriesColor)
    {
        switch (c.Settings.ColorMode)
        {
            case ImportColorMode.Single:
                e.Color = AcColor.FromColorIndex(AcColorMethod.ByAci, c.Settings.SingleColor);
                break;
            case ImportColorMode.ByStorey:
                e.Color = layerCarriesColor
                    ? AcColor.FromColorIndex(AcColorMethod.ByLayer, 256)
                    : AcColor.FromColorIndex(AcColorMethod.ByAci, c.StoreyColors.GetValueOrDefault(i.StoreyKey, (short)7));
                break;
            default:
                e.Color = i.Color is { } rgb ? AcColor.FromRgb(rgb[0], rgb[1], rgb[2]) : AcColor.FromColorIndex(AcColorMethod.ByLayer, 256);
                if (i.Transparency > 0.01) e.Transparency = new Autodesk.AutoCAD.Colors.Transparency((byte)Math.Clamp(Math.Round(255 * (1 - i.Transparency)), 0, 255));
                break;
        }
    }

    private static void EnsureLayer(AcTransaction tr, Database db, Dictionary<string, ObjectId> cache, string name, short? color)
    {
        if (cache.ContainsKey(name)) return;
        var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
        if (lt.Has(name))
        {
            ObjectId id = lt[name];
            if (color is short c)
            {
                var rec = (LayerTableRecord)tr.GetObject(id, OpenMode.ForWrite);
                rec.Color = AcColor.FromColorIndex(AcColorMethod.ByAci, c);
            }
            cache[name] = id;
            return;
        }
        lt.UpgradeOpen();
        var ltr = new LayerTableRecord { Name = name, Color = AcColor.FromColorIndex(AcColorMethod.ByAci, color ?? 7) };
        cache[name] = lt.Add(ltr);
        tr.AddNewlyCreatedDBObject(ltr, true);
    }

    private static ObjectId DefineBlock(AcTransaction tr, Database db, string name, Entity content)
    {
        var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForWrite);
        var btr = new BlockTableRecord { Name = name, Origin = Point3d.Origin };
        ObjectId id = bt.Add(btr);
        tr.AddNewlyCreatedDBObject(btr, true);
        btr.AppendEntity(content);
        tr.AddNewlyCreatedDBObject(content, true);
        return id;
    }

    private static string UniqueBlockName(AcTransaction tr, Database db, string baseName)
    {
        var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
        string n = ImportNaming.Layer("", baseName);
        if (!bt.Has(n)) return n;
        for (int k = 2; ; k++) if (!bt.Has(n + "_" + k)) return n + "_" + k;
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

    private static string Trim(string s) => s.Length > 250 ? s[..250] : s;

    private static string TempFile(string ext) => Path.Combine(Path.GetTempPath(), "pasb-import-" + Guid.NewGuid().ToString("N") + ext);
    private static void TryDelete(string f) { try { if (File.Exists(f)) File.Delete(f); } catch (System.Exception) { } }

    private static string Version =>
        typeof(ImportCommand).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion?.Split('+')[0] ?? "";
}
