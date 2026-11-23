using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.AdvanceSteel.Arrangement;
using Autodesk.AdvanceSteel.CADAccess;
using Autodesk.AdvanceSteel.CADLink.Database;
using Autodesk.AdvanceSteel.ConstructionTypes;
using Autodesk.AdvanceSteel.Contours;
using Autodesk.AdvanceSteel.Modelling;
using Autodesk.AdvanceSteel.Profiles;
using Pasb.Connector;
using AsMatrix = Autodesk.AdvanceSteel.Geometry.Matrix3d;
using AsMatrix2 = Autodesk.AdvanceSteel.Geometry.Matrix2d;
using AsPoint = Autodesk.AdvanceSteel.Geometry.Point3d;
using AsVector = Autodesk.AdvanceSteel.Geometry.Vector3d;
using Unit = Autodesk.AdvanceSteel.DotNetRoots.Units.Unit;

namespace Pasb.Joints;

/// <summary>Números das mensagens da ligação antidobra no banco AstCrtlDb. Iguais em bundle/Register-NativeJoints.ps1.</summary>
internal static class SagIds
{
    public const int Sheet = 880100;
    public const int PageRows = 880101, PageBays = 880102, PageRod = 880103, PageNuts = 880104, PageDouble = 880105, PageRigid = 880106,
        PageCleat = 880107, PageTies = 880108, PageTieEnd = 880109, PageRodHoles = 880110, PageTieHoles = 880111, PagePurlinBolts = 880112,
        PageRigidBolts = 880113;
    public const int PromptPurlins = 880120, PromptFrameA = 880121, PromptFrameB = 880122;
}

/// <summary>
/// Ligação nativa "PASB - Sistema antidobra" (correntes de terça). Entradas: as terças de uma água do telhado e,
/// opcionalmente, os dois pórticos (A e B) entre os quais ficam as filas. A geometria é de <see cref="SagRodGeometry"/>;
/// aqui as terças e os pórticos são medidos (posição da alma, espessura, altura) e as peças são criadas.
/// </summary>
public sealed class SagRodJoint : IRule
{
    private SagRodParams _p = new();
    private int _frames;                     // 2 = as duas primeiras entradas são os pórticos A e B

    // Sistema do telhado (calculado a cada recálculo).
    private V3 _o, _x = new(1, 0, 0), _y = new(0, 1, 0), _z = new(0, 0, 1);

    public ObjectId JointId { get; set; } = null!;

    private static string SettingsFile => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PASB", "antidobra-settings.json");

    // ------------------------------------------------------------------ entrada

    public void Query(IUI ui)
    {
        _p = LoadLastSettings();
        // 1º banzo (pórtico A), 2º banzo (pórtico B), depois as terças entre eles.
        Beam? a = Pick(ui, SagIds.PromptFrameA);
        if (a == null) { Log("--- seleção cancelada no 1º banzo"); return; }
        Beam? b = Pick(ui, SagIds.PromptFrameB);
        if (b == null || b.Handle == a.Handle) { Log("--- seleção cancelada no 2º banzo"); return; }
        var purlins = Select(ui, SagIds.PromptPurlins).Where(t => t.Handle != a.Handle && t.Handle != b.Handle).ToList();
        if (purlins.Count < 2) { Log($"--- seleção cancelada: {purlins.Count} terça(s)"); return; }

        _frames = 2;
        var inputs = new List<FilerObject> { a, b };
        inputs.AddRange(purlins);
        Log($"--- nova ligação: banzos {a.Handle} e {b.Handle}, {purlins.Count} terça(s) selecionada(s)");
        if (DatabaseManager.Open(JointId) is IJoint jnt)
            jnt.SetInputObjects(inputs, Enumerable.Repeat(1, inputs.Count));
        JointHandoff.Publish(JointId);
    }

    /// <summary>Um clique numa viga.</summary>
    private static Beam? Pick(IUI ui, int prompt)
    {
        try
        {
            ui.ClassFilter.Reset();
            ui.ClassFilter.AppendAcceptedBaseClass(FilerObject.eObjectType.kBeam);
            FilerObject? o = ui.SelectObject(prompt, true, out eUIErrorCodes err);
            if (err == eUIErrorCodes.kUINormal) return o as Beam;
        }
        catch (Exception ex) { Log("clique falhou: " + ex.Message); }
        return null;
    }

    private static List<Beam> Select(IUI ui, int prompt)
    {
        try
        {
            ui.ClassFilter.Reset();
            ui.ClassFilter.AppendAcceptedBaseClass(FilerObject.eObjectType.kBeam);
            FilerObject[]? sel = ui.SelectObjects(prompt, out eUIErrorCodes err);
            if (err == eUIErrorCodes.kUINormal && sel != null) return sel.OfType<Beam>().ToList();
        }
        catch (Exception ex) { Log("seleção falhou: " + ex.Message); }
        return new List<Beam>();
    }

    private static SagRodParams LoadLastSettings()
    {
        try { if (File.Exists(SettingsFile)) return SagRodParams.FromJson(File.ReadAllText(SettingsFile)); }
        catch (Exception) { }
        return new SagRodParams();
    }

    private void SaveLastSettings()
    {
        try { Directory.CreateDirectory(Path.GetDirectoryName(SettingsFile)!); File.WriteAllText(SettingsFile, _p.ToJson()); }
        catch (Exception) { }
    }

    // ------------------------------------------------------------------ parâmetros

