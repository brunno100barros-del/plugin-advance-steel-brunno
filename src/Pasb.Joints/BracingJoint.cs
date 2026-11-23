using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.AdvanceSteel.CADAccess;
using Autodesk.AdvanceSteel.CADLink.Database;
using Autodesk.AdvanceSteel.ConstructionTypes;
using Autodesk.AdvanceSteel.Modelling;
using Autodesk.AdvanceSteel.Profiles;
using Pasb.Connector;
using AsMatrix = Autodesk.AdvanceSteel.Geometry.Matrix3d;
using AsPlane = Autodesk.AdvanceSteel.Geometry.Plane;
using AsPoint = Autodesk.AdvanceSteel.Geometry.Point3d;
using AsVector = Autodesk.AdvanceSteel.Geometry.Vector3d;
using Unit = Autodesk.AdvanceSteel.DotNetRoots.Units.Unit;

namespace Pasb.Joints;

/// <summary>Números das mensagens do PASB no banco AstCrtlDb (tabela ErrorMessages). Iguais em bundle/Register-NativeJoints.ps1.</summary>
internal static class Ids
{
    public const int Sheet = 880000;
    public const int PageGeneral = 880001, PageBase = 880002, PageGusset = 880003, PageBolt = 880004, PageRods = 880005,
        PageTurnbuckle = 880006, PageNuts = 880007, PageWelds = 880008;
    public const int PromptCorner1 = 880020, PromptCorner2 = 880021, PromptSupports = 880022;
}

/// <summary>
/// Ligação nativa "PASB - Contraventamento em X" (IRule do SDK do Advance Steel).
/// Entradas: 2 pontos do usuário (cantos opostos, no plano XY da UCS atual) e, opcionalmente, as vigas de apoio.
/// Saída: tudo o que <see cref="BracingGeometry"/> descreve. Com viga de apoio num canto, a chapa de base é recortada
/// na largura do banzo e soldada na viga. Como é ligação nativa, aparece como caixa cinza, edita com duplo clique e
/// se recalcula quando as entradas mudam.
/// </summary>
public sealed class BracingJoint : IRule
{
    private BracingParams _p = new();
    // UCS em que o contraventamento foi lançado (o plano XY dela é o plano do contraventamento).
    private V3 _ucsO = new(0, 0, 0), _ucsX = new(1, 0, 0), _ucsY = new(0, 1, 0), _ucsZ = new(0, 0, 1);

    public ObjectId JointId { get; set; } = null!;

    // ------------------------------------------------------------------ entrada

    public void Query(IUI ui)
    {
        _p = LoadLastSettings();
        try
        {
            ui.GetUCS().GetCoordSystem(out AsPoint o, out AsVector x, out AsVector y, out AsVector z);
            _ucsO = V(o); _ucsX = V(x).Unit; _ucsY = V(y).Unit; _ucsZ = V(z).Unit;
        }
        catch (Exception) { }

        AsPoint a = ui.AcquirePoint(false, new AsPoint(0, 0, 0), true, true, Ids.PromptCorner1, out eUIErrorCodes err);
        if (err != eUIErrorCodes.kUINormal || a == null) return;
        AsPoint b = ui.AcquirePoint(true, a, true, true, Ids.PromptCorner2, out err);
        if (err != eUIErrorCodes.kUINormal || b == null) return;

        var supports = new List<FilerObject>();
        try
        {
            ui.ClassFilter.Reset();
            ui.ClassFilter.AppendAcceptedBaseClass(FilerObject.eObjectType.kBeam);
            FilerObject[]? sel = ui.SelectObjects(Ids.PromptSupports, out err);
            if (err == eUIErrorCodes.kUINormal && sel != null) supports.AddRange(sel.Where(s => s is Beam));
        }
        catch (Exception) { }

        Log($"--- nova ligação: {supports.Count} viga(s) de apoio selecionada(s)");
        if (supports.Count == 0)
        {
            supports.AddRange(FindSupports(V(a), V(b)));
            Log($"vigas de apoio achadas sob os cantos: {supports.Count}");
        }
        var p1 = new UserDefinedPoint(a); p1.WriteToDb();
        var p2 = new UserDefinedPoint(b); p2.WriteToDb();
        var inputs = new List<FilerObject> { p1, p2 };
        inputs.AddRange(supports);
        if (DatabaseManager.Open(JointId) is IJoint jnt)
            jnt.SetInputObjects(inputs, Enumerable.Repeat(1, inputs.Count));
        JointHandoff.Publish(JointId);
    }

