using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Pasb.Connector;

// =====================================================================================================================
// Sistema antidobra (correntes de terça): agulhas (barras lisas com pontas roscadas entre as almas das terças),
// barras rígidas (cantoneiras com cantoneiras de ligação parafusadas na alma) e tirantes diagonais.
//
// Toda a geometria é calculada no "sistema do telhado":
//   X = ao longo das terças, do pórtico A (x = XA) para o pórtico B (x = XB > XA);
//   Y = perpendicular às terças no plano do telhado, subindo (terça 0 embaixo);
//   Z = normal ao telhado (altura da alma da terça).
// A ligação nativa (Pasb.Joints.SagRodJoint) mede as terças e os pórticos, chama Build e cria as peças.
// =====================================================================================================================

public enum SagRowMode { Count, Spacing }
public enum SagSpanBase { Axes, Faces }
public enum SagSplice { Butt, Lap }
public enum SagNutSide { Before, After, Both }
public enum SagRodHeight { WebCenter, FromTopFlange }
public enum SagHoleDirection { AlongPurlin, AlongWeb }
public enum SagRigidPosition { TopDefault, TopOther, BottomDefault, BottomOther }
public enum SagCleatSide { Outside, Inside }
public enum SagCleatLink { Welded, Bolted }
public enum SagTieDirection { Up, Down, Cross }

/// <summary>Parâmetros do sistema antidobra (abas da ligação). Medidas em mm.</summary>
public sealed class SagRodParams
{
    public int SchemaVersion { get; set; } = 1;

    // 1 - Filas
    public SagRowMode RowMode { get; set; } = SagRowMode.Count;
    public int RowCount { get; set; } = 3;
    public double RowSpacing { get; set; } = 1500;
    public SagSpanBase SpanBase { get; set; } = SagSpanBase.Axes;

    // 2 - Entreterças (1 = entre a 1ª e a 2ª terça, de baixo para cima; T = a de cima)
    public string RigidBays { get; set; } = "T";
    public string NoRodBays { get; set; } = "";

    // 3 - Barra da agulha
    public string RodProfile { get; set; } = "AISC RoundBar#@§@#RB 3/8";
    public string RodMaterial { get; set; } = "ASTMA36";
    public string RodThreadProfile { get; set; } = "";
    public string RodThreadMaterial { get; set; } = "";
    public double RodDiameter { get; set; } = 9.525;
    public double RodThreadLength { get; set; } = 100;
    public double RodExtraBefore { get; set; } = 0;
    public double RodExtraAfter { get; set; } = 0;
    public SagSplice RodSplice { get; set; } = SagSplice.Butt;
    public double RodLapWeld { get; set; } = 0;
    public double RodLapGap { get; set; } = 0;
    public double RodLapClearance { get; set; } = 50;

    // 4 - Porcas e arruelas (blocos PASB)
    public SagNutSide NutSide { get; set; } = SagNutSide.Both;
    public bool Nuts { get; set; } = true;
    public bool Washers { get; set; } = true;
    public double NutAcrossFlats { get; set; } = 17.5;
    public double NutHeight { get; set; } = 9.1;
    public double WasherOuterDiameter { get; set; } = 20.6;
    public double WasherInnerDiameter { get; set; } = 11.1;
    public double WasherThickness { get; set; } = 3.4;

    // 5 - Agulha dupla
    public bool DoubleCrossed { get; set; } = false;
    public double DoubleTopOffset { get; set; } = 50;
    public double DoubleBottomOffset { get; set; } = 50;

    // 6 - Barra rígida
    public string RigidProfile { get; set; } = "AISC 15.0 Angle equal#@§@#L2X2X1/8";
    public string RigidMaterial { get; set; } = "ASTMA36";
    public double RigidClearance { get; set; } = 2;
    public double RigidFaceHeight { get; set; } = 0;
    public SagRigidPosition RigidPosition { get; set; } = SagRigidPosition.TopDefault;
    public double RigidShift { get; set; } = 0;