    public void Save(IFiler filer)
    {
        filer.WriteVersion(1);
        ParamFiler<SagRodParams>.Write(filer, _p);
        filer.WriteItem(_frames, "PasbFrames");
    }

    public void Load(IFiler filer)
    {
        filer.ReadVersion();
        _p = ParamFiler<SagRodParams>.Read(filer);
        try { _frames = Convert.ToInt32(filer.ReadItem("PasbFrames")); } catch (Exception) { _frames = 0; }
    }

    public string GetTableName() => "";

    public void GetRulePages(IRuleUIBuilder b)
    {
        b.SheetPromptId = SagIds.Sheet;
        const string dll = "PasbJoints.dll";
        var mm = Unit.eUnitType.kDistance;
        void Num(int page, string label, string prop) => b.AddTextBox(page, label, prop, typeof(double), mm);
        void Int(int page, string label, string prop) => b.AddTextBox(page, label, prop, typeof(int));
        void Text(int page, string label, string prop) => b.AddTextBox(page, label, prop, typeof(string));
        void Check(int page, string label, string prop) => b.AddCheckBox(page, label, prop);
        void Choice(int page, string label, string prop, params string[] items) => b.AddSelectorCustom(page, label, prop, typeof(int), items);
        void Profile(int page, string label, string prop) => b.AddSelector(page, IRuleUIBuilder.eSelectorType.kProfile, label, prop, Enumerable.Empty<string>());
        void Material(int page, string label, string prop) => b.AddSelector(page, IRuleUIBuilder.eSelectorType.kMaterial, label, prop, Enumerable.Empty<string>());

        int f = b.BuildRulePage(SagIds.PageRows, -1, dll, "pasb_ad_filas.png");
        Choice(f, "1 - Distribuição das filas", nameof(SagRodParams.RowMode), "Número de fileiras", "Espaçamento máximo");
        Int(f, "2 - Nº de fileiras", nameof(SagRodParams.RowCount));
        Num(f, "3 - Espaçamento", nameof(SagRodParams.RowSpacing));
        Choice(f, "4 - Base do cálculo", nameof(SagRodParams.SpanBase), "Vão entre eixos dos pórticos", "Vão entre faces dos pórticos");

        int e = b.BuildRulePage(SagIds.PageBays, -1, dll, "pasb_ad_entreter.png");
        Text(e, "1 - Rígidas (ex.: 1 6 T; T = de cima; * = todas)", nameof(SagRodParams.RigidBays));
        Text(e, "2 - Sem agulha (ex.: 5 8)", nameof(SagRodParams.NoRodBays));

        int r = b.BuildRulePage(SagIds.PageRod, -1, dll, "pasb_ad_agulha.png");
        Profile(r, "1 - Perfil da barra lisa", nameof(SagRodParams.RodProfile));
        Material(r, "2 - Material da barra lisa", nameof(SagRodParams.RodMaterial));
        Profile(r, "3 - Perfil da barra roscada (vazio = lisa)", nameof(SagRodParams.RodThreadProfile));
        Material(r, "4 - Material da roscada", nameof(SagRodParams.RodThreadMaterial));
        Num(r, "Diâmetro da barra (φ)", nameof(SagRodParams.RodDiameter));
        Num(r, "5 - Rosca em cada ponta", nameof(SagRodParams.RodThreadLength));
        Num(r, "6 - Sobra antes (0 = auto)", nameof(SagRodParams.RodExtraBefore));
        Num(r, "7 - Sobra depois (0 = auto)", nameof(SagRodParams.RodExtraAfter));
        Choice(r, "8 - Emenda lisa/roscada", nameof(SagRodParams.RodSplice), "De topo", "Por traspasse");
        Num(r, "9 - Cordão (0 = 5φ)", nameof(SagRodParams.RodLapWeld));
        Num(r, "10 - Espaço cordões (0 = 5φ)", nameof(SagRodParams.RodLapGap));
        Num(r, "11 - Folga da lisa até a alma (traspasse)", nameof(SagRodParams.RodLapClearance));

        int n = b.BuildRulePage(SagIds.PageNuts, -1, dll, "pasb_ad_porcas.png");
        Choice(n, "1 - Lado da porca", nameof(SagRodParams.NutSide), "Antes", "Depois", "Dos dois lados");
        Check(n, "2 - Lançar porca", nameof(SagRodParams.Nuts));
        Num(n, "3 - Porca: entre faces", nameof(SagRodParams.NutAcrossFlats));
        Num(n, "3 - Porca: altura", nameof(SagRodParams.NutHeight));
        Check(n, "5 - Lançar arruela", nameof(SagRodParams.Washers));
        Num(n, "6 - Arruela: Ø externo", nameof(SagRodParams.WasherOuterDiameter));
        Num(n, "6 - Arruela: Ø interno", nameof(SagRodParams.WasherInnerDiameter));
        Num(n, "6 - Arruela: espessura", nameof(SagRodParams.WasherThickness));

        int d = b.BuildRulePage(SagIds.PageDouble, -1, dll, "pasb_ad_dupla.png");
        Check(d, "1 - Agulha dupla cruzada", nameof(SagRodParams.DoubleCrossed));
        Num(d, "2 - Cota da mesa superior", nameof(SagRodParams.DoubleTopOffset));
        Num(d, "3 - Cota da mesa inferior", nameof(SagRodParams.DoubleBottomOffset));

        int g = b.BuildRulePage(SagIds.PageRigid, -1, dll, "pasb_ad_rigida.png");
        Profile(g, "1 - Perfil da rígida", nameof(SagRodParams.RigidProfile));
        Material(g, "2 - Material da rígida", nameof(SagRodParams.RigidMaterial));
        Num(g, "3 - Folga até a alma", nameof(SagRodParams.RigidClearance));
        Num(g, "4 - Altura da face (0 = h/2)", nameof(SagRodParams.RigidFaceHeight));
        Choice(g, "5 - Posição da L", nameof(SagRodParams.RigidPosition), "Aba em cima, abre p/ lado padrão", "Aba em cima, abre p/ outro lado",
            "Aba embaixo, abre p/ lado padrão", "Aba embaixo, abre p/ outro lado");
        Num(g, "6 - Desloc. no suporte (+ B)", nameof(SagRodParams.RigidShift));

        int c = b.BuildRulePage(SagIds.PageCleat, -1, dll, "pasb_ad_cantoneira.png");
        Profile(c, "1 - Cantoneira de ligação", nameof(SagRodParams.CleatProfile));
        Material(c, "2 - Material da cantoneira", nameof(SagRodParams.CleatMaterial));
        Num(c, "3 - Comprimento", nameof(SagRodParams.CleatLength));
        Num(c, "4 - Costas do suporte ao paraf.", nameof(SagRodParams.CleatBackToBolt));
        Choice(c, "5 - Cantoneira na rígida", nameof(SagRodParams.CleatSide), "Por fora da rígida", "Por dentro da rígida");
        Choice(c, "6 - Ligação na rígida", nameof(SagRodParams.CleatLink), "Soldada na rígida", "Parafusada na rígida");
        Num(c, "7 - Ponta ao parafuso (parafusada)", nameof(SagRodParams.CleatEndToBolt));
        Num(c, "9 - Espessura da solda", nameof(SagRodParams.WeldSize));
        Check(c, "Soldas de oficina (desmarcado = montagem)", nameof(SagRodParams.WeldsInShop));

        int t = b.BuildRulePage(SagIds.PageTies, -1, dll, "pasb_ad_tirante.png");
        Check(t, "Lançar tirantes", nameof(SagRodParams.Ties));
        Text(t, "1 - Vãos (ex.: 1 3)", nameof(SagRodParams.TieSpans));
        Text(t, "2 - Entreterças (ex.: 1 5 T)", nameof(SagRodParams.TieBays));
        Choice(t, "3 - Direção (todos)", nameof(SagRodParams.TieDirection), "Subindo", "Descendo", "Em X");
        Text(t, "4 - Direção por tirante (S D X)", nameof(SagRodParams.TieDirections));
        Num(t, "5 - Distância da fila", nameof(SagRodParams.TieFromRow));
        Num(t, "6 - Recuo do pórtico", nameof(SagRodParams.TieFromFrame));

        int te = b.BuildRulePage(SagIds.PageTieEnd, -1, dll, "pasb_ad_ponta.png");
        Profile(te, "2 - Perfil do tirante (lisa)", nameof(SagRodParams.TieProfile));
        Material(te, "3 - Material da lisa", nameof(SagRodParams.TieMaterial));
        Profile(te, "4 - Perfil da ponta roscada (vazio = lisa)", nameof(SagRodParams.TieThreadProfile));
        Material(te, "5 - Material da roscada", nameof(SagRodParams.TieThreadMaterial));
        Num(te, "6 - Rosca da ponta", nameof(SagRodParams.TieThreadLength));
        Num(te, "7 - Rosca atrás da alma", nameof(SagRodParams.TieThreadBehindWeb));

        int ha = b.BuildRulePage(SagIds.PageRodHoles, -1, dll, "pasb_ad_furos_agulha.png");
        Choice(ha, "1 - Agulha: forma", nameof(SagRodParams.RodHoleSlotted), "Redondo", "Oblongo");
        Num(ha, "2 - Agulha: diâmetro", nameof(SagRodParams.RodHoleDiameter));
        Num(ha, "3 - Agulha: comprimento", nameof(SagRodParams.RodHoleLength));
        Choice(ha, "4 - Agulha: direção", nameof(SagRodParams.RodHoleDirection), "Ao longo da terça", "Na altura da alma");
        Choice(ha, "5 - Posição na alma", nameof(SagRodParams.RodHeight), "Centro da alma", "Cota até a mesa de cima");
        Num(ha, "6 - Cota até a mesa", nameof(SagRodParams.RodHeightFromFlange));
        Num(ha, "7 - Desloc. entreterça par", nameof(SagRodParams.OffsetEvenBay));
        Num(ha, "8 - Desloc. entreterça ímpar", nameof(SagRodParams.OffsetOddBay));
        Choice(ha, "9 - Dupla: forma", nameof(SagRodParams.DoubleHoleSlotted), "Redondo", "Oblongo");
        Num(ha, "10 - Dupla: diâmetro", nameof(SagRodParams.DoubleHoleDiameter));
        Num(ha, "11 - Dupla: comprimento", nameof(SagRodParams.DoubleHoleLength));
        Choice(ha, "12 - Dupla: direção", nameof(SagRodParams.DoubleHoleDirection), "Ao longo da terça", "Na altura da alma");

        int ht = b.BuildRulePage(SagIds.PageTieHoles, -1, dll, "pasb_ad_furos_tirante.png");
        Choice(ht, "1 - Furo e porca: forma", nameof(SagRodParams.TieHoleSlotted), "Redondo", "Oblongo");
        Num(ht, "2 - Furo e porca: diâmetro", nameof(SagRodParams.TieHoleDiameter));
        Num(ht, "3 - Furo e porca: comprim.", nameof(SagRodParams.TieHoleLength));
        Choice(ht, "4 - Furo e porca: direção", nameof(SagRodParams.TieHoleDirection), "Ao longo da terça", "Na altura da alma");

        int bt = b.BuildRulePage(SagIds.PagePurlinBolts, -1, dll, "pasb_ad_paraf_terca.png");
        b.AddBoltSelector(bt, "Parafuso na terça", nameof(SagRodParams.BoltStandard), nameof(SagRodParams.BoltGrade), nameof(SagRodParams.BoltDiameter), nameof(SagRodParams.BoltSet));
        Int(bt, "1 - Nº de parafusos", nameof(SagRodParams.PurlinBoltCount));
        Num(bt, "2 - Espaçamento", nameof(SagRodParams.PurlinBoltSpacing));
        Num(bt, "3 - Folga do furo", nameof(SagRodParams.PurlinBoltTolerance));

        int br = b.BuildRulePage(SagIds.PageRigidBolts, -1, dll, "pasb_ad_paraf_rigida.png");
        b.AddBoltSelector(br, "Parafuso na rígida", nameof(SagRodParams.RigidBoltStandard), nameof(SagRodParams.RigidBoltGrade), nameof(SagRodParams.RigidBoltDiameter), nameof(SagRodParams.RigidBoltSet));
        Int(br, "1 - Nº de parafusos", nameof(SagRodParams.RigidBoltCount));
        Num(br, "2 - Espaçamento", nameof(SagRodParams.RigidBoltSpacing));
        Num(br, "4 - Folga do furo", nameof(SagRodParams.RigidBoltTolerance));
    }