    /// <summary>
    /// Sem seleção: procura no modelo as vigas cujo banzo fica sob algum dos 4 cantos do painel (na cota do 1º clique,
    /// com folga de 600 mm). Pilares (eixo na vertical da UCS) ficam de fora.
    /// </summary>
    private List<FilerObject> FindSupports(V3 aw, V3 bw)
    {
        var found = new List<FilerObject>();
        try
        {
            V3 c1 = ToLocal(aw), c2 = ToLocal(bw);
            V3[] corners = { c1, c2, new(c1.X, c2.Y, c1.Z), new(c2.X, c1.Y, c1.Z) };
            DatabaseManager.GetModelObjectIds(out ObjectId[] ids);
            foreach (ObjectId id in ids ?? Array.Empty<ObjectId>())
            {
                if (DatabaseManager.Open(id) is not Beam beam) continue;
                SupportStrip? strip = StripOf(beam, 0, log: false);
                if (strip == null) continue;
                // Só vigas que chegam perto da cota das chapas (do clique até 50 mm acima/abaixo, conforme a UCS).
                bool under = corners.Any(c => strip.ZMax >= c.Z - 60 && strip.ZMin <= c.Z + 60
                    && BracingGeometry.SupportAt(c, new[] { strip }) != null
                    && WithinLength(beam, c));
                if (under && !found.Any(f => f.Handle == beam.Handle)) found.Add(beam);
                if (found.Count >= 24) break;
            }
        }
        catch (Exception ex) { Log("procura de vigas de apoio falhou: " + ex.Message); }
        return found;
    }

    /// <summary>O canto fica entre as pontas da viga (projeção no eixo, com folga de 50 mm).</summary>
    private bool WithinLength(Beam beam, V3 corner)
    {
        V3 s = ToLocal(V(beam.GetPointAtStart())), e = ToLocal(V(beam.GetPointAtEnd()));
        V3 d = new(e.X - s.X, e.Y - s.Y, 0);
        double len = d.Length;
        if (len < 1) return false;
        double t = V3.Dot(new V3(corner.X - s.X, corner.Y - s.Y, 0), d * (1 / len));
        return t >= -50 && t <= len + 50;
    }

    private static BracingParams LoadLastSettings()
    {
        try
        {
            string f = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PASB", "bracing-settings.json");
            if (File.Exists(f)) return BracingParams.FromJson(File.ReadAllText(f));
        }
        catch (Exception) { }
        return new BracingParams();
    }

    // ------------------------------------------------------------------ parâmetros

    public void Save(IFiler filer)
    {
        filer.WriteVersion(1);
        ParamFiler<BracingParams>.Write(filer, _p);
        WriteV(filer, "UcsO", _ucsO); WriteV(filer, "UcsX", _ucsX); WriteV(filer, "UcsY", _ucsY); WriteV(filer, "UcsZ", _ucsZ);
    }

    public void Load(IFiler filer)
    {
        filer.ReadVersion();
        _p = ParamFiler<BracingParams>.Read(filer);
        _ucsO = ReadV(filer, "UcsO", _ucsO); _ucsX = ReadV(filer, "UcsX", _ucsX); _ucsY = ReadV(filer, "UcsY", _ucsY); _ucsZ = ReadV(filer, "UcsZ", _ucsZ);
    }

    private static void WriteV(IFiler f, string name, V3 v) { f.WriteItem(v.X, name + "_x"); f.WriteItem(v.Y, name + "_y"); f.WriteItem(v.Z, name + "_z"); }

    private static V3 ReadV(IFiler f, string name, V3 def)
    {
        try { return new V3((double)f.ReadItem(name + "_x"), (double)f.ReadItem(name + "_y"), (double)f.ReadItem(name + "_z")); }
        catch (Exception) { return def; }
    }

    public string GetTableName() => "";