    // 7 - Cantoneira de ligação
    public string CleatProfile { get; set; } = "AISC 15.0 Angle equal#@§@#L2X2X1/8";
    public string CleatMaterial { get; set; } = "ASTMA36";
    public double CleatLength { get; set; } = 100;
    public double CleatBackToBolt { get; set; } = 28;
    public SagCleatSide CleatSide { get; set; } = SagCleatSide.Outside;
    public SagCleatLink CleatLink { get; set; } = SagCleatLink.Welded;
    public double CleatEndToBolt { get; set; } = 35;
    public double WeldSize { get; set; } = 4;
    public bool WeldsInShop { get; set; } = true;

    // 8 - Onde vai o tirante
    public string TieSpans { get; set; } = "1 2 3 4";
    public string TieBays { get; set; } = "T";
    public SagTieDirection TieDirection { get; set; } = SagTieDirection.Up;
    public string TieDirections { get; set; } = "S S D D";
    public double TieFromRow { get; set; } = 100;
    public double TieFromFrame { get; set; } = 100;

    // 9 - Barra e ponta do tirante
    public bool Ties { get; set; } = true;
    public string TieProfile { get; set; } = "AISC RoundBar#@§@#RB 3/8";
    public string TieMaterial { get; set; } = "ASTMA36";
    public string TieThreadProfile { get; set; } = "";
    public string TieThreadMaterial { get; set; } = "";
    public double TieThreadLength { get; set; } = 75;
    public double TieThreadBehindWeb { get; set; } = 20;

    // 10 - Furos da agulha
    public bool RodHoleSlotted { get; set; } = false;
    public double RodHoleDiameter { get; set; } = 10.82;
    public double RodHoleLength { get; set; } = 30;
    public SagHoleDirection RodHoleDirection { get; set; } = SagHoleDirection.AlongPurlin;
    public SagRodHeight RodHeight { get; set; } = SagRodHeight.WebCenter;
    public double RodHeightFromFlange { get; set; } = 50;
    public double OffsetEvenBay { get; set; } = -40;
    public double OffsetOddBay { get; set; } = 40;
    public bool DoubleHoleSlotted { get; set; } = true;
    public double DoubleHoleDiameter { get; set; } = 10.82;
    public double DoubleHoleLength { get; set; } = 30;
    public SagHoleDirection DoubleHoleDirection { get; set; } = SagHoleDirection.AlongWeb;

    // 11 - Furos do tirante
    public bool TieHoleSlotted { get; set; } = false;
    public double TieHoleDiameter { get; set; } = 10.82;
    public double TieHoleLength { get; set; } = 30;
    public SagHoleDirection TieHoleDirection { get; set; } = SagHoleDirection.AlongPurlin;

    // 12 - Parafusos na terça (cantoneira de ligação x alma)
    public string BoltStandard { get; set; } = "ASTM A325";
    public string BoltGrade { get; set; } = "10.9";
    public double BoltDiameter { get; set; } = 12.7;
    public string BoltSet { get; set; } = "NaW";
    public int PurlinBoltCount { get; set; } = 1;
    public double PurlinBoltSpacing { get; set; } = 40;
    public double PurlinBoltTolerance { get; set; } = 1.3;

    // 13 - Parafusos na rígida (só com a cantoneira parafusada na rígida)
    public string RigidBoltStandard { get; set; } = "ASTM A325";
    public string RigidBoltGrade { get; set; } = "10.9";
    public double RigidBoltDiameter { get; set; } = 12.7;
    public string RigidBoltSet { get; set; } = "NaW";
    public int RigidBoltCount { get; set; } = 1;
    public double RigidBoltSpacing { get; set; } = 40;
    public double RigidBoltTolerance { get; set; } = 1.3;

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true, Converters = { new JsonStringEnumConverter() } };
    public string ToJson() => JsonSerializer.Serialize(this, Json);
    public static SagRodParams FromJson(string json) => JsonSerializer.Deserialize<SagRodParams>(json, Json) ?? new SagRodParams();
}

/// <summary>Terça medida no sistema do telhado: plano médio da alma (Y), espessura da alma, altura (Z0..Z1) e trecho em X.</summary>
public sealed record PurlinSection(int Tag, double WebY, double WebThickness, double Z0, double Z1, double X0, double X1)
{
    public double Height => Z1 - Z0;
}

/// <summary>Medidas de uma cantoneira (abas e espessura), para encostar as peças umas nas outras.</summary>
public readonly record struct AngleSize(double LegA, double LegB, double Thickness);

public enum SagPartKind { Rod, ThreadedRod, Angle, Nut, Washer, Bolt, Weld, Hole }

