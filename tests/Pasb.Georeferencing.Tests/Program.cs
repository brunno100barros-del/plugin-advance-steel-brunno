using System;
using Pasb.Georeferencing;

int failed = 0, passed = 0;
void Run(string name, Action t)
{
    try { t(); passed++; Console.WriteLine("ok    " + name); }
    catch (Exception ex) { failed++; Console.WriteLine("FALHA " + name + ": " + ex.Message); }
}
void Near(double exp, double act, string what, double tol = 1e-6)
{
    if (Math.Abs(exp - act) > tol) throw new Exception($"{what}: esperado {exp}, obtido {act}");
}
double Dist(Point3 a, Point3 b) => Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2) + Math.Pow(a.Z - b.Z, 2));

// Ponto de controle no modelo (mm) e coordenadas reais (m), zona 22S (Brasil central/sul).
GeoParameters P(double rot, double z = 0) => new(GeoMode.SirgasUtm,
    new Point3(1000, 2000, z), new Point3(500_000, 7_400_000, 800), rot, 0.001, 22, Hemisphere.South);

Run("ponto de controle cai nas coordenadas informadas", () =>
{
    foreach (double r in new[] { 0.0, 90, 180, 37.5, -12 })
    {
        Point3 t = Georeference.ToTarget(P(r), new Point3(1000, 2000, 0));
        Near(500_000, t.X, $"E rot {r}"); Near(7_400_000, t.Y, $"N rot {r}"); Near(800, t.Z, $"H rot {r}");
    }
});
Run("rotação 0: deslocamento direto em metros", () =>
{
    Point3 t = Georeference.ToTarget(P(0), new Point3(2000, 2000, 0));
    Near(500_001, t.X, "E"); Near(7_400_000, t.Y, "N");
});
Run("rotação 90: X do modelo aponta para o Norte", () =>
{
    Point3 t = Georeference.ToTarget(P(90), new Point3(2000, 2000, 0));
    Near(500_000, t.X, "E"); Near(7_400_001, t.Y, "N");
});
Run("rotação 180", () =>
{
    Point3 t = Georeference.ToTarget(P(180), new Point3(2000, 2000, 0));
    Near(499_999, t.X, "E"); Near(7_400_000, t.Y, "N");
});
Run("distâncias preservadas (escala mm→m)", () =>
{
    var a = new Point3(0, 0, 0); var b = new Point3(3000, 4000, 1200);
    double dm = Dist(a, b) * 0.001;
    Near(dm, Dist(Georeference.ToTarget(P(33), a), Georeference.ToTarget(P(33), b)), "distância");
});
Run("elevação positiva, negativa e zero", () =>
{
    foreach (double z in new[] { 0.0, 2500, -1500 })
        Near(800 + (z - 0) * 0.001, Georeference.ToTarget(P(10), new Point3(1000, 2000, z)).Z, $"H z={z}");
});
Run("inversa exata (ida e volta)", () =>
{
    var m = new Point3(12345.6, -789.1, 4321);
    Point3 back = Georeference.ToModel(P(123.4), Georeference.ToTarget(P(123.4), m));
    Near(m.X, back.X, "X", 1e-6); Near(m.Y, back.Y, "Y", 1e-6); Near(m.Z, back.Z, "Z", 1e-6);
});
Run("IfcMapConversion: origem do modelo e eixo X", () =>
{
    MapConversion mc = Georeference.ToIfcMapConversion(P(90));
    // Origem (0,0,0) está 1 m a oeste... com rotação 90: dx=-1000, dy=-2000 → E = 500000 + 0.001*(0*-1000 - 1*-2000) = 500002
    Near(500_002, mc.Eastings, "E"); Near(7_399_999, mc.Northings, "N"); Near(800, mc.OrthogonalHeight, "H");
    Near(0, mc.XAxisAbscissa, "abscissa"); Near(1, mc.XAxisOrdinate, "ordenada"); Near(1, mc.Scale, "escala");
});
Run("MapConversion reproduz a transformação para qualquer ponto", () =>
{
    GeoParameters p = P(-27.3);
    MapConversion mc = Georeference.ToIfcMapConversion(p);
    var m = new Point3(5500, -300, 900);
    double xm = m.X * 0.001, ym = m.Y * 0.001, zm = m.Z * 0.001;   // geometria IFC em metros
    double e = mc.Eastings + mc.XAxisAbscissa * xm - mc.XAxisOrdinate * ym;
    double n = mc.Northings + mc.XAxisOrdinate * xm + mc.XAxisAbscissa * ym;
    Point3 t = Georeference.ToTarget(p, m);
    Near(t.X, e, "E"); Near(t.Y, n, "N"); Near(t.Z, mc.OrthogonalHeight + zm, "H");
});
Run("azimute do Norte do modelo vira rotação negativa", () => Near(-30, GeoParameters.RotationFromModelNorthAzimuth(30), "α"));
Run("EPSG SIRGAS 2000 / UTM", () =>
{
    Near(31982, Georeference.EpsgSirgas2000Utm(22, Hemisphere.South)!.Value, "22S");
    Near(31985, Georeference.EpsgSirgas2000Utm(25, Hemisphere.South)!.Value, "25S");
    Near(31977, Georeference.EpsgSirgas2000Utm(17, Hemisphere.South)!.Value, "17S");
    Near(31976, Georeference.EpsgSirgas2000Utm(22, Hemisphere.North)!.Value, "22N");
    if (Georeference.EpsgSirgas2000Utm(30, Hemisphere.South) is not null) throw new Exception("30S não deveria existir");
});
Run("validação: parâmetros válidos e inválidos", () =>
{
    if (Georeference.Validate(P(0)).Count != 0) throw new Exception(string.Join("; ", Georeference.Validate(P(0))));
    var bad = P(0) with { UtmZone = 0, TargetPoint = new Point3(50, 20, 0) };
    if (Georeference.Validate(bad).Count < 2) throw new Exception("deveria acusar zona e coordenadas");
    var local = new GeoParameters(GeoMode.LocalXyz, new Point3(0, 0, 0), new Point3(100, 200, 10), 15, 0.001);
    if (Georeference.Validate(local).Count != 1) throw new Exception("modo B exige nome do CRS");
});
Run("modo B: CRS descritivo, sem EPSG, mesma matemática", () =>
{
    var b = new GeoParameters(GeoMode.LocalXyz, new Point3(1000, 2000, 0), new Point3(100, 200, 10), 90, 0.001, LocalCrsName: "Obra X");
    ProjectedCrs crs = Georeference.ToIfcProjectedCrs(b);
    if (crs.Name != "Obra X" || crs.GeodeticDatum != null) throw new Exception("CRS local incorreto");
    Point3 t = Georeference.ToTarget(b, new Point3(2000, 2000, 0));
    Near(100, t.X, "X"); Near(201, t.Y, "Y");
});

Console.WriteLine($"{passed} passaram, {failed} falharam");
return failed == 0 ? 0 : 1;