    public void GetRulePages(IRuleUIBuilder b)
    {
        b.SheetPromptId = Ids.Sheet;
        const string dll = "PasbJoints.dll";
        var mm = Unit.eUnitType.kDistance;
        void Num(int page, string label, string prop) => b.AddTextBox(page, label, prop, typeof(double), mm);
        void Plain(int page, string label, string prop) => b.AddTextBox(page, label, prop, typeof(double));
        void Check(int page, string label, string prop) => b.AddCheckBox(page, label, prop);
        void Choice(int page, string label, string prop, params string[] items) => b.AddSelectorCustom(page, label, prop, typeof(int), items);
        void Profile(int page, string label, string prop) => b.AddSelector(page, IRuleUIBuilder.eSelectorType.kProfile, label, prop, Enumerable.Empty<string>());
        void Material(int page, string label, string prop) => b.AddSelector(page, IRuleUIBuilder.eSelectorType.kMaterial, label, prop, Enumerable.Empty<string>());

        int g = b.BuildRulePage(Ids.PageGeneral, -1, dll, "pasb_cv_geral.png");
        Choice(g, "Diagonais", nameof(BracingParams.Diagonals), "X (as duas diagonais)", "Só a diagonal 1 (clique 1 → clique 2)", "Só a diagonal 2");
        Choice(g, "Posição do esticador em", nameof(BracingParams.TurnbuckleMode), "% da diagonal (até o centro)", "mm a partir do ponto clicado");
        Plain(g, "Distância do esticador (mm ou %)", nameof(BracingParams.TurnbucklePosition));
        Choice(g, "Medida até", nameof(BracingParams.TurnbuckleReference), "a ponta mais próxima do esticador", "o centro do esticador");
        Check(g, "Medir a partir do 2º clique", nameof(BracingParams.TurnbuckleFromCorner2));

        int bp = b.BuildRulePage(Ids.PageBase, -1, dll, "pasb_cv_base.png");
        Check(bp, "a0 - Criar chapa de base", nameof(BracingParams.BasePlate));
        Choice(bp, "Alinhamento", nameof(BracingParams.BaseAlignment), "Na viga de apoio (canto no clique)", "Na diagonal (centrada)");
        Num(bp, "a1 - Comprimento (X da viga)", nameof(BracingParams.BaseLength));
        Num(bp, "a2 - Largura (Y)", nameof(BracingParams.BaseWidth));
        Num(bp, "a3 - Espessura", nameof(BracingParams.BaseThickness));
        Num(bp, "a8 - Topo abaixo do clique", nameof(BracingParams.BaseTopBelowClick));
        Num(bp, "a4 - Chanfro no canto do clique", nameof(BracingParams.BaseChamferClick));
        Num(bp, "a5 - Chanfro canto 2", nameof(BracingParams.BaseChamfer2));
        Num(bp, "a6 - Chanfro canto 3", nameof(BracingParams.BaseChamfer3));
        Num(bp, "a7 - Chanfro canto 4", nameof(BracingParams.BaseChamfer4));
        Num(bp, "Centro a partir do clique (só na diagonal)", nameof(BracingParams.BaseCenterFromPoint));
        Material(bp, "Material das chapas", nameof(BracingParams.PlateMaterial));

        int gp = b.BuildRulePage(Ids.PageGusset, -1, dll, "pasb_cv_gusset.png");
        Num(gp, "b1 - Comprimento", nameof(BracingParams.GussetLength));
        Num(gp, "b2 - Largura", nameof(BracingParams.GussetWidth));
        Num(gp, "b3 - Espessura", nameof(BracingParams.GussetThickness));
        Num(gp, "b4 - Recuo do clique ao gusset", nameof(BracingParams.GussetSetback));
        Num(gp, "b7 - Barra apoiada (solda barra x gusset)", nameof(BracingParams.RodBearingOnGusset));
        Num(gp, "b10 - Chanfro da ponta (lado do clique)", nameof(BracingParams.GussetEndChamfer));
        Num(gp, "b11 - Chanfro da outra ponta", nameof(BracingParams.GussetFarChamfer));

        int bo = b.BuildRulePage(Ids.PageBolt, -1, dll, "pasb_cv_parafuso.png");
        Check(bo, "Criar parafuso (gusset x chapa de base)", nameof(BracingParams.Bolt));
        b.AddBoltSelector(bo, "Parafuso", nameof(BracingParams.BoltStandard), nameof(BracingParams.BoltGrade), nameof(BracingParams.BoltDiameter), nameof(BracingParams.BoltSet));
        Num(bo, "c1 - Parafuso até a ponta do gusset", nameof(BracingParams.BoltFromGussetStart));

        int r = b.BuildRulePage(Ids.PageRods, -1, dll, "pasb_cv_barras.png");
        Profile(r, "Perfil da barra", nameof(BracingParams.RodProfile));
        Material(r, "Material da barra", nameof(BracingParams.RodMaterial));
        Num(r, "Diâmetro da barra (posição no gusset)", nameof(BracingParams.RodDiameter));
        Profile(r, "Perfil da barra roscada (vazio = barra)", nameof(BracingParams.ThreadedRodProfile));
        Material(r, "Material da barra roscada", nameof(BracingParams.ThreadedRodMaterial));
        Num(r, "d2 - Comprimento da barra roscada", nameof(BracingParams.ThreadLength));
        Num(r, "d3 - Rosca dentro do esticador", nameof(BracingParams.ThreadPenetration));
        Check(r, "e0 - Criar talas na emenda", nameof(BracingParams.SpliceBars));
        Num(r, "e1 - Comprimento das talas", nameof(BracingParams.SpliceLength));
        Profile(r, "e2 - Perfil das talas (vazio = barra)", nameof(BracingParams.SpliceProfile));

        int t = b.BuildRulePage(Ids.PageTurnbuckle, -1, dll, "pasb_cv_esticador.png");
        Profile(t, "Cantoneira", nameof(BracingParams.CleatProfile));
        Material(t, "Material da cantoneira", nameof(BracingParams.CleatMaterial));
        Plain(t, "f1 - Rotação da cantoneira (graus)", nameof(BracingParams.CleatRotation));
        Num(t, "f3 - Comprimento da cantoneira", nameof(BracingParams.CleatLength));
        Num(t, "f4 - Chapa de topo: largura", nameof(BracingParams.EndPlateLength));
        Num(t, "f5 - Chapa de topo: altura", nameof(BracingParams.EndPlateWidth));
        Num(t, "f6 - Chapa de topo: espessura", nameof(BracingParams.EndPlateThickness));
        Num(t, "f8 - Ponta da cantoneira à chapa de topo", nameof(BracingParams.EndPlateInset));

        int n = b.BuildRulePage(Ids.PageNuts, -1, dll, "pasb_cv_porcas.png");
        Check(n, "g0 - Criar porcas e arruelas (blocos PASB)", nameof(BracingParams.NutsAndWashers));
        Num(n, "g1 - Porca: entre faces", nameof(BracingParams.NutAcrossFlats));
        Num(n, "g2 - Porca: altura", nameof(BracingParams.NutHeight));
        Num(n, "g3 - Arruela: Ø externo", nameof(BracingParams.WasherOuterDiameter));
        Num(n, "g4 - Arruela: Ø interno", nameof(BracingParams.WasherInnerDiameter));
        Num(n, "g5 - Arruela: espessura", nameof(BracingParams.WasherThickness));

        int w = b.BuildRulePage(Ids.PageWelds, -1, dll, "pasb_cv_soldas.png");
        Check(w, "Soldas de oficina (desmarcado = montagem)", nameof(BracingParams.WeldsInShop));
        Check(w, "h1 - Barra x gusset", nameof(BracingParams.WeldRodGussetOn));
        Num(w, "h1 - Espessura", nameof(BracingParams.WeldRodGusset));
        Check(w, "h2 - Emenda (talas)", nameof(BracingParams.WeldSpliceOn));
        Num(w, "h2 - Espessura", nameof(BracingParams.WeldSplice));
        Check(w, "h3 - Cantoneira x chapa de topo", nameof(BracingParams.WeldEndPlateOn));
        Num(w, "h3 - Espessura", nameof(BracingParams.WeldEndPlate));
        Check(w, "h4 - Chapa de base x estrutura", nameof(BracingParams.WeldSupportOn));
        Num(w, "h4 - Espessura", nameof(BracingParams.WeldSupport));
    }