/// <summary>Peça do sistema antidobra, no sistema do telhado.</summary>
public sealed class SagPart
{
    public SagPartKind Kind { get; init; }
    /// <summary>Identificador estável (vira o JointTransferId; mesma peça mantém o mesmo nome ao recalcular).</summary>
    public string Id { get; init; } = "";
    public string Role { get; init; } = "";
    public string Profile { get; init; } = "";
    public string Material { get; init; } = "";
    /// <summary>Barras: eixo. Cantoneiras: linha da quina externa (calcanhar).</summary>
    public V3 Start { get; init; }
    public V3 End { get; init; }
    /// <summary>Cantoneiras: direção para onde cada aba sai da quina.</summary>
    public V3 LegA { get; init; }
    public V3 LegB { get; init; }
    /// <summary>Porca, arruela, parafuso, furo e solda: centro e eixo.</summary>
    public V3 Origin { get; init; }
    public V3 Axis { get; init; }
    /// <summary>Parafusos: direção da fileira. Furos oblongos: direção do oblongo.</summary>
    public V3 Across { get; init; }
    public double Size { get; init; }
    public double Length { get; init; }
    public double Tolerance { get; init; }
    public int Count { get; init; } = 1;
    public double Spacing { get; init; }
    public bool Slotted { get; init; }
    /// <summary>Parafusos: 0 = terça, 1 = rígida (escolhe o conjunto de parafuso).</summary>
    public int BoltGroup { get; init; }
    /// <summary>Terça envolvida (furo na alma, parafuso ou solda na terça); -1 = nenhuma.</summary>
    public int Purlin { get; init; } = -1;
    public int[] Connects { get; init; } = Array.Empty<int>();
}

public sealed class SagResult
{
    public List<SagPart> Parts { get; } = new();
    public List<string> Errors { get; } = new();
    public List<string> Notes { get; } = new();
    public List<double> Rows { get; } = new();
}

public static class SagRodGeometry
{
    private static readonly V3 Ux = new(1, 0, 0), Uy = new(0, 1, 0), Uz = new(0, 0, 1);

    /// <summary>
    /// Monta o sistema antidobra. <paramref name="purlins"/> em ordem de baixo para cima (Y crescente).
    /// XA/XB: eixos dos pórticos; FaceA/FaceB: faces internas (iguais aos eixos se não houver pórtico medido).
    /// </summary>
    public static SagResult Build(IReadOnlyList<PurlinSection> purlins, double xA, double xB, double faceA, double faceB,
        SagRodParams p, AngleSize rigid, AngleSize cleat)
    {
        var r = new SagResult();
        Validate(purlins, xA, xB, p, r.Errors);
        if (r.Errors.Count > 0) return r;

        double s0 = p.SpanBase == SagSpanBase.Faces ? faceA : xA, s1 = p.SpanBase == SagSpanBase.Faces ? faceB : xB;
        r.Rows.AddRange(Rows(s0, s1, p));
        int bays = purlins.Count - 1;
        HashSet<int> rigidBays = ParseBays(p.RigidBays, bays), noRod = ParseBays(p.NoRodBays, bays);

        for (int i = 0; i < r.Rows.Count; i++)
            for (int k = 1; k <= bays; k++)
            {
                double x = r.Rows[i] + (k % 2 == 1 ? p.OffsetOddBay : p.OffsetEvenBay);
                PurlinSection lo = purlins[k - 1], up = purlins[k];
                if (!Covers(lo, x) || !Covers(up, x)) { r.Notes.Add($"fila {i + 1}, entreterça {k}: fora do trecho das terças"); continue; }
                string tag = $"{i + 1}_{k}";
                if (rigidBays.Contains(k)) Rigid(r, p, lo, up, k - 1, k, x + p.RigidShift, rigid, cleat, tag);
                else if (!noRod.Contains(k))
                {
                    if (p.DoubleCrossed)
                    {
                        Needle(r, p, lo, up, k - 1, k, x, lo.Z1 - p.DoubleTopOffset, up.Z0 + p.DoubleBottomOffset, true, tag + "_a");
                        Needle(r, p, lo, up, k - 1, k, x + p.RodDiameter + 2, lo.Z0 + p.DoubleBottomOffset, up.Z1 - p.DoubleTopOffset, true, tag + "_b");
                    }
                    else Needle(r, p, lo, up, k - 1, k, x, RodZ(lo, p), RodZ(up, p), false, tag);
                }
            }

        if (p.Ties) Ties(r, p, purlins, s0, s1);
        return r;
    }