    // ------------------------------------------------------------------ criação

    public void CreateObjects()
    {
        if (DatabaseManager.Open(JointId) is not IJoint jnt) return;
        var created = new List<CreatedObjectInformation>();
        bool ok = true;
        try
        {
            List<Beam> inputs = jnt.InputObjects.OfType<Beam>().ToList();
            List<Beam> frames = _frames == 2 && inputs.Count >= 2 ? inputs.Take(2).ToList() : new List<Beam>();
            List<Beam> purlinBeams = inputs.Skip(frames.Count).ToList();
            Log($"--- recalculando: {purlinBeams.Count} terça(s), {frames.Count} banzo(s)");
            if (frames.Count == 2) SetRoofFrameFromChords(frames[0], frames[1]);
            else if (purlinBeams.Count > 0) SetRoofFrame(purlinBeams[0], frames);
            // Só vigas ao longo das terças (paralelas ao X do telhado); diagonais, montantes e o próprio banzo ficam de fora.
            purlinBeams = purlinBeams.Where(t =>
            {
                V3 d = V(t.GetPointAtEnd()) - V(t.GetPointAtStart());
                bool along = d.Length > 1 && Math.Abs(V3.Dot(d.Unit, _x)) > 0.95;
                if (!along) Log($"viga {t.Handle} ({t.ProfSectionName}) ignorada: não está ao longo das terças");
                return along;
            }).ToList();
            if (purlinBeams.Count < 2) { ok = false; Log("menos de 2 terças entre os banzos"); }
            else
            {
                var sections = new List<(PurlinSection s, Beam beam)>();
                for (int i = 0; i < purlinBeams.Count; i++)
                    if (MeasurePurlin(purlinBeams[i], i) is { } s) sections.Add((s, purlinBeams[i]));
                sections = sections.OrderBy(x => x.s.WebY).ToList();
                var purlins = sections.Select((x, i) => x.s with { Tag = i }).ToList();
                var beams = sections.Select(x => x.beam).ToList();

                double xA, xB, faceA, faceB;
                if (frames.Count == 2 && FrameX(frames[0]) is { } fa && FrameX(frames[1]) is { } fb)
                { xA = fa.axis; xB = fb.axis; faceA = fa.max; faceB = fb.min; }
                else
                {
                    xA = purlins.Max(s => Math.Min(s.X0, s.X1)); xB = purlins.Min(s => Math.Max(s.X0, s.X1));
                    faceA = xA; faceB = xB;
                }
                Log($"pórticos: A {xA:0.#} (face {faceA:0.#}), B {xB:0.#} (face {faceB:0.#})");

                AngleSize rigid = MeasureAngle(_p.RigidProfile), cleat = MeasureAngle(_p.CleatProfile);
                SagResult res = SagRodGeometry.Build(purlins, xA, xB, faceA, faceB, _p, rigid, cleat);
                foreach (string err in res.Errors) Log("erro: " + err);
                foreach (string note in res.Notes) Log("aviso: " + note);
                Log($"filas em {string.Join(" ", res.Rows.Select(x => x.ToString("0.#")))}; peças: {res.Parts.Count}");
                if (res.Errors.Count > 0) ok = false;
                else { ok = Create(res, beams, created); SaveLastSettings(); }
            }
        }
        catch (Exception ex) { ok = false; Log("falha: " + ex); }
        jnt.SetCreatedObjects(created, ok);
    }

    /// <summary>
    /// Sistema do telhado a partir da 1ª terça: X ao longo dela (de A para B), Z na altura da alma (para cima),
    /// Y = subindo o telhado, perpendicular às terças.
    /// </summary>
    private void SetRoofFrame(Beam first, List<Beam> frames)
    {
        V3 s = V(first.GetPointAtStart()), e = V(first.GetPointAtEnd());
        _x = (e - s).Unit;
        AsMatrix m = first.GetMatProfToWCS();
        V3 o = V(new AsPoint(0, 0, 0).TransformBy(m)), px = V(new AsPoint(1, 0, 0).TransformBy(m)) - o, py = V(new AsPoint(0, 1, 0).TransformBy(m)) - o;
        using ProfileType prof = first.GetProfType();
        prof.GetGeomExtents(out AsPoint min, out AsPoint max);
        V3 h = (max.y - min.y) >= (max.x - min.x) ? py : px;       // seção no plano X-Y do perfil; a maior medida é a altura da alma
        h = (h - _x * V3.Dot(h, _x)).Unit;
        if (h.Z < 0) h = h * -1;
        _z = h;
        _y = V3.Cross(_z, _x).Unit;
        if (_y.Z < -1e-6) _y = _y * -1;
        _o = s;
        if (frames.Count == 2)
        {
            double ax = V3.Dot(Mid(frames[0]) - _o, _x), bx = V3.Dot(Mid(frames[1]) - _o, _x);
            if (bx < ax) _x = _x * -1;
        }
        Log($"telhado: X {_x} Y {_y} Z {_z}");
    }

    /// <summary>
    /// Sistema do telhado a partir dos dois banzos: Y = ao longo do 1º banzo, subindo; X = do banzo A para o B,
    /// perpendicular a Y; Z = normal ao telhado, para cima. A origem fica no meio do banzo A.
    /// </summary>
    private void SetRoofFrameFromChords(Beam a, Beam b)
    {
        V3 ya = (V(a.GetPointAtEnd()) - V(a.GetPointAtStart())).Unit;
        if (ya.Z < 0) ya = ya * -1;
        V3 d = Mid(b) - Mid(a);
        V3 x = d - ya * V3.Dot(d, ya);
        _x = x.Length > 1 ? x.Unit : V3.Cross(ya, new V3(0, 0, 1)).Unit;
        _y = ya;
        _z = V3.Cross(_x, _y).Unit;
        if (_z.Z < 0) _z = _z * -1;
        _o = Mid(a);
        Log($"telhado pelos banzos: X {_x} Y {_y} Z {_z}, distância entre banzos {V3.Dot(d, _x):0.#}");
    }

    private static V3 Mid(Beam b) => (V(b.GetPointAtStart()) + V(b.GetPointAtEnd())) * 0.5;

    /// <summary>Mede a terça no sistema do telhado: alma (lado e espessura), altura e trecho em X.</summary>
    private PurlinSection? MeasurePurlin(Beam beam, int tag)
    {
        try
        {
            List<(double y, double z)> sec = Section(beam);
            double y0 = sec.Min(q => q.y), y1 = sec.Max(q => q.y), z0 = sec.Min(q => q.z), z1 = sec.Max(q => q.z);
            const double tol = 0.5;
            // Alma = lado com uma aresta reta (contínua) cobrindo quase toda a altura. Nas pontas das abas de um U
            // os vértices também chegam em cima e embaixo, mas em arestas separadas.
            double Span(double side)
            {
                double best = 0;
                for (int i = 0; i < sec.Count; i++)
                {
                    var (p, q) = (sec[i], sec[(i + 1) % sec.Count]);
                    if (Math.Abs(p.y - side) < tol && Math.Abs(q.y - side) < tol) best = Math.Max(best, Math.Abs(p.z - q.z));
                }
                return best;
            }
            double h = z1 - z0, sMin = Span(y0), sMax = Span(y1);
            double webY, webT;
            var inner = sec.Where(q => q.y > y0 + tol && q.y < y1 - tol).Select(q => q.y).ToList();
            Log($"terça {tag}: seção {sec.Count} vértices, aresta reta Y- {sMin:0.#} / Y+ {sMax:0.#} de {h:0.#}");
            if (sMin > 0.7 * h && sMax < 0.5 * h || sMax > 0.7 * h && sMin < 0.5 * h)
            {
                // Perfil C/U/Z: alma num dos lados.
                bool atMin = sMin > sMax;
                double side = atMin ? y0 : y1;
                double innerY = inner.Count == 0 ? side + (atMin ? 2 : -2) : (atMin ? inner.Min() : inner.Max());
                webT = Math.Abs(innerY - side);
                if (webT > 30 || webT < 0.3) webT = 2;
                webY = side + (atMin ? webT / 2 : -webT / 2);
            }
            else
            {
                // Perfil simétrico (I, tubo): alma no meio.
                webY = (y0 + y1) / 2;
                var near = inner.Where(y => Math.Abs(y - webY) < (y1 - y0) / 4).ToList();
                webT = near.Count >= 2 ? near.Max() - near.Min() : 5;
            }
            double xs = V3.Dot(V(beam.GetPointAtStart()) - _o, _x), xe = V3.Dot(V(beam.GetPointAtEnd()) - _o, _x);
            var ps = new PurlinSection(tag, webY, webT, z0, z1, Math.Min(xs, xe), Math.Max(xs, xe));
            Log($"terça {tag} ({beam.ProfSectionName}): alma Y {webY:0.#} t {webT:0.##}, Z {z0:0.#}..{z1:0.#}, X {ps.X0:0.#}..{ps.X1:0.#}");
            return ps;
        }
        catch (Exception ex) { Log($"terça {tag}: falhou ao medir: {ex.Message}"); return null; }
    }

    /// <summary>Contorno da seção da viga no plano Y-Z do telhado.</summary>
    private List<(double y, double z)> Section(Beam beam)
    {
        using ProfileType prof = beam.GetProfType();
        AsMatrix m = beam.GetMatProfToWCS();
        if (prof.GetOuterAugPolygon(false, 0.1, false, out Autodesk.AdvanceSteel.Geometry.Polyline3d poly) && poly != null)
        {
            poly.GetApproxVertices(out AsPoint[] verts);
            if (verts.Length >= 3)
                return verts.Select(v => { V3 q = V(v.TransformBy(m)) - _o; return (V3.Dot(q, _y), V3.Dot(q, _z)); }).ToList();
        }
        prof.GetGeomExtents(out AsPoint min, out AsPoint max);
        var list = new List<(double, double)>();
        foreach (double y in new[] { min.y, max.y })
            foreach (double x in new[] { min.x, max.x })
            {
                V3 q = V(new AsPoint(x, y, 0).TransformBy(m)) - _o;
                list.Add((V3.Dot(q, _y), V3.Dot(q, _z)));
            }
        return list;
    }

    /// <summary>Posição do pórtico ao longo das terças: eixo e faces (menor e maior X da seção).</summary>
    private (double axis, double min, double max)? FrameX(Beam beam)
    {
        try
        {
            double ax = V3.Dot(Mid(beam) - _o, _x);
            using ProfileType prof = beam.GetProfType();
            prof.GetGeomExtents(out AsPoint min, out AsPoint max);
            AsMatrix m = beam.GetMatProfToWCS();
            double lo = double.MaxValue, hi = double.MinValue;
            foreach (double y in new[] { min.y, max.y })
                foreach (double px in new[] { min.x, max.x })
                {
                    double x = V3.Dot(V(new AsPoint(px, y, 0).TransformBy(m)) - _o, _x);
                    lo = Math.Min(lo, x); hi = Math.Max(hi, x);
                }
            return (ax, Math.Min(lo, ax), Math.Max(hi, ax));
        }
        catch (Exception ex) { Log("pórtico: falhou ao medir: " + ex.Message); return null; }
    }

    /// <summary>Abas e espessura de uma cantoneira do catálogo (viga provisória, fora do desenho). Padrão: L2x2x1/8.</summary>
    private static AngleSize MeasureAngle(string profile)
    {
        try
        {
            using var tmp = new StraightBeam(profile, new AsPoint(0, 0, 0), new AsPoint(100, 0, 0), new AsVector(0, 0, 1));
            using ProfileType prof = tmp.GetProfType();
            if (prof.GetOuterAugPolygon(false, 0.1, false, out Autodesk.AdvanceSteel.Geometry.Polyline3d poly) && poly != null)
            {
                poly.GetApproxVertices(out AsPoint[] v);
                // Contorno no sistema do perfil: seção no plano X-Y.
                double y0 = v.Min(q => q.x), y1 = v.Max(q => q.x), z0 = v.Min(q => q.y), z1 = v.Max(q => q.y);
                var inner = v.Where(q => q.x > y0 + 0.5 && q.x < y1 - 0.5 && q.y > z0 + 0.5 && q.y < z1 - 0.5).ToList();
                if (inner.Count == 1)
                {
                    double ty = Math.Min(inner[0].x - y0, y1 - inner[0].x), tz = Math.Min(inner[0].y - z0, z1 - inner[0].y);
                    return new AngleSize(y1 - y0, z1 - z0, Math.Min(ty, tz));
                }
            }
        }
        catch (Exception ex) { Log($"cantoneira {profile}: não deu para medir ({ex.Message}); usando 50.8 x 3.175"); }
        return new AngleSize(50.8, 50.8, 3.175);
    }

    private bool Create(SagResult res, List<Beam> purlins, List<CreatedObjectInformation> created)
    {
        bool ok = true;
        string nutBlock = "", washerBlock = "";
        if (_p.Nuts || _p.Washers)
        {
            try
            {
                NutBlocks.Ensure(Autodesk.AutoCAD.DatabaseServices.HostApplicationServices.WorkingDatabase,
                    _p.NutAcrossFlats, _p.NutHeight, _p.WasherOuterDiameter, _p.WasherInnerDiameter, _p.WasherThickness);
                nutBlock = NutBlocks.NutName(_p.NutAcrossFlats, _p.NutHeight, _p.WasherInnerDiameter);
                washerBlock = NutBlocks.WasherName(_p.WasherOuterDiameter, _p.WasherInnerDiameter, _p.WasherThickness);
            }
            catch (Exception ex) { Log("blocos de porca/arruela: " + ex.Message); }
        }

        var made = new FilerObject?[res.Parts.Count];
        for (int i = 0; i < res.Parts.Count; i++)
        {
            SagPart part = res.Parts[i];
            if (part.Kind is SagPartKind.Bolt or SagPartKind.Weld) continue;
            try
            {
                made[i] = part.Kind switch
                {
                    SagPartKind.Rod or SagPartKind.ThreadedRod => MakeBar(part),
                    SagPartKind.Angle => MakeAngle(part),
                    SagPartKind.Nut => nutBlock.Length > 0 ? MakeSpecial(part, nutBlock) : null,
                    SagPartKind.Washer => washerBlock.Length > 0 ? MakeSpecial(part, washerBlock) : null,
                    SagPartKind.Hole => part.Purlin >= 0 && part.Purlin < purlins.Count ? MakeHole(part, purlins[part.Purlin]) : null,
                    _ => null,
                };
            }
            catch (Exception ex)
            {
                Log($"{part.Id} não criado: {ex.Message}");
                if (part.Kind is SagPartKind.Rod or SagPartKind.Angle) ok = false;
            }
            if (made[i] != null) created.Add(Info(made[i]!, part));
        }

        var location = _p.WeldsInShop ? AtomicElement.eAssemblyLocation.kInShop : AtomicElement.eAssemblyLocation.kOnSite;
        for (int i = 0; i < res.Parts.Count; i++)
        {
            SagPart part = res.Parts[i];
            if (part.Kind is not (SagPartKind.Bolt or SagPartKind.Weld)) continue;
            var objs = part.Connects.Select(k => made[k]).Where(o => o != null).Cast<FilerObject>().ToList();
            if (part.Purlin >= 0 && part.Purlin < purlins.Count) objs.Add(purlins[part.Purlin]);
            if (objs.Count < 2) continue;
            try
            {
                if (part.Kind == SagPartKind.Weld) created.Add(Info(MakeWeld(part, objs.ToArray(), location), part));
                else
                    for (int k = 0; k < Math.Max(1, part.Count); k++)
                    {
                        double off = (k - (part.Count - 1) / 2.0) * part.Spacing;
                        created.Add(Info(MakeBolt(part, part.Origin + part.Across * off, objs.ToArray()), part, k));
                    }
            }
            catch (Exception ex) { Log($"{part.Id} não criado: {ex.Message}"); }
        }
        Log($"criados: {created.Count}");
        return ok;
    }

    private static CreatedObjectInformation Info(FilerObject o, SagPart part, int k = 0) => new()
    {
        CreatedObject = o,
        JointTransferId = k == 0 ? part.Id : $"{part.Id}_{k}",
        Attributes = Array.Empty<eObjectsAttribute>(),
    };

    private FilerObject MakeBar(SagPart part)
    {
        var beam = new StraightBeam(part.Profile, P(part.Start), P(part.End), Vec(new V3(0, 0, 1)));
        beam.WriteToDb();
        if (part.Material.Length > 0) beam.Material = part.Material;
        beam.Role = part.Role;
        return beam;
    }

    /// <summary>
    /// Cantoneira com a quina externa na linha Start→End e as abas saindo para LegA e LegB. Gira a seção de 90 em 90
    /// graus até as abas apontarem para o lado certo (medindo a seção real) e depois encosta a quina na linha.
    /// </summary>
    private FilerObject MakeAngle(SagPart part)
    {
        V3 a = part.LegA.Unit, b = part.LegB.Unit;
        // 1) Testa, com vigas provisórias fora do desenho, qual vetor de orientação deixa as abas para o lado certo
        //    e quanto deslocar. 2) Cria a cantoneira já na posição final, sem girar nem mover depois: assim o
        //    recálculo da ligação dá sempre o mesmo resultado.
        foreach (V3 ov in new[] { a, b, a * -1, b * -1 })
        {
            List<(double u, double v)> sec = Probe(part, ov, a, b);
            if (sec.Count < 3) break;
            if (!FitsL(sec, out double u0, out double v0)) continue;
            V3 shift = a * -u0 + b * -v0;
            var placed = new StraightBeam(part.Profile, P(part.Start + shift), P(part.End + shift), Vec(ov));
            placed.WriteToDb();
            if (part.Material.Length > 0) placed.Material = part.Material;
            placed.Role = part.Role;
            Log($"{part.Id}: orientação {ov}, deslocada {-u0:0.#} / {-v0:0.#}");
            return placed;
        }
        Log($"{part.Id}: vigas provisórias não deram a seção; usando o ajuste por rotação");

        var beam = new StraightBeam(part.Profile, P(part.Start), P(part.End), Vec(new V3(0, 0, 1)));
        beam.WriteToDb();
        if (part.Material.Length > 0) beam.Material = part.Material;
        beam.Role = part.Role;
        foreach (double rot in new[] { 0.0, 90, 180, 270 })
        {
            if (rot != 0) beam.SetXRotation(rot);
            List<(double u, double v)> sec = SectionUV(beam, part.Start, a, b);
            if (sec.Count < 3) break;
            double u0 = sec.Min(q => q.u), u1 = sec.Max(q => q.u), v0 = sec.Min(q => q.v), v1 = sec.Max(q => q.v);
            var inner = sec.Where(q => q.u > u0 + 0.5 && q.u < u1 - 0.5 && q.v > v0 + 0.5 && q.v < v1 - 0.5).ToList();
            bool fits = inner.Count == 1 && inner[0].u - u0 < u1 - inner[0].u && inner[0].v - v0 < v1 - inner[0].v;
            if (!fits && !(inner.Count != 1 && rot == 270)) continue;
            V3 shift = a * -u0 + b * -v0;
            beam.SetStartEndPoints(P(part.Start + shift), P(part.End + shift));
            return beam;
        }
        Log($"{part.Id}: seção não parece uma cantoneira; ficou sem ajuste");
        return beam;
    }

    /// <summary>
    /// Seção de uma viga provisória com o vetor de orientação dado. Primeiro fora do desenho; se o Advance Steel não
    /// der a seção assim, grava a provisória, mede e apaga.
    /// </summary>
    private List<(double u, double v)> Probe(SagPart part, V3 ov, V3 a, V3 b)
    {
        try
        {
            using var probe = new StraightBeam(part.Profile, P(part.Start), P(part.End), Vec(ov));
            var sec = SectionUV(probe, part.Start, a, b);
            if (sec.Count >= 3) return sec;
        }
        catch (Exception) { }
        try
        {
            var probe = new StraightBeam(part.Profile, P(part.Start), P(part.End), Vec(ov));
            probe.WriteToDb();
            var sec = SectionUV(probe, part.Start, a, b);
            DatabaseManager.Erase(new[] { probe.Handle });
            return sec;
        }
        catch (Exception ex) { Log($"{part.Id}: viga provisória falhou: {ex.Message}"); return new(); }
    }

    /// <summary>A seção é uma L com a quina externa no menor u e menor v (abas saindo para +u e +v).</summary>
    private static bool FitsL(List<(double u, double v)> sec, out double u0, out double v0)
    {
        u0 = sec.Min(q => q.u); v0 = sec.Min(q => q.v);
        double u1 = sec.Max(q => q.u), v1 = sec.Max(q => q.v), mu = u0, mv = v0;
        var inner = sec.Where(q => q.u > mu + 0.5 && q.u < u1 - 0.5 && q.v > mv + 0.5 && q.v < v1 - 0.5).ToList();
        return inner.Count == 1 && inner[0].u - u0 < u1 - inner[0].u && inner[0].v - v0 < v1 - inner[0].v;
    }

    private List<(double u, double v)> SectionUV(Beam beam, V3 origin, V3 a, V3 b)
    {
        using ProfileType prof = beam.GetProfType();
        if (!prof.GetOuterAugPolygon(false, 0.1, false, out Autodesk.AdvanceSteel.Geometry.Polyline3d poly) || poly == null) return new();
        poly.GetApproxVertices(out AsPoint[] verts);
        AsMatrix m = beam.GetMatProfToWCS();
        return verts.Select(v => { V3 q = ToLocal(V(v.TransformBy(m))) - origin; return (V3.Dot(q, a), V3.Dot(q, b)); }).ToList();
    }

    private FilerObject MakeHole(SagPart part, Beam purlin)
    {
        V3 z = part.Axis.Unit, x = part.Across.Unit;
        V3 y = V3.Cross(z, x).Unit;
        var cs = new AsMatrix();
        cs.SetCoordSystem(P(part.Origin), Vec(x), Vec(y), Vec(z));
        var hole = new ConnectionHoleBeam(purlin, cs, Beam.eEnd.kStart);
        hole.Arranger = new RectangularArranger(new AsMatrix2(), 0, 0, 1, 1);
        hole.Hole = part.Slotted ? new SlottedHole(part.Size, part.Length, SlottedHole.eDirection.kXAxis) : new Hole(part.Size);
        purlin.AddFeature(hole);
        return hole;
    }

    private FilerObject MakeBolt(SagPart part, V3 at, FilerObject[] objs)
    {
        V3 ax = part.Axis.Unit, u = part.Across.Unit;
        V3 w = V3.Cross(ax, u).Unit;
        var bolt = new FinitRectScrewBoltPattern(P(at), P(at), Vec(u), Vec(w));
        bolt.Nx = 1; bolt.Ny = 1;
        bool rigid = part.BoltGroup == 1;
        string std = rigid ? _p.RigidBoltStandard : _p.BoltStandard, grade = rigid ? _p.RigidBoltGrade : _p.BoltGrade, set = rigid ? _p.RigidBoltSet : _p.BoltSet;
        if (std.Length > 0) bolt.Standard = std;
        if (grade.Length > 0) bolt.Grade = grade;
        if (set.Length > 0) bolt.BoltAssembly = set;
        bolt.ScrewDiameter = rigid ? _p.RigidBoltDiameter : _p.BoltDiameter;
        bolt.WriteToDb();
        try { bolt.HoleTolerance = part.Tolerance; } catch (Exception) { }
        bolt.Connect(objs, AtomicElement.eAssemblyLocation.kOnSite);
        return bolt;
    }

    private FilerObject MakeWeld(SagPart part, FilerObject[] objs, AtomicElement.eAssemblyLocation location)
    {
        V3 x = part.Axis.Unit;
        V3 y = Math.Abs(V3.Dot(x, new V3(0, 0, 1))) > 0.9 ? new V3(1, 0, 0) : V3.Cross(new V3(0, 0, 1), x).Unit;
        var weld = new WeldPoint(P(part.Origin), Vec(x), Vec(y));
        weld.WriteToDb();
        weld.Thickness = part.Size;
        weld.Connect(objs, location);
        return weld;
    }

    private FilerObject MakeSpecial(SagPart part, string block)
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

    internal static void Log(string msg)
    {
        try
        {
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PASB");
            Directory.CreateDirectory(dir);
            string f = Path.Combine(dir, "antidobra.log");
            if (File.Exists(f) && new FileInfo(f).Length > 1_000_000) File.Delete(f);
            File.AppendAllText(f, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss ") + msg + Environment.NewLine);
        }
        catch (Exception) { }
    }

    // ------------------------------------------------------------------ sistema do telhado <-> mundo

    private V3 ToLocal(V3 w) { V3 d = w - _o; return new V3(V3.Dot(d, _x), V3.Dot(d, _y), V3.Dot(d, _z)); }
    private V3 ToWorld(V3 l) => _o + _x * l.X + _y * l.Y + _z * l.Z;
    private V3 DirToWorld(V3 l) => _x * l.X + _y * l.Y + _z * l.Z;
    private AsPoint P(V3 local) { V3 w = ToWorld(local); return new AsPoint(w.X, w.Y, w.Z); }
    private AsVector Vec(V3 local) { V3 w = DirToWorld(local); return new AsVector(w.X, w.Y, w.Z); }
    private static V3 V(AsPoint p) => new(p.x, p.y, p.z);
}