    // ------------------------------------------------------------------ criação

    public void CreateObjects()
    {
        if (DatabaseManager.Open(JointId) is not IJoint jnt) return;
        var created = new List<CreatedObjectInformation>();
        bool ok = true;
        try
        {
            List<FilerObject> inputs = jnt.InputObjects.ToList();
            List<UserDefinedPoint> pts = inputs.OfType<UserDefinedPoint>().ToList();
            if (pts.Count >= 2)
            {
                V3 p1 = ToLocal(V(pts[0].Point)), p2 = ToLocal(V(pts[1].Point));
                var beams = inputs.OfType<Beam>().ToList();
                Log($"--- recalculando: pontos {p1} {p2}, vigas de apoio: {beams.Count}");
                var strips = new List<SupportStrip>();
                for (int i = 0; i < beams.Count; i++)
                    if (StripOf(beams[i], i) is { } s) strips.Add(s);

                BracingResult res = BracingGeometry.Build(p1, p2, _p, strips);
                foreach (string e in res.Errors) Log("erro: " + e);
                Log($"peças: {res.Parts.Count}; soldas na estrutura: {res.Parts.Count(x => x.SupportTag >= 0)}");
                if (res.Errors.Count > 0) ok = false;
                else ok = Create(res, beams, created);
            }
            else { ok = false; Log($"faltam os 2 pontos de entrada (achados: {pts.Count})"); }
        }
        catch (Exception ex) { ok = false; Log("falha: " + ex); }
        jnt.SetCreatedObjects(created, ok);
    }