    private static void Validate(IReadOnlyList<PurlinSection> purlins, double xA, double xB, SagRodParams p, List<string> e)
    {
        if (purlins.Count < 2) e.Add("Selecione pelo menos 2 terças.");
        if (!(xB - xA > 100)) e.Add("Os pórticos A e B precisam estar separados ao longo das terças.");
        if (p.RowMode == SagRowMode.Count && p.RowCount < 0) e.Add("Nº de fileiras não pode ser negativo.");
        if (p.RowMode == SagRowMode.Spacing && !(p.RowSpacing > 50)) e.Add("Espaçamento das fileiras deve ser maior que 50 mm.");
        if (!(p.RodDiameter > 0)) e.Add("Diâmetro da barra deve ser positivo.");
        if (!(p.RodThreadLength > 0)) e.Add("Rosca da agulha deve ser positiva.");
        if (p.Ties && !(p.TieThreadLength > 0)) e.Add("Rosca da ponta do tirante deve ser positiva.");
        if (!(p.CleatLength > 0)) e.Add("Comprimento da cantoneira de ligação deve ser positivo.");
        if (p.PurlinBoltCount < 0 || p.RigidBoltCount < 0) e.Add("Nº de parafusos não pode ser negativo.");
        for (int i = 1; i < purlins.Count; i++)
            if (purlins[i].WebY - purlins[i - 1].WebY < purlins[i].WebThickness + purlins[i - 1].WebThickness + 10)
                e.Add($"As terças {i} e {i + 1} estão muito próximas ou fora de ordem.");
    }

    /// <summary>Posição das fileiras entre s0 e s1: N fileiras iguais, ou espaçamento máximo dado.</summary>
    public static List<double> Rows(double s0, double s1, SagRodParams p)
    {
        int n = p.RowMode == SagRowMode.Count ? p.RowCount : Math.Max(0, (int)Math.Ceiling((s1 - s0) / p.RowSpacing - 1e-9) - 1);
        var list = new List<double>();
        for (int i = 1; i <= n; i++) list.Add(s0 + (s1 - s0) * i / (n + 1));
        return list;
    }

