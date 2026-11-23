using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Pasb.Connector;

/// <summary>Vetor/ponto 3D simples (mm), sem dependência da Autodesk.</summary>
public readonly record struct V3(double X, double Y, double Z)
{
    public static V3 operator +(V3 a, V3 b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    public static V3 operator -(V3 a, V3 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    public static V3 operator *(V3 a, double k) => new(a.X * k, a.Y * k, a.Z * k);
    public double Length => Math.Sqrt(X * X + Y * Y + Z * Z);
    public V3 Unit => this * (1.0 / Length);
    public static V3 Cross(V3 a, V3 b) => new(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);
    public static double Dot(V3 a, V3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
    public override string ToString() => string.Format(CultureInfo.InvariantCulture, "({0:0.###}; {1:0.###}; {2:0.###})", X, Y, Z);
}

public enum BracingDiagonals { X, Diagonal1, Diagonal2 }
public enum TurnbucklePositionMode { Percent, FromCorner1 }
public enum BasePlateAlignment { SupportCorner, Diagonal }
/// <summary>Em modo mm: a distância vai do ponto clicado até a ponta mais próxima do esticador ou até o centro dele.</summary>
public enum TurnbuckleReference { NearEnd, Center }

/// <summary>
/// Parâmetros do contraventamento em X com barra redonda e esticador (mm). Valores padrão tirados do exemplo
/// analisado no desenho do usuário; tudo é editável na janela. Guardados no desenho para "Editar contraventamento".
/// </summary>
public sealed class BracingParams
{
    // Diagonais
    public BracingDiagonals Diagonals { get; set; } = BracingDiagonals.X;

    // Barra (tirante)
    public string RodProfile { get; set; } = "AISC RoundBar#@§@#RB 1/2";
    public double RodDiameter { get; set; } = 12.7;
    public string RodMaterial { get; set; } = "ASTMA36";
    /// <summary>Perfil da barra roscada (vazio = o mesmo da barra).</summary>
    public string ThreadedRodProfile { get; set; } = "";
    public string ThreadedRodMaterial { get; set; } = "";

    // Gusset
    public double GussetThickness { get; set; } = 6.35;
    public double GussetLength { get; set; } = 160;
    public double GussetWidth { get; set; } = 50;
    /// <summary>Distância do ponto clicado até o início do gusset, ao longo da diagonal.</summary>
    public double GussetSetback { get; set; } = 54.5;
    /// <summary>Chanfro dos dois cantos do gusset na ponta do lado do clique (b10) e na ponta oposta.</summary>
    public double GussetEndChamfer { get; set; } = 0;
    public double GussetFarChamfer { get; set; } = 0;
    /// <summary>Comprimento da barra apoiada (e soldada) sobre o gusset.</summary>
    public double RodBearingOnGusset { get; set; } = 100;
    public string PlateMaterial { get; set; } = "ASTMA36";

    // Chapa de base + parafuso
    public bool BasePlate { get; set; } = true;
    /// <summary>Topo da chapa de base abaixo do ponto clicado (0 = no Z do clique).</summary>
    public double BaseTopBelowClick { get; set; } = 0;
    public double BaseThickness { get; set; } = 8;
    public double BaseLength { get; set; } = 150;
    public double BaseWidth { get; set; } = 150;
    /// <summary>Centro da chapa de base, medido do ponto clicado ao longo da diagonal (só com a chapa alinhada à diagonal).</summary>
    public double BaseCenterFromPoint { get; set; } = 105;
    /// <summary>Chapa de base alinhada à viga de apoio (ou à UCS) com o canto no clique, como no SteelX; ou alinhada à diagonal.</summary>
    public BasePlateAlignment BaseAlignment { get; set; } = BasePlateAlignment.SupportCorner;
    /// <summary>Chanfros dos cantos da chapa de base: a4 no canto do clique, a5/a6/a7 nos outros (sentido anti-horário visto de cima).</summary>
    public double BaseChamferClick { get; set; } = 0;
    public double BaseChamfer2 { get; set; } = 0;
    public double BaseChamfer3 { get; set; } = 0;
    public double BaseChamfer4 { get; set; } = 0;
    public bool Bolt { get; set; } = true;
    public double BoltDiameter { get; set; } = 12.7;
    public double BoltLength { get; set; } = 38.1;
    public string BoltStandard { get; set; } = "ASTM A325";
    public string BoltGrade { get; set; } = "10.9";
    public string BoltSet { get; set; } = "NaW";
    /// <summary>Parafuso medido do início do gusset (lado do ponto).</summary>
    public double BoltFromGussetStart { get; set; } = 30;

    // Esticador
    public TurnbucklePositionMode TurnbuckleMode { get; set; } = TurnbucklePositionMode.FromCorner1;
    /// <summary>% do comprimento da diagonal (modo Percent, medido até o centro) ou mm a partir do ponto clicado (modo FromCorner1).</summary>
    public double TurnbucklePosition { get; set; } = 300;
    /// <summary>Modo mm: medir até a ponta mais próxima do esticador (padrão) ou até o centro.</summary>
    public TurnbuckleReference TurnbuckleReference { get; set; } = TurnbuckleReference.NearEnd;
    /// <summary>Modo mm: medir a partir do canto 2 (fim da diagonal) em vez do canto 1.</summary>
    public bool TurnbuckleFromCorner2 { get; set; }
    /// <summary>Versão do formato; configurações salvas antes da 0.10.1 não têm este campo.</summary>
    public int SchemaVersion { get; set; } = 2;
    public string CleatProfile { get; set; } = "AISC 15.0 Angle equal#@§@#L2X2X1/8";
    public double CleatLength { get; set; } = 140;
    public string CleatMaterial { get; set; } = "ASTMA36";
    /// <summary>Rotação da cantoneira em torno do próprio eixo (graus: 0, 90, 180 ou 270).</summary>
    public double CleatRotation { get; set; } = 0;
    public double EndPlateThickness { get; set; } = 4.75;
    public double EndPlateLength { get; set; } = 47;
    public double EndPlateWidth { get; set; } = 45;
    /// <summary>Recuo das chapas de topo para dentro das pontas da cantoneira.</summary>
    public double EndPlateInset { get; set; } = 10;
    public double ThreadLength { get; set; } = 125;
    /// <summary>Quanto a barra roscada passa da chapa de topo para dentro do esticador.</summary>
    public double ThreadPenetration { get; set; } = 35;

    // Emenda barra lisa ↔ barra roscada
    public bool SpliceBars { get; set; } = true;
    public double SpliceLength { get; set; } = 60;
    /// <summary>Perfil das talas (vazio = o mesmo da barra).</summary>
    public string SpliceProfile { get; set; } = "";

    // Porcas e arruelas (blocos próprios do PASB)
    public bool NutsAndWashers { get; set; } = true;
    public double NutAcrossFlats { get; set; } = 22.2;
    public double NutHeight { get; set; } = 12.3;
    public double WasherOuterDiameter { get; set; } = 27;
    public double WasherInnerDiameter { get; set; } = 13.5;
    public double WasherThickness { get; set; } = 3.4;

    // Soldas (cordão, mm)
    public bool WeldRodGussetOn { get; set; } = true;
    public double WeldRodGusset { get; set; } = 5;
    public bool WeldSpliceOn { get; set; } = true;
    public double WeldSplice { get; set; } = 4;
    public bool WeldEndPlateOn { get; set; } = true;
    public double WeldEndPlate { get; set; } = 4;
    /// <summary>Solda da chapa de base (ou do gusset) na viga de apoio escolhida.</summary>
    public bool WeldSupportOn { get; set; } = true;
    public double WeldSupport { get; set; } = 5;
    /// <summary>Soldas de oficina (agrupam no conjunto) ou de montagem.</summary>
    public bool WeldsInShop { get; set; } = true;

    private static readonly JsonSerializerOptions Json = new() { Converters = { new JsonStringEnumConverter() } };
    public string ToJson() => JsonSerializer.Serialize(this, Json);
    public static BracingParams FromJson(string json) => JsonSerializer.Deserialize<BracingParams>(json, Json) ?? new BracingParams();
    public BracingParams Clone() => FromJson(ToJson());
}

// ======================================================================== peças geradas

public enum BracingPartKind { Rod, ThreadedRod, SpliceBar, Cleat, Gusset, BasePlate, EndPlate, Bolt, Weld, Nut, Washer }

/// <summary>Uma peça a criar. Barras: Start/End. Chapas: polígono (4 vértices) + normal + espessura (a chapa fica do lado -normal do plano).
/// Parafuso: Origin + Normal. Solda: Origin + índices das peças que ela liga. Porca/arruela: Origin + Axis (eixo da barra).</summary>
public sealed class BracingPart
{
    public BracingPartKind Kind { get; init; }
    public int Diagonal { get; init; }               // 1 ou 2
    public string Role { get; init; } = "";
    public string Profile { get; init; } = "";
    public string Material { get; init; } = "";
    public V3 Start { get; init; }
    public V3 End { get; init; }
    public V3[] Polygon { get; set; } = Array.Empty<V3>();
    /// <summary>Barra: rotação em graus em torno do eixo.</summary>
    public double Angle { get; init; }
    /// <summary>Solda na estrutura: índice da viga de apoio (em <see cref="SupportStrip.Tag"/>); -1 = nenhuma.</summary>
    public int SupportTag { get; init; } = -1;
    public V3 Normal { get; init; }
    public double Thickness { get; init; }
    public V3 Origin { get; init; }
    public V3 Axis { get; init; }
    public double Size { get; init; }                // solda: cordão; parafuso: diâmetro
    public double Length { get; init; }              // parafuso: comprimento
    public int[] Connects { get; init; } = Array.Empty<int>();
}

/// <summary>
/// Viga de apoio (banzo) vista em planta: eixo passando por <paramref name="Origin"/> na direção <paramref name="Dir"/>,
/// com meia largura do banzo <paramref name="HalfWidth"/>; a seção vai de <paramref name="ZMin"/> a <paramref name="ZMax"/>
/// (cota no plano do contraventamento). <paramref name="Tag"/> identifica a viga para a solda.
/// </summary>
public sealed record SupportStrip(V3 Origin, V3 Dir, double HalfWidth, int Tag,
    double ZMin = double.NegativeInfinity, double ZMax = double.PositiveInfinity);

public sealed record BracingResult(IReadOnlyList<BracingPart> Parts, IReadOnlyList<string> Errors, IReadOnlyList<(V3 a, V3 b)> DiagonalLines);

/// <summary>
/// Geometria do contraventamento em X no plano XY (cota Z do primeiro ponto). Os 2 pontos são cantos opostos do retângulo;
/// a diagonal 1 vai do ponto 1 ao ponto 2 e a diagonal 2 liga os outros dois cantos.
/// Ao longo de cada diagonal: gusset + chapa de base + parafuso, barra lisa apoiada e soldada no gusset, emenda com talas,
/// barra roscada, esticador (cantoneira + 2 chapas de topo + porca e arruela dos dois lados) e o mesmo detalhe no outro canto.
/// </summary>
public static class BracingGeometry
{
    public static List<string> Validate(BracingParams p, double diagonalLength)
    {
        var e = new List<string>();
        void Pos(double v, string name) { if (!(v > 0) || !double.IsFinite(v)) e.Add($"{name} deve ser maior que zero."); }
        Pos(p.RodDiameter, "Diâmetro da barra"); Pos(p.GussetThickness, "Espessura do gusset"); Pos(p.GussetLength, "Comprimento do gusset");
        Pos(p.GussetWidth, "Largura do gusset"); Pos(p.CleatLength, "Comprimento da cantoneira"); Pos(p.ThreadLength, "Comprimento da rosca");
        Pos(p.EndPlateThickness, "Espessura da chapa de topo"); Pos(p.RodBearingOnGusset, "Apoio da barra no gusset");
        if (p.BasePlate) { Pos(p.BaseThickness, "Espessura da chapa de base"); Pos(p.BaseLength, "Comprimento da chapa de base"); Pos(p.BaseWidth, "Largura da chapa de base"); }
        if (p.Bolt) { Pos(p.BoltDiameter, "Diâmetro do parafuso"); Pos(p.BoltLength, "Comprimento do parafuso"); if (!p.BasePlate) e.Add("O parafuso liga o gusset à chapa de base: ligue a chapa de base ou desligue o parafuso."); }
        if (p.NutsAndWashers) { Pos(p.NutAcrossFlats, "Porca (entre faces)"); Pos(p.NutHeight, "Altura da porca"); Pos(p.WasherOuterDiameter, "Diâmetro externo da arruela"); Pos(p.WasherThickness, "Espessura da arruela"); if (p.WasherInnerDiameter >= p.WasherOuterDiameter) e.Add("Diâmetro interno da arruela deve ser menor que o externo."); }
        if (p.RodBearingOnGusset > p.GussetLength) e.Add("O apoio da barra não pode ser maior que o comprimento do gusset.");
        if (p.EndPlateInset * 2 >= p.CleatLength) e.Add("O recuo das chapas de topo é grande demais para a cantoneira.");
        if (p.ThreadPenetration >= p.ThreadLength) e.Add("A penetração da rosca deve ser menor que o comprimento da rosca.");
        if (p.CleatLength / 2 - p.EndPlateInset - p.ThreadPenetration < 1) e.Add("As roscas se tocam dentro do esticador: diminua a penetração ou aumente a cantoneira.");
        if (p.SpliceBars && !(p.SpliceLength > 0)) e.Add("Comprimento das talas deve ser maior que zero.");
        if (diagonalLength > 0)
        {
            double c = Center(p, diagonalLength);
            double rodStart = p.GussetSetback + p.GussetLength - p.RodBearingOnGusset;
            double half = HalfTurnbuckle(p);
            if (c - half <= rodStart + 1) e.Add($"O esticador ficou em cima do gusset do canto 1 (centro a {c:0} mm). Mude a posição.");
            if (c + half >= diagonalLength - rodStart - 1) e.Add($"O esticador ficou em cima do gusset do canto 2 (centro a {c:0} mm de {diagonalLength:0} mm). Mude a posição.");
            if (2 * (p.GussetSetback + p.GussetLength) >= diagonalLength) e.Add("A diagonal é curta demais para os dois gussets.");
        }
        return e;
    }

    /// <summary>Centro do esticador ao longo da diagonal (mm a partir do canto 1).</summary>
    public static double Center(BracingParams p, double length)
    {
        if (p.TurnbuckleMode == TurnbucklePositionMode.Percent) return length * p.TurnbucklePosition / 100.0;
        double d = p.TurnbucklePosition + (p.TurnbuckleReference == TurnbuckleReference.NearEnd ? p.CleatLength / 2 : 0);
        return p.TurnbuckleFromCorner2 ? length - d : d;
    }

    /// <summary>Meio comprimento do conjunto esticador (do centro até o fim da barra roscada).</summary>
    private static double HalfTurnbuckle(BracingParams p) => p.CleatLength / 2 - p.EndPlateInset - p.ThreadPenetration + p.ThreadLength;

    public static BracingResult Build(V3 p1, V3 p2, BracingParams p) => Build(p1, p2, p, Array.Empty<SupportStrip>());

    /// <summary>
    /// Com vigas de apoio: em cada canto que cai sobre uma viga, a chapa de base (ou o gusset, sem chapa de base) é
    /// recortada na largura do banzo e soldada na viga.
    /// </summary>
    public static BracingResult Build(V3 p1, V3 p2, BracingParams p, IReadOnlyList<SupportStrip> supports)
    {
        double z = p1.Z;
        var a = new V3(p1.X, p1.Y, z);
        var b = new V3(p2.X, p2.Y, z);
        var lines = new List<(V3, V3)>();
        if (p.Diagonals is BracingDiagonals.X or BracingDiagonals.Diagonal1) lines.Add((a, b));
        if (p.Diagonals is BracingDiagonals.X or BracingDiagonals.Diagonal2) lines.Add((new V3(a.X, b.Y, z), new V3(b.X, a.Y, z)));

        var errors = new List<string>();
        if (Math.Abs(b.X - a.X) < 1 || Math.Abs(b.Y - a.Y) < 1) errors.Add("Os dois pontos precisam formar um retângulo no plano XY (não podem estar alinhados em X ou em Y).");
        var parts = new List<BracingPart>();
        if (errors.Count == 0)
        {
            // Em X, as diagonais se cruzam no meio: os esticadores não podem ficar no cruzamento.
            if (lines.Count == 2)
            {
                double len = (lines[0].Item2 - lines[0].Item1).Length;
                double c = Center(p, len), clear = HalfTurnbuckle(p) + Math.Max(p.NutAcrossFlats, p.EndPlateLength) / 2;
                if (Math.Abs(c - len / 2) < clear)
                    errors.Add($"Em X, os esticadores das duas diagonais se chocam no cruzamento (centro a {c:0} mm, cruzamento a {len / 2:0} mm). Afaste o esticador pelo menos {clear:0} mm do meio (por exemplo, 65%).");
            }
            int k = 0;
            foreach (var (q1, q2) in lines)
            {
                k++;
                List<string> e = Validate(p, (q2 - q1).Length);
                foreach (string s in e) if (!errors.Contains(s)) errors.Add(s);
                if (e.Count == 0) BuildDiagonal(parts, q1, q2, k, p, supports);
            }
        }
        return new BracingResult(errors.Count == 0 ? parts : new List<BracingPart>(), errors, lines);
    }

    /// <summary>Viga de apoio sob o canto: o canto cai dentro do banzo (com folga de 25 mm). Null se nenhuma.</summary>
    public static SupportStrip? SupportAt(V3 corner, IReadOnlyList<SupportStrip> supports)
    {
        SupportStrip? best = null; double bestD = double.MaxValue;
        foreach (SupportStrip s in supports)
        {
            V3 d = new V3(s.Dir.X, s.Dir.Y, 0);
            if (d.Length < 1e-9) continue;
            V3 perp = V3.Cross(new V3(0, 0, 1), d.Unit);
            double off = Math.Abs(V3.Dot(new V3(corner.X - s.Origin.X, corner.Y - s.Origin.Y, 0), perp));
            if (off <= s.HalfWidth + 25 && off < bestD) { bestD = off; best = s; }
        }
        return best;
    }

    /// <summary>
    /// Corta um polígono (no plano XY) no contato com a viga: fica só a parte FORA do banzo, do lado
    /// <paramref name="side"/> (+1 ou -1 em relação ao eixo da viga). Sutherland–Hodgman com um semiplano.
    /// </summary>
    public static V3[] ClipOutside(V3[] poly, SupportStrip s, double side)
    {
        V3 d = new V3(s.Dir.X, s.Dir.Y, 0).Unit;
        V3 perp = V3.Cross(new V3(0, 0, 1), d);
        double F(V3 q) => side * V3.Dot(new V3(q.X - s.Origin.X, q.Y - s.Origin.Y, 0), perp) - s.HalfWidth;   // ≥ 0 = fica
        var output = new List<V3>();
        for (int i = 0; i < poly.Length; i++)
        {
            V3 a = poly[i], b = poly[(i + 1) % poly.Length];
            double fa = F(a), fb = F(b);
            if (fa >= 0) output.Add(a);
            if ((fa >= 0) != (fb >= 0)) output.Add(a + (b - a) * (fa / (fa - fb)));
        }
        var clean = new List<V3>();
        foreach (V3 q in output) if (clean.Count == 0 || (q - clean[^1]).Length > 0.01) clean.Add(q);
        if (clean.Count > 1 && (clean[0] - clean[^1]).Length <= 0.01) clean.RemoveAt(clean.Count - 1);
        return clean.ToArray();
    }

    /// <summary>Todas as vigas cujo banzo fica sob o canto (com folga de 25 mm).</summary>
    public static List<SupportStrip> SupportsAt(V3 corner, IReadOnlyList<SupportStrip> supports)
    {
        var list = new List<SupportStrip>();
        foreach (SupportStrip s in supports)
        {
            V3 d = new V3(s.Dir.X, s.Dir.Y, 0);
            if (d.Length < 1e-9) continue;
            V3 perp = V3.Cross(new V3(0, 0, 1), d.Unit);
            double off = Math.Abs(V3.Dot(new V3(corner.X - s.Origin.X, corner.Y - s.Origin.Y, 0), perp));
            if (off <= s.HalfWidth + 25) list.Add(s);
        }
        return list;
    }

    /// <summary>A chapa bate na viga ou encosta nela (até 2 mm acima ou abaixo da seção).</summary>
    public static bool Touches(SupportStrip s, double bottom, double top) => Math.Min(top, s.ZMax) - Math.Max(bottom, s.ZMin) > -2;

    /// <summary>Chanfra os cantos pedidos (índice do vértice, tamanho) recuando ao longo das duas arestas vizinhas.</summary>
    public static V3[] Chamfer(V3[] poly, IEnumerable<(int index, double size)> corners)
    {
        var map = new Dictionary<int, double>();
        foreach (var (index, size) in corners) if (size > 0 && index >= 0 && index < poly.Length) map[index] = size;
        if (map.Count == 0) return poly;
        var res = new List<V3>();
        for (int i = 0; i < poly.Length; i++)
        {
            if (!map.TryGetValue(i, out double c)) { res.Add(poly[i]); continue; }
            V3 prev = poly[(i + poly.Length - 1) % poly.Length], next = poly[(i + 1) % poly.Length], v = poly[i];
            double lp = (prev - v).Length, ln = (next - v).Length;
            res.Add(v + (prev - v) * (Math.Min(c, lp * 0.49) / lp));
            res.Add(v + (next - v) * (Math.Min(c, ln * 0.49) / ln));
        }
        return res.ToArray();
    }

    /// <summary>Lado da viga para onde vai o contraventamento (o painel): sinal de <paramref name="inward"/> na normal do eixo.</summary>
    public static double PanelSide(SupportStrip s, V3 inward)
    {
        V3 d = new V3(s.Dir.X, s.Dir.Y, 0).Unit;
        double k = V3.Dot(new V3(inward.X, inward.Y, 0), V3.Cross(new V3(0, 0, 1), d));
        return k >= 0 ? 1 : -1;
    }

    /// <summary>A chapa (de <paramref name="bottom"/> a <paramref name="top"/>) bate na seção da viga (mais de 0,5 mm de sobreposição na cota).</summary>
    public static bool Collides(SupportStrip s, double bottom, double top) => Math.Min(top, s.ZMax) - Math.Max(bottom, s.ZMin) > 0.5;

    private static void BuildDiagonal(List<BracingPart> parts, V3 q1, V3 q2, int diag, BracingParams p, IReadOnlyList<SupportStrip> supports)
    {
        double L = (q2 - q1).Length;
        V3 u = (q2 - q1).Unit;                       // ao longo da diagonal
        V3 zA = new(0, 0, 1);
        V3 n = V3.Cross(zA, u).Unit;                 // lateral, no plano
        double r = p.RodDiameter / 2;
        double baseTop = q1.Z - p.BaseTopBelowClick; // topo da chapa de base no Z do ponto (menos a cota a8)
        double gussetTop = (p.BasePlate ? baseTop : q1.Z) + p.GussetThickness;
        string thrProfile = p.ThreadedRodProfile.Trim().Length > 0 ? p.ThreadedRodProfile : p.RodProfile;
        string thrMaterial = p.ThreadedRodMaterial.Trim().Length > 0 ? p.ThreadedRodMaterial : p.RodMaterial;
        string spliceProfile = p.SpliceProfile.Trim().Length > 0 ? p.SpliceProfile : p.RodProfile;
        double rodZ = gussetTop + r;
        V3 At(double s, double lat = 0, double zAbs = double.NaN) => q1 + u * s + n * lat + new V3(0, 0, (double.IsNaN(zAbs) ? rodZ : zAbs) - q1.Z);
        int Add(BracingPart part) { parts.Add(part); return parts.Count - 1; }

        double c = Center(p, L);
        double plateIn1 = c - p.CleatLength / 2 + p.EndPlateInset;       // chapa de topo 1
        double plateIn2 = c + p.CleatLength / 2 - p.EndPlateInset;       // chapa de topo 2
        double thrIn1 = plateIn1 + p.ThreadPenetration, thrOut1 = thrIn1 - p.ThreadLength;
        double thrIn2 = plateIn2 - p.ThreadPenetration, thrOut2 = thrIn2 + p.ThreadLength;
        double rodStart = p.GussetSetback + p.GussetLength - p.RodBearingOnGusset;
        double rodEnd = L - rodStart;

        foreach (int end in new[] { 1, 2 })
        {
            // Distância medida a partir do canto daquela ponta.
            double S(double fromCorner) => end == 1 ? fromCorner : L - fromCorner;
            double gs = S(p.GussetSetback), ge = S(p.GussetSetback + p.GussetLength);
            V3 corner = end == 1 ? q1 : q2;
            V3 uIn = end == 1 ? u : u * -1;
            List<SupportStrip> here = SupportsAt(corner, supports);
            double baseBottom = baseTop - p.BaseThickness, gussetBottom = gussetTop - p.GussetThickness;

            BracingPart gussetPart = Rect(BracingPartKind.Gusset, diag, "Gusset", p.PlateMaterial, At(Math.Min(gs, ge)), At(Math.Max(gs, ge)), u, n, p.GussetWidth, gussetTop, p.GussetThickness);
            // Rect: [início-h, fim-h, fim+h, início+h]; na ponta 1 o "início" fica no lado do clique, na ponta 2 o "fim".
            int[] nearIdx = end == 1 ? new[] { 0, 3 } : new[] { 1, 2 }, farIdx = end == 1 ? new[] { 1, 2 } : new[] { 0, 3 };
            gussetPart.Polygon = Chamfer(gussetPart.Polygon, nearIdx.Select(i => (i, p.GussetEndChamfer)).Concat(farIdx.Select(i => (i, p.GussetFarChamfer))));
            foreach (SupportStrip sp in here)
                if (Collides(sp, gussetBottom, gussetTop)) Clip(gussetPart, sp, PanelSide(sp, uIn));
            int gusset = Add(gussetPart);

            int? basePl = null;
            if (p.BasePlate)
            {
                BracingPart bpPart;
                if (p.BaseAlignment == BasePlateAlignment.Diagonal)
                {
                    double bc = S(p.BaseCenterFromPoint);
                    bpPart = Rect(BracingPartKind.BasePlate, diag, "Chapa de base", p.PlateMaterial, At(bc - p.BaseLength / 2), At(bc + p.BaseLength / 2), u, n, p.BaseWidth, baseTop, p.BaseThickness, centered: true);
                }
                else
                {
                    // Alinhada à viga de apoio (a que bate na chapa; senão a mais próxima) ou à UCS, com o canto no clique,
                    // crescendo para dentro do painel.
                    SupportStrip? prim = here.OrderByDescending(sp => Collides(sp, baseBottom, baseTop)).FirstOrDefault();
                    V3 ax = prim != null ? new V3(prim.Dir.X, prim.Dir.Y, 0).Unit : new V3(1, 0, 0);
                    V3 ay = V3.Cross(zA, ax);
                    V3 X = ax * (V3.Dot(uIn, ax) >= 0 ? 1 : -1), Y = ay * (V3.Dot(uIn, ay) >= 0 ? 1 : -1);
                    V3 c0 = new(corner.X, corner.Y, baseTop);
                    V3[] poly = { c0, c0 + X * p.BaseLength, c0 + X * p.BaseLength + Y * p.BaseWidth, c0 + Y * p.BaseWidth };
                    if (V3.Cross(X, Y).Z < 0) Array.Reverse(poly, 1, 3);   // sempre anti-horário visto de cima
                    bpPart = new BracingPart
                    {
                        Kind = BracingPartKind.BasePlate, Diagonal = diag, Role = "Chapa de base", Material = p.PlateMaterial, Thickness = p.BaseThickness,
                        Normal = zA, Origin = (poly[0] + poly[2]) * 0.5, Polygon = poly,
                    };
                }
                bpPart.Polygon = Chamfer(bpPart.Polygon, new[] { (0, p.BaseChamferClick), (1, p.BaseChamfer2), (2, p.BaseChamfer3), (3, p.BaseChamfer4) });
                foreach (SupportStrip sp in here)
                    if (Collides(sp, baseBottom, baseTop)) Clip(bpPart, sp, PanelSide(sp, uIn));
                basePl = Add(bpPart);
            }
            if (p.WeldSupportOn)
            {
                // Solda da chapa que encosta na viga (chapa de base ou, sem ela, o gusset): só vigas que batem ou tocam a chapa.
                int onBeam = basePl ?? gusset;
                double bottom = p.BasePlate ? baseBottom : gussetBottom, top = p.BasePlate ? baseTop : gussetTop;
                V3[] pg = parts[onBeam].Polygon;
                V3 mid = pg.Aggregate(new V3(0, 0, 0), (acc, q) => acc + q) * (1.0 / pg.Length);
                foreach (SupportStrip sp in here.Where(sp => Touches(sp, bottom, top)))
                    parts.Add(new BracingPart
                    {
                        Kind = BracingPartKind.Weld, Diagonal = diag, Role = "Solda na estrutura", Origin = new V3(mid.X, mid.Y, bottom),
                        Axis = new V3(sp.Dir.X, sp.Dir.Y, 0).Unit, Size = p.WeldSupport, Connects = new[] { onBeam }, SupportTag = sp.Tag,
                    });
            }
            if (p.Bolt && basePl is int bp)
                Add(new BracingPart { Kind = BracingPartKind.Bolt, Diagonal = diag, Role = "Parafuso", Origin = At(S(p.GussetSetback + p.BoltFromGussetStart), 0, gussetTop), Normal = zA, Axis = u, Size = p.BoltDiameter, Length = p.BoltLength, Connects = new[] { gusset, bp } });
        }

        // Barras: lisa 1 → roscada 1 → (esticador) → roscada 2 → lisa 2
        int rod1 = Add(Bar(BracingPartKind.Rod, diag, "Tirante", p.RodProfile, p.RodMaterial, At(rodStart), At(thrOut1)));
        int thr1 = Add(Bar(BracingPartKind.ThreadedRod, diag, "Barra roscada", thrProfile, thrMaterial, At(thrOut1), At(thrIn1)));
        int cleat = Add(Bar(BracingPartKind.Cleat, diag, "Esticador", p.CleatProfile, p.CleatMaterial, At(c - p.CleatLength / 2), At(c + p.CleatLength / 2), p.CleatRotation));
        int thr2 = Add(Bar(BracingPartKind.ThreadedRod, diag, "Barra roscada", thrProfile, thrMaterial, At(thrOut2), At(thrIn2)));
        int rod2 = Add(Bar(BracingPartKind.Rod, diag, "Tirante", p.RodProfile, p.RodMaterial, At(rodEnd), At(thrOut2)));

        // Soldas barra-gusset (gussets são os 2 primeiros "Gusset" desta diagonal)
        var gussets = parts.Select((x, i) => (x, i)).Where(t => t.x.Diagonal == diag && t.x.Kind == BracingPartKind.Gusset).Select(t => t.i).ToList();
        double bearingMid = p.GussetSetback + p.GussetLength - p.RodBearingOnGusset / 2;
        if (p.WeldRodGussetOn)
        {
            Add(Weld(diag, At(bearingMid), u, p.WeldRodGusset, rod1, gussets[0]));
            Add(Weld(diag, At(L - bearingMid), u, p.WeldRodGusset, rod2, gussets[1]));
        }

        // Emendas com talas (lado a lado com a barra)
        foreach (var (s, rod, thr) in new[] { (thrOut1, rod1, thr1), (thrOut2, rod2, thr2) })
        {
            var connects = new List<int> { rod, thr };
            if (p.SpliceBars)
            {
                foreach (double side in new[] { +1.0, -1.0 })
                    connects.Add(Add(Bar(BracingPartKind.SpliceBar, diag, "Tala", spliceProfile, p.RodMaterial, At(s - p.SpliceLength / 2, side * p.RodDiameter), At(s + p.SpliceLength / 2, side * p.RodDiameter))));
            }
            if (p.WeldSpliceOn) Add(Weld(diag, At(s), u, p.WeldSplice, connects.ToArray()));
        }

        // Chapas de topo do esticador (normal ao longo da diagonal), soldadas na cantoneira
        foreach (double s in new[] { plateIn1, plateIn2 })
        {
            V3 ctr = At(s);
            int ep = Add(new BracingPart
            {
                Kind = BracingPartKind.EndPlate, Diagonal = diag, Role = "Chapa de topo", Material = p.PlateMaterial, Thickness = p.EndPlateThickness,
                Normal = u, Origin = ctr,
                Polygon = SquareAround(ctr + u * (p.EndPlateThickness / 2), n, zA, p.EndPlateLength, p.EndPlateWidth),
            });
            if (p.WeldEndPlateOn) Add(Weld(diag, ctr, n, p.WeldEndPlate, ep, cleat));
        }

        // Porcas e arruelas: dos dois lados de cada chapa de topo
        if (p.NutsAndWashers)
        {
            foreach (double s in new[] { plateIn1, plateIn2 })
            {
                double faceIn = s + p.EndPlateThickness / 2, faceOut = s - p.EndPlateThickness / 2;   // em +u e -u
                foreach (double dir in new[] { +1.0, -1.0 })
                {
                    double face = dir > 0 ? faceIn : faceOut;
                    double wc = face + dir * p.WasherThickness / 2;
                    double nc = face + dir * (p.WasherThickness + p.NutHeight / 2);
                    Add(new BracingPart { Kind = BracingPartKind.Washer, Diagonal = diag, Role = "Arruela", Origin = At(wc), Axis = u * dir });
                    Add(new BracingPart { Kind = BracingPartKind.Nut, Diagonal = diag, Role = "Porca", Origin = At(nc), Axis = u * dir });
                }
            }
        }
    }

    private static BracingPart Bar(BracingPartKind kind, int diag, string role, string profile, string material, V3 s, V3 e, double angle = 0) =>
        new() { Kind = kind, Diagonal = diag, Role = role, Profile = profile, Material = material, Start = s, End = e, Angle = angle };

    private static void Clip(BracingPart plate, SupportStrip s, double side)
    {
        V3[] c = ClipOutside(plate.Polygon, s, side);
        if (c.Length >= 3) plate.Polygon = c;
    }

    private static BracingPart Weld(int diag, V3 at, V3 dir, double size, params int[] connects) =>
        new() { Kind = BracingPartKind.Weld, Diagonal = diag, Role = "Solda", Origin = at, Axis = dir, Size = size, Connects = connects };

    /// <summary>Chapa horizontal retangular entre dois pontos do eixo (comprimento) com largura centrada; topo em <paramref name="top"/>.</summary>
    private static BracingPart Rect(BracingPartKind kind, int diag, string role, string material, V3 a, V3 b, V3 u, V3 n, double width, double top, double thickness, bool centered = false)
    {
        V3 a0 = new(a.X, a.Y, top), b0 = new(b.X, b.Y, top);
        V3 h = n * (width / 2);
        return new BracingPart
        {
            Kind = kind, Diagonal = diag, Role = role, Material = material, Thickness = thickness, Normal = new V3(0, 0, 1),
            Origin = (a0 + b0) * 0.5, Polygon = new[] { a0 - h, b0 - h, b0 + h, a0 + h },
        };
    }

    private static V3[] SquareAround(V3 c, V3 x, V3 y, double lx, double ly)
    {
        V3 hx = x * (lx / 2), hy = y * (ly / 2);
        return new[] { c - hx - hy, c + hx - hy, c + hx + hy, c - hx + hy };
    }
}