    /// <summary>Faixa do banzo da viga de apoio, no sistema da UCS do contraventamento. Null para pilar (eixo na vertical da UCS).</summary>
    private SupportStrip? StripOf(Beam beam, int tag, bool log = true)
    {
        try
        {
            V3 s = ToLocal(V(beam.GetPointAtStart())), e = ToLocal(V(beam.GetPointAtEnd()));
            V3 dir = new(e.X - s.X, e.Y - s.Y, 0);
            if (dir.Length < 1) return null;
            dir = dir.Unit;
            V3 perp = V3.Cross(new V3(0, 0, 1), dir);
            using ProfileType prof = beam.GetProfType();
            prof.GetGeomExtents(out AsPoint min, out AsPoint max);
            AsMatrix m = beam.GetMatProfToWCS();
            double lo = double.MaxValue, hi = double.MinValue, zlo = double.MaxValue, zhi = double.MinValue;
            foreach (double y in new[] { min.y, max.y })
                foreach (double z in new[] { min.z, max.z })
                    foreach (double x in new[] { min.x, max.x })
                    {
                        V3 q = ToLocal(V(new AsPoint(x, y, z).TransformBy(m)));
                        double off = V3.Dot(new V3(q.X - s.X, q.Y - s.Y, 0), perp);
                        lo = Math.Min(lo, off); hi = Math.Max(hi, off);
                        zlo = Math.Min(zlo, q.Z); zhi = Math.Max(zhi, q.Z);
                    }
            if (!(hi > lo)) { if (log) Log($"viga {tag}: seção sem largura"); return null; }
            var strip = new SupportStrip(s + perp * ((lo + hi) / 2), dir, (hi - lo) / 2, tag, zlo, zhi);
            if (log) Log($"viga {tag} ({beam.ProfSectionName}): eixo {strip.Origin} dir {dir} meia largura {strip.HalfWidth:0.#} cota {zlo:0.#} a {zhi:0.#}");
            return strip;
        }
        catch (Exception ex) { if (log) Log($"viga {tag}: falhou ao medir a seção: {ex.Message}"); return null; }
    }