    /// <summary>Lê listas como "1 3 T" (T = entreterça de cima; * = todas). Números fora da faixa são ignorados.</summary>
    public static HashSet<int> ParseBays(string text, int bays)
    {
        var set = new HashSet<int>();
        foreach (string tok in (text ?? "").Split(new[] { ' ', ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
        {
            string t = tok.Trim().ToUpperInvariant();
            if (t == "T") set.Add(bays);
            else if (t == "*") for (int k = 1; k <= bays; k++) set.Add(k);
            else if (int.TryParse(t, NumberStyles.Integer, CultureInfo.InvariantCulture, out int k) && k >= 1 && k <= bays) set.Add(k);
        }
        return set;
    }

    private static bool Covers(PurlinSection s, double x) => x >= Math.Min(s.X0, s.X1) - 1 && x <= Math.Max(s.X0, s.X1) + 1;
    private static double RodZ(PurlinSection s, SagRodParams p) => p.RodHeight == SagRodHeight.FromTopFlange ? s.Z1 - p.RodHeightFromFlange : (s.Z0 + s.Z1) / 2;

    // ------------------------------------------------------------------------------------------------ agulha

    /// <summary>Agulha entre a terça de baixo (lo) e a de cima (up): barra lisa, pontas roscadas, porcas, arruelas e furos.</summary>
    private static void Needle(SagResult r, SagRodParams p, PurlinSection lo, PurlinSection up, int iLo, int iUp,
        double x, double zLo, double zUp, bool isDouble, string tag)
    {
        V3 a = new(x, lo.WebY, zLo), b = new(x, up.WebY, zUp);
        V3 d = (b - a).Unit;
        double dy = d.Y;                                            // quanto Y anda por mm de barra
        V3 AtY(double y) => a + d * ((y - lo.WebY) / dy);
        double loIn = lo.WebY + lo.WebThickness / 2, loOut = lo.WebY - lo.WebThickness / 2;
        double upIn = up.WebY - up.WebThickness / 2, upOut = up.WebY + up.WebThickness / 2;
        string thrProf = p.RodThreadProfile.Length > 0 ? p.RodThreadProfile : p.RodProfile;
        string thrMat = p.RodThreadMaterial.Length > 0 ? p.RodThreadMaterial : p.RodMaterial;
        const double autoInside = 25;                               // rosca que fica para dentro da alma (sobra automática)
        double phi = p.RodDiameter;

        // Pontas: yOut = ponta de fora da rosca; yIn = onde a rosca encontra a lisa (topo) ou o fim da rosca (traspasse).
        if (p.RodSplice == SagSplice.Butt)
        {
            double inLo = p.RodExtraBefore > 0 ? loOut - p.RodExtraBefore * dy + p.RodThreadLength * dy : loIn + autoInside * dy;
            double outLo = inLo - p.RodThreadLength * dy;
            double inUp = p.RodExtraAfter > 0 ? upOut + p.RodExtraAfter * dy - p.RodThreadLength * dy : upIn - autoInside * dy;
            double outUp = inUp + p.RodThreadLength * dy;
            if (inUp - inLo < 50 * dy) { r.Notes.Add($"agulha {tag}: terças próximas demais para a barra lisa"); return; }
            int t0 = Add(r, new SagPart { Kind = SagPartKind.ThreadedRod, Id = $"Agulha_{tag}_R0", Role = "Agulha roscada", Profile = thrProf, Material = thrMat, Start = AtY(outLo), End = AtY(inLo) });
            int rod = Add(r, new SagPart { Kind = SagPartKind.Rod, Id = $"Agulha_{tag}", Role = "Agulha", Profile = p.RodProfile, Material = p.RodMaterial, Start = AtY(inLo), End = AtY(inUp) });
            int t1 = Add(r, new SagPart { Kind = SagPartKind.ThreadedRod, Id = $"Agulha_{tag}_R1", Role = "Agulha roscada", Profile = thrProf, Material = thrMat, Start = AtY(inUp), End = AtY(outUp) });
            Weld(r, $"Agulha_{tag}_W0", AtY(inLo), d, p.WeldSize, rod, t0);
            Weld(r, $"Agulha_{tag}_W1", AtY(inUp), d, p.WeldSize, rod, t1);
        }
        else
        {
            double weld = p.RodLapWeld > 0 ? p.RodLapWeld : 5 * phi, gap = p.RodLapGap > 0 ? p.RodLapGap : 5 * phi, lap = 2 * weld + gap;
            double sLo = loIn + p.RodLapClearance * dy, sUp = upIn - p.RodLapClearance * dy;
            if (sUp - sLo < 2 * lap * dy) { r.Notes.Add($"agulha {tag}: terças próximas demais para o traspasse"); return; }
            double autoOut = p.WasherThickness + p.NutHeight + phi;
            double outLo = loOut - (p.RodExtraBefore > 0 ? p.RodExtraBefore : autoOut) * dy, outUp = upOut + (p.RodExtraAfter > 0 ? p.RodExtraAfter : autoOut) * dy;
            V3 side = Ux * phi;                                      // rosca ao lado da lisa, encostada
            int rod = Add(r, new SagPart { Kind = SagPartKind.Rod, Id = $"Agulha_{tag}", Role = "Agulha", Profile = p.RodProfile, Material = p.RodMaterial, Start = AtY(sLo), End = AtY(sUp) });
            int t0 = Add(r, new SagPart { Kind = SagPartKind.ThreadedRod, Id = $"Agulha_{tag}_R0", Role = "Agulha roscada", Profile = thrProf, Material = thrMat, Start = AtY(outLo) + side, End = AtY(sLo + lap * dy) + side });
            int t1 = Add(r, new SagPart { Kind = SagPartKind.ThreadedRod, Id = $"Agulha_{tag}_R1", Role = "Agulha roscada", Profile = thrProf, Material = thrMat, Start = AtY(sUp - lap * dy) + side, End = AtY(outUp) + side });
            Weld(r, $"Agulha_{tag}_W0", AtY(sLo + weld / 2 * dy) + side * 0.5, d, p.WeldSize, rod, t0);
            Weld(r, $"Agulha_{tag}_W1", AtY(sLo + (lap - weld / 2) * dy) + side * 0.5, d, p.WeldSize, rod, t0);
            Weld(r, $"Agulha_{tag}_W2", AtY(sUp - weld / 2 * dy) + side * 0.5, d, p.WeldSize, rod, t1);
            Weld(r, $"Agulha_{tag}_W3", AtY(sUp - (lap - weld / 2) * dy) + side * 0.5, d, p.WeldSize, rod, t1);
            a += side; b += side;
        }

        bool slotted = isDouble ? p.DoubleHoleSlotted : p.RodHoleSlotted;
        double hd = isDouble ? p.DoubleHoleDiameter : p.RodHoleDiameter, hl = isDouble ? p.DoubleHoleLength : p.RodHoleLength;
        V3 hdir = (isDouble ? p.DoubleHoleDirection : p.RodHoleDirection) == SagHoleDirection.AlongPurlin ? Ux : Uz;
        V3 ca = a, cb = b;
        Hole(r, $"Agulha_{tag}_F0", ca, Uy, hd, slotted, hl, hdir, iLo);
        Hole(r, $"Agulha_{tag}_F1", cb, Uy, hd, slotted, hl, hdir, iUp);
        V3 Line(double y) => ca + d * ((y - lo.WebY) / dy);
        NutsAt(r, p, $"Agulha_{tag}_P0", Line, lo, d);
        NutsAt(r, p, $"Agulha_{tag}_P1", Line, up, d);
    }

    /// <summary>Arruela encostada na face da alma e porca depois dela, no(s) lado(s) pedido(s). "Antes" = lado de baixo (Y menor).</summary>
    private static void NutsAt(SagResult r, SagRodParams p, string tag, Func<double, V3> line, PurlinSection web, V3 axis)
    {
        double dy = Math.Abs(axis.Y);
        foreach (int side in p.NutSide switch { SagNutSide.Before => new[] { -1 }, SagNutSide.After => new[] { 1 }, _ => new[] { -1, 1 } })
        {
            double face = web.WebY + side * web.WebThickness / 2;
            string s = side < 0 ? "a" : "d";
            if (p.Washers)
                Add(r, new SagPart { Kind = SagPartKind.Washer, Id = $"{tag}{s}_A", Role = "Arruela", Origin = line(face + side * p.WasherThickness / 2 * dy), Axis = axis });
            if (p.Nuts)
            {
                double off = (p.Washers ? p.WasherThickness : 0) + p.NutHeight / 2;
                Add(r, new SagPart { Kind = SagPartKind.Nut, Id = $"{tag}{s}_P", Role = "Porca", Origin = line(face + side * off * dy), Axis = axis });
            }
        }
    }

    // ------------------------------------------------------------------------------------------------ barra rígida

    /// <summary>
    /// Barra rígida (cantoneira) entre as almas, presa em cada terça por uma cantoneira de ligação parafusada na alma.
    /// A cantoneira de ligação tem a aba A encostada na alma (parafuso a <c>CleatBackToBolt</c> das costas) e a aba B
    /// entrando na entreterça; a rígida encosta na aba B por fora (lado do pórtico B) ou por dentro.
    /// </summary>
    private static void Rigid(SagResult r, SagRodParams p, PurlinSection lo, PurlinSection up, int iLo, int iUp,
        double xBolt, AngleSize rg, AngleSize cl, string tag)
    {
        double xBack = xBolt + p.CleatBackToBolt;                    // costas da aba B (lado do pórtico B)
        double loIn = lo.WebY + lo.WebThickness / 2, upIn = up.WebY - up.WebThickness / 2;
        double zfLo = p.RigidFaceHeight > 0 ? lo.Z0 + p.RigidFaceHeight : (lo.Z0 + lo.Z1) / 2;
        double zfUp = p.RigidFaceHeight > 0 ? up.Z0 + p.RigidFaceHeight : (up.Z0 + up.Z1) / 2;

        bool outside = p.CleatSide == SagCleatSide.Outside;
        bool top = p.RigidPosition is SagRigidPosition.TopDefault or SagRigidPosition.TopOther;
        bool other = p.RigidPosition is SagRigidPosition.TopOther or SagRigidPosition.BottomOther;
        // Face da aba B da cantoneira de ligação em que a rígida encosta, e para que lado a rígida fica.
        double contact = outside ? xBack : xBack - cl.Thickness;
        double away = outside ? 1 : -1;                              // a aba vertical da rígida fica deste lado do contato
        double hdir = other ? -away : away;                          // para onde vai a aba horizontal
        double heelX = hdir == away ? contact : contact + away * rg.Thickness;
        V3 legH = Ux * hdir, legV = top ? Uz * -1 : Uz;

        double yA = loIn + p.RigidClearance, yB = upIn - p.RigidClearance;
        if (yB - yA < 100) { r.Notes.Add($"rígida {tag}: terças próximas demais"); return; }
        int rigid = Add(r, new SagPart
        {
            Kind = SagPartKind.Angle, Id = $"Rigida_{tag}", Role = "Barra rígida", Profile = p.RigidProfile, Material = p.RigidMaterial,
            Start = new V3(heelX, yA, zfLo), End = new V3(heelX, yB, zfUp), LegA = legH, LegB = legV,
        });
        double zRigidMidLo = zfLo + legV.Z * rg.LegB / 2, zRigidMidUp = zfUp + legV.Z * rg.LegB / 2;

        foreach (var (web, idx, face, inward, end) in new[] { (lo, iLo, loIn, 1.0, 0), (up, iUp, upIn, -1.0, 1) })
        {
            double zc = (web.Z0 + web.Z1) / 2;
            int cleat = Add(r, new SagPart
            {
                Kind = SagPartKind.Angle, Id = $"Rigida_{tag}_C{end}", Role = "Cantoneira de ligação", Profile = p.CleatProfile, Material = p.CleatMaterial,
                Start = new V3(xBack, face, zc - p.CleatLength / 2), End = new V3(xBack, face, zc + p.CleatLength / 2),
                LegA = Ux * -1, LegB = Uy * inward,
            });
            if (p.PurlinBoltCount > 0)
                Add(r, new SagPart
                {
                    Kind = SagPartKind.Bolt, Id = $"Rigida_{tag}_B{end}", Role = "Parafuso", Origin = new V3(xBolt, web.WebY, zc), Axis = Uy * inward,
                    Across = Uz, Count = p.PurlinBoltCount, Spacing = p.PurlinBoltSpacing, Tolerance = p.PurlinBoltTolerance,
                    BoltGroup = 0, Purlin = idx, Connects = new[] { cleat },
                });
            double yEnd = end == 0 ? yA : yB;
            double zr = end == 0 ? zRigidMidLo : zRigidMidUp;
            if (p.CleatLink == SagCleatLink.Welded)
                Weld(r, $"Rigida_{tag}_W{end}", new V3(contact, yEnd, zr), Uz, p.WeldSize, rigid, cleat);
            else if (p.RigidBoltCount > 0)
                Add(r, new SagPart
                {
                    Kind = SagPartKind.Bolt, Id = $"Rigida_{tag}_BR{end}", Role = "Parafuso", Origin = new V3(contact, face + inward * p.CleatEndToBolt, zr),
                    Axis = Ux * away, Across = Uy * inward, Count = p.RigidBoltCount, Spacing = p.RigidBoltSpacing, Tolerance = p.RigidBoltTolerance,
                    BoltGroup = 1, Connects = new[] { cleat, rigid },
                });
        }
    }

    // ------------------------------------------------------------------------------------------------ tirantes

    private static void Ties(SagResult r, SagRodParams p, IReadOnlyList<PurlinSection> purlins, double s0, double s1)
    {
        var st = new List<double> { s0 };
        st.AddRange(r.Rows);
        st.Add(s1);
        int spans = st.Count - 1, bays = purlins.Count - 1;
        HashSet<int> spanSet = ParseBays(p.TieSpans, spans), baySet = ParseBays(p.TieBays, bays);
        // "T" na lista de vãos não faz sentido; ParseBays trata T como o último vão.
        string[] dirs = (p.TieDirections ?? "").Split(new[] { ' ', ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
        int count = 0;
        foreach (int v in spanSet.OrderBy(x => x))
        {
            double a = st[v - 1] + (v == 1 ? p.TieFromFrame : p.TieFromRow), b = st[v] - (v == spans ? p.TieFromFrame : p.TieFromRow);
            if (b - a < 50) { r.Notes.Add($"tirante no vão {v}: vão curto demais"); continue; }
            foreach (int k in baySet.OrderBy(x => x))
            {
                SagTieDirection dir = p.TieDirection;
                if (count < dirs.Length)
                    dir = dirs[count].ToUpperInvariant() switch { "S" => SagTieDirection.Up, "D" => SagTieDirection.Down, "X" => SagTieDirection.Cross, _ => dir };
                count++;
                PurlinSection lo = purlins[k - 1], up = purlins[k];
                string tag = $"{v}_{k}";
                if (dir is SagTieDirection.Up or SagTieDirection.Cross) Tie(r, p, lo, up, k - 1, k, a, b, tag + "_s");
                if (dir is SagTieDirection.Down or SagTieDirection.Cross) Tie(r, p, lo, up, k - 1, k, b, a, tag + "_d");
            }
        }
    }

    /// <summary>Tirante da terça de baixo (em xLo) à de cima (em xUp): ponta roscada perpendicular à alma, soldada na diagonal lisa.</summary>
    private static void Tie(SagResult r, SagRodParams p, PurlinSection lo, PurlinSection up, int iLo, int iUp, double xLo, double xUp, string tag)
    {
        string thrProf = p.TieThreadProfile.Length > 0 ? p.TieThreadProfile : p.TieProfile;
        string thrMat = p.TieThreadMaterial.Length > 0 ? p.TieThreadMaterial : p.TieMaterial;
        var ends = new[] { (web: lo, idx: iLo, x: xLo, inward: 1.0), (web: up, idx: iUp, x: xUp, inward: -1.0) };
        var inner = new V3[2];
        for (int e = 0; e < 2; e++)
        {
            var (web, _, x, inward) = ends[e];
            double z = RodZ(web, p);
            double face = web.WebY + inward * web.WebThickness / 2;
            inner[e] = new V3(x, face + inward * p.TieThreadBehindWeb, z);
        }
        if ((inner[1] - inner[0]).Length < 100) { r.Notes.Add($"tirante {tag}: curto demais"); return; }
        int rod = Add(r, new SagPart { Kind = SagPartKind.Rod, Id = $"Tirante_{tag}", Role = "Tirante", Profile = p.TieProfile, Material = p.TieMaterial, Start = inner[0], End = inner[1] });
        for (int e = 0; e < 2; e++)
        {
            var (web, idx, x, inward) = ends[e];
            V3 outDir = Uy * -inward;
            int thr = Add(r, new SagPart
            {
                Kind = SagPartKind.ThreadedRod, Id = $"Tirante_{tag}_R{e}", Role = "Tirante roscado", Profile = thrProf, Material = thrMat,
                Start = inner[e], End = inner[e] + outDir * p.TieThreadLength,
            });
            Weld(r, $"Tirante_{tag}_W{e}", inner[e], outDir, p.WeldSize, rod, thr);
            V3 c = new(x, web.WebY, inner[e].Z);
            Hole(r, $"Tirante_{tag}_F{e}", c, Uy, p.TieHoleDiameter, p.TieHoleSlotted, p.TieHoleLength,
                p.TieHoleDirection == SagHoleDirection.AlongPurlin ? Ux : Uz, idx);
            V3 basePt = c;
            NutsAt(r, p, $"Tirante_{tag}_P{e}", y => new V3(basePt.X, y, basePt.Z), web, Uy);
        }
    }

    // ------------------------------------------------------------------------------------------------ utilidades

    private static int Add(SagResult r, SagPart part) { r.Parts.Add(part); return r.Parts.Count - 1; }

    private static void Weld(SagResult r, string id, V3 at, V3 axis, double size, int a, int b) =>
        Add(r, new SagPart { Kind = SagPartKind.Weld, Id = id, Role = "Solda", Origin = at, Axis = axis, Size = size, Connects = new[] { a, b } });

    private static void Hole(SagResult r, string id, V3 at, V3 axis, double dia, bool slotted, double len, V3 slotDir, int purlin) =>
        Add(r, new SagPart { Kind = SagPartKind.Hole, Id = id, Role = "Furo", Origin = at, Axis = axis, Size = dia, Slotted = slotted, Length = len, Across = slotDir, Purlin = purlin });
}