    private bool Create(BracingResult res, List<Beam> beams, List<CreatedObjectInformation> created)
    {
        bool ok = true;
        string nutBlock = "", washerBlock = "";
        if (_p.NutsAndWashers)
        {
            try
            {
                NutBlocks.Ensure(Autodesk.AutoCAD.DatabaseServices.HostApplicationServices.WorkingDatabase,
                    _p.NutAcrossFlats, _p.NutHeight, _p.WasherOuterDiameter, _p.WasherInnerDiameter, _p.WasherThickness);
                nutBlock = NutBlocks.NutName(_p.NutAcrossFlats, _p.NutHeight, _p.WasherInnerDiameter);
                washerBlock = NutBlocks.WasherName(_p.WasherOuterDiameter, _p.WasherInnerDiameter, _p.WasherThickness);
            }
            catch (Exception) { nutBlock = washerBlock = ""; }
        }

        var made = new FilerObject?[res.Parts.Count];
        for (int i = 0; i < res.Parts.Count; i++)
        {
            BracingPart part = res.Parts[i];
            try
            {
                made[i] = part.Kind switch
                {
                    BracingPartKind.Rod or BracingPartKind.ThreadedRod or BracingPartKind.SpliceBar or BracingPartKind.Cleat => MakeBar(part),
                    BracingPartKind.Gusset or BracingPartKind.BasePlate or BracingPartKind.EndPlate => MakePlate(part),
                    BracingPartKind.Nut => nutBlock.Length > 0 ? MakeSpecial(part, nutBlock) : null,
                    BracingPartKind.Washer => washerBlock.Length > 0 ? MakeSpecial(part, washerBlock) : null,
                    _ => null,
                };
            }
            catch (Exception ex)
            {
                Log($"{part.Role} (diagonal {part.Diagonal}) não criado: {ex.Message}");
                if (part.Kind is BracingPartKind.Rod or BracingPartKind.ThreadedRod or BracingPartKind.Cleat or BracingPartKind.Gusset) ok = false;
            }
            if (made[i] != null) created.Add(Info(made[i]!, part, i));
        }

        var location = _p.WeldsInShop ? AtomicElement.eAssemblyLocation.kInShop : AtomicElement.eAssemblyLocation.kOnSite;
        for (int i = 0; i < res.Parts.Count; i++)
        {
            BracingPart part = res.Parts[i];
            if (part.Kind is not (BracingPartKind.Bolt or BracingPartKind.Weld)) continue;
            var objs = part.Connects.Select(k => made[k]).Where(o => o != null).Cast<FilerObject>().ToList();
            if (part.SupportTag >= 0 && part.SupportTag < beams.Count) objs.Add(beams[part.SupportTag]);
            if (objs.Count < 2) continue;
            try
            {
                FilerObject o = part.Kind == BracingPartKind.Bolt ? MakeBolt(part, objs.ToArray()) : MakeWeld(part, objs.ToArray(), location);
                created.Add(Info(o, part, i));
            }
            catch (Exception ex) { Log($"{part.Role} (diagonal {part.Diagonal}) não criado: {ex.Message}"); }
        }
        return ok;
    }

    private static CreatedObjectInformation Info(FilerObject o, BracingPart part, int index) => new()
    {
        CreatedObject = o,
        JointTransferId = $"{part.Kind}_{part.Diagonal}_{index}",
        Attributes = Array.Empty<eObjectsAttribute>(),
    };

    private FilerObject MakeBar(BracingPart part)
    {
        var beam = new StraightBeam(part.Profile, P(part.Start), P(part.End), Vec(new V3(0, 0, 1)));
        beam.WriteToDb();
        if (part.Material.Length > 0) beam.Material = part.Material;
        beam.Role = part.Role;
        if (Math.Abs(part.Angle) > 1e-9) beam.SetXRotation(part.Angle);
        if (part.Kind == BracingPartKind.Cleat) FitCleat(beam, part);
        return beam;
    }

    /// <summary>
    /// Encaixa a cantoneira nas chapas de topo: as chapas ficam dentro das abas da L, encostadas nelas, e a barra passa
    /// no centro da chapa. Mede a seção real do perfil (depois da rotação f1). Perfil que não é L fica centrado na barra.
    /// </summary>
    private void FitCleat(Beam beam, BracingPart part)
    {
        try
        {
            V3 a = part.Start, ax = (part.End - part.Start).Unit;
            V3 nl = V3.Cross(new V3(0, 0, 1), ax).Unit, zl = new V3(0, 0, 1);
            using ProfileType prof = beam.GetProfType();
            if (!prof.GetOuterAugPolygon(false, 0.1, false, out Autodesk.AdvanceSteel.Geometry.Polyline3d poly) || poly == null) { Log("cantoneira: sem contorno da seção"); return; }
            poly.GetApproxVertices(out AsPoint[] verts);
            AsMatrix m = beam.GetMatProfToWCS();
            var sec = verts.Select(v => { V3 q = ToLocal(V(v.TransformBy(m))) - a; return (n: V3.Dot(q, nl), z: V3.Dot(q, zl)); }).ToList();
            double n0 = sec.Min(q => q.n), n1 = sec.Max(q => q.n), z0 = sec.Min(q => q.z), z1 = sec.Max(q => q.z);
            const double tol = 0.5;
            var inner = sec.Where(q => q.n > n0 + tol && q.n < n1 - tol && q.z > z0 + tol && q.z < z1 - tol).ToList();
            double sn, sz;
            if (inner.Count == 1)
            {
                (double n, double z) c = inner[0];
                double hn = Math.Abs(c.n - n0) < Math.Abs(c.n - n1) ? n0 : n1, hz = Math.Abs(c.z - z0) < Math.Abs(c.z - z1) ? z0 : z1;   // quina externa (calcanhar)
                double dn = Math.Sign(c.n - hn), dz = Math.Sign(c.z - hz);
                // Centro da chapa de topo, encostada nas faces internas das abas.
                sn = -(c.n + dn * _p.EndPlateLength / 2);
                sz = -(c.z + dz * _p.EndPlateWidth / 2);
            }
            else { sn = -(n0 + n1) / 2; sz = -(z0 + z1) / 2; }
            V3 shift = nl * sn + zl * sz;
            beam.SetStartEndPoints(P(part.Start + shift), P(part.End + shift));
            Log($"cantoneira: seção n {n0:0.#}..{n1:0.#} z {z0:0.#}..{z1:0.#}, quina interna {(inner.Count == 1 ? "sim" : "não")}, deslocada {sn:0.#} / {sz:0.#}");
        }
        catch (Exception ex) { Log("cantoneira: não deu para encaixar: " + ex.Message); }
    }

    private static void Log(string msg)
    {
        try
        {
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PASB");
            Directory.CreateDirectory(dir);
            string f = Path.Combine(dir, "contraventamento.log");
            if (File.Exists(f) && new FileInfo(f).Length > 1_000_000) File.Delete(f);
            File.AppendAllText(f, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss ") + msg + Environment.NewLine);
        }
        catch (Exception) { }
    }

    private FilerObject MakePlate(BracingPart part)
    {
        var plane = new AsPlane(P(part.Polygon[0]), Vec(part.Normal));
        var plate = new Plate(plane, part.Polygon.Select(P).ToArray(), part.Thickness);
        plate.WriteToDb();
        plate.Portioning = 1;
        if (part.Material.Length > 0) plate.Material = part.Material;
        plate.Role = part.Role;
        return plate;
    }

    private FilerObject MakeBolt(BracingPart part, FilerObject[] objs)
    {
        V3 n = V3.Cross(new V3(0, 0, 1), part.Axis).Unit;
        var bolt = new FinitRectScrewBoltPattern(P(part.Origin), P(part.Origin), Vec(part.Axis), Vec(n));
        bolt.Nx = 1; bolt.Ny = 1;
        if (_p.BoltStandard.Length > 0) bolt.Standard = _p.BoltStandard;
        if (_p.BoltGrade.Length > 0) bolt.Grade = _p.BoltGrade;
        if (_p.BoltSet.Length > 0) bolt.BoltAssembly = _p.BoltSet;
        bolt.ScrewDiameter = _p.BoltDiameter;
        bolt.WriteToDb();
        bolt.Connect(objs, AtomicElement.eAssemblyLocation.kOnSite);
        return bolt;
    }

    private FilerObject MakeWeld(BracingPart part, FilerObject[] objs, AtomicElement.eAssemblyLocation location)
    {
        V3 x = part.Axis.Unit;
        V3 y = Math.Abs(x.Z) > 0.9 ? new V3(1, 0, 0) : V3.Cross(new V3(0, 0, 1), x).Unit;
        var weld = new WeldPoint(P(part.Origin), Vec(x), Vec(y));
        weld.WriteToDb();
        weld.Thickness = part.Size;
        weld.Connect(objs, location);
        return weld;
    }

    private FilerObject MakeSpecial(BracingPart part, string block)
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

    // ------------------------------------------------------------------ UCS: a geometria é calculada no plano XY da UCS

    private V3 ToLocal(V3 w)
    {
        V3 d = w - _ucsO;
        return new V3(V3.Dot(d, _ucsX), V3.Dot(d, _ucsY), V3.Dot(d, _ucsZ));
    }

    private V3 ToWorld(V3 l) => _ucsO + _ucsX * l.X + _ucsY * l.Y + _ucsZ * l.Z;
    private V3 DirToWorld(V3 l) => _ucsX * l.X + _ucsY * l.Y + _ucsZ * l.Z;

    private AsPoint P(V3 local) { V3 w = ToWorld(local); return new AsPoint(w.X, w.Y, w.Z); }
    private AsVector Vec(V3 local) { V3 w = DirToWorld(local); return new AsVector(w.X, w.Y, w.Z); }
    private static V3 V(AsPoint p) => new(p.x, p.y, p.z);
    private static V3 V(AsVector v) => new(v.x, v.y, v.z);
}
