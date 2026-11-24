using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Xbim.Common;
using Xbim.Common.Step21;
using Xbim.Ifc;
using Xbim.Ifc4.Interfaces;
using Xbim.Ifc4.MeasureResource;
using I2 = Xbim.Ifc2x3.GeometryResource;
using I4 = Xbim.Ifc4.GeometryResource;
using R2 = Xbim.Ifc2x3.RepresentationResource;
using R4 = Xbim.Ifc4.RepresentationResource;
using G2 = Xbim.Ifc2x3.GeometricModelResource;
using G4 = Xbim.Ifc4.GeometricModelResource;

namespace Pasb.IfcImport;

/// <summary>
/// Correções de geometria antes da triangulação (docs/IFC-IMPORT.md, "Correção na 0.14.3").
/// A xBIM interrompe o modelo inteiro na primeira peça cujo sólido o OpenCascade não consegue montar
/// ("Processing halted due to model error"). Aqui:
///  1. <see cref="RepairBrokenArcs"/>: contornos de recorte (IfcPolygonalBoundedHalfSpace) com um arco cujas pontas
///     não encontram os segmentos vizinhos (dado errado exportado pelo Revit) ganham uma reta entre os vizinhos;
///  2. <see cref="TryDropFailedClipping"/>: se ainda assim um recorte falhar, a peça entra sem aquele recorte.
/// Tudo é feito só na cópia em memória; o arquivo IFC não é alterado.
/// </summary>
internal static class GeometryRepair
{
    /// <summary>Troca por reta os arcos de contorno de recorte que não fecham com os vizinhos. Devolve um aviso por peça.</summary>
    public static List<string> RepairBrokenArcs(IfcStore model)
    {
        var warnings = new List<string>();
        if (model.SchemaVersion != XbimSchemaVersion.Ifc2X3 && model.SchemaVersion != XbimSchemaVersion.Ifc4) return warnings;
        double tol = model.ModelFactors.OneMilliMetre;      // 1 mm nas unidades do IFC
        double ang = model.ModelFactors.AngleToRadiansConversionFactor;
        var fixes = new List<(IIfcCompositeCurveSegment seg, Pt a, Pt b, IIfcPolygonalBoundedHalfSpace hs, double gap)>();

        foreach (IIfcPolygonalBoundedHalfSpace hs in model.Instances.OfType<IIfcPolygonalBoundedHalfSpace>())
        {
            if (hs.PolygonalBoundary is not IIfcCompositeCurve cc) continue;
            var segs = cc.Segments.ToList();
            if (segs.Count < 3) continue;
            var ends = segs.Select(s => Ends(s, ang)).ToList();
            for (int i = 0; i < segs.Count; i++)
            {
                if (segs[i].ParentCurve is not IIfcTrimmedCurve { BasisCurve: IIfcCircle } || ends[i] is not { } me) continue;
                var prev = ends[(i + segs.Count - 1) % segs.Count];
                var next = ends[(i + 1) % segs.Count];
                if (prev is null || next is null) continue;
                double gap = Math.Max(me.start.Dist(prev.Value.end), me.end.Dist(next.Value.start));
                if (gap <= tol || prev.Value.end.Dist(next.Value.start) <= tol) continue;
                fixes.Add((segs[i], prev.Value.end, next.Value.start, hs, gap));
            }
        }
        if (fixes.Count == 0) return warnings;

        var owners = Owners(model);
        using (ITransaction txn = model.BeginTransaction("PASB: arcos de recorte"))
        {
            foreach (var f in fixes)
            {
                // A reta vai do fim do segmento anterior ao início do seguinte, no sentido da curva (SameSense verdadeiro).
                Pt a = f.a, b = f.b;
                if (f.seg is I2.IfcCompositeCurveSegment s2)
                {
                    s2.ParentCurve = model.Instances.New<I2.IfcPolyline>(pl =>
                    {
                        pl.Points.Add(model.Instances.New<I2.IfcCartesianPoint>(p => p.SetXY(a.X, a.Y)));
                        pl.Points.Add(model.Instances.New<I2.IfcCartesianPoint>(p => p.SetXY(b.X, b.Y)));
                    });
                    s2.SameSense = true;
                }
                else if (f.seg is I4.IfcCompositeCurveSegment s4)
                {
                    s4.ParentCurve = model.Instances.New<I4.IfcPolyline>(pl =>
                    {
                        pl.Points.Add(model.Instances.New<I4.IfcCartesianPoint>(p => p.SetXY(a.X, a.Y)));
                        pl.Points.Add(model.Instances.New<I4.IfcCartesianPoint>(p => p.SetXY(b.X, b.Y)));
                    });
                    s4.SameSense = true;
                }
                else continue;
                warnings.Add($"{Describe(owners, f.hs)}: arco do contorno de um recorte não fechava (pontas a {f.gap / model.ModelFactors.OneMetre:0.###} m dos vizinhos); trocado por reta.");
            }
            txn.Commit();
        }
        return warnings;
    }

    /// <summary>
    /// Se a falha da xBIM aponta um IfcBooleanResult/IfcBooleanClippingResult (#n), troca-o pelo primeiro operando
    /// (a peça entra sem aquele recorte). Devolve false se a falha for de outro tipo.
    /// </summary>
    public static bool TryDropFailedClipping(IfcStore model, Exception ex, out string warning)
    {
        warning = "";
        Match m = Regex.Match(ex.ToString(), @"Creating (Ifc\w+), #(\d+)");
        if (!m.Success || !int.TryParse(m.Groups[2].Value, out int label)) return false;
        if (model.Instances[label] is not IIfcBooleanResult br || br.FirstOperand is not IIfcRepresentationItem first) return false;

        var owners = Owners(model);
        string who = Describe(owners, br);
        int changed = 0;
        using (ITransaction txn = model.BeginTransaction("PASB: recorte sem solução"))
        {
            foreach (var rep in model.Instances.OfType<IIfcRepresentation>().Where(r => r.Items.Contains(br)).ToList())
            {
                if (rep is R2.IfcRepresentation r2 && first is I2.IfcRepresentationItem f2) { r2.Items.Remove((I2.IfcRepresentationItem)br); r2.Items.Add(f2); changed++; }
                else if (rep is R4.IfcRepresentation r4 && first is I4.IfcRepresentationItem f4) { r4.Items.Remove((I4.IfcRepresentationItem)br); r4.Items.Add(f4); changed++; }
            }
            foreach (var outer in model.Instances.OfType<IIfcBooleanResult>().Where(b => b.FirstOperand == br).ToList())
            {
                if (outer is G2.IfcBooleanResult o2 && first is G2.IfcBooleanOperand b2) { o2.FirstOperand = b2; changed++; }
                else if (outer is G4.IfcBooleanResult o4 && first is G4.IfcBooleanOperand b4) { o4.FirstOperand = b4; changed++; }
            }
            if (changed == 0) return false;
            txn.Commit();
        }
        warning = $"{who}: recorte {m.Groups[1].Value} #{label} sem solução no motor geométrico; a peça entrou sem esse recorte.";
        return true;
    }

    // ---- apoio ----

    internal readonly record struct Pt(double X, double Y)
    {
        public double Dist(Pt o) => Math.Sqrt((X - o.X) * (X - o.X) + (Y - o.Y) * (Y - o.Y));
    }

    /// <summary>Início e fim do segmento no sentido em que a curva composta o percorre (só retas e arcos 2D).</summary>
    internal static (Pt start, Pt end)? Ends(IIfcCompositeCurveSegment seg, double angleToRad)
    {
        (Pt, Pt)? r = seg.ParentCurve switch
        {
            IIfcPolyline pl when pl.Points.Count >= 2 => (P(pl.Points.First()), P(pl.Points.Last())),
            IIfcTrimmedCurve { BasisCurve: IIfcCircle c } tc when c.Position is IIfcAxis2Placement2D pos
                && Trim(tc.Trim1, c, pos, angleToRad) is { } t1 && Trim(tc.Trim2, c, pos, angleToRad) is { } t2 => (t1, t2),
            _ => null,
        };
        if (r is not { } v) return null;
        return (bool)seg.SameSense ? v : (v.Item2, v.Item1);
    }

    private static Pt P(IIfcCartesianPoint p) => new(p.X, p.Y);

    private static Pt? Trim(IEnumerable<IIfcTrimmingSelect> trims, IIfcCircle c, IIfcAxis2Placement2D pos, double angleToRad)
    {
        var list = trims.ToList();
        if (list.OfType<IIfcCartesianPoint>().FirstOrDefault() is { } cp) return P(cp);
        if (list.OfType<IfcParameterValue>().Select(v => (double?)System.Convert.ToDouble(v.Value)).FirstOrDefault() is not { } t) return null;
        double bx = 1, by = 0;
        if (pos.RefDirection is { } d && d.DirectionRatios.Count >= 2) { bx = d.DirectionRatios[0]; by = d.DirectionRatios[1]; }
        double a = Math.Atan2(by, bx) + t * angleToRad, r = c.Radius;
        return new Pt(pos.Location.X + r * Math.Cos(a), pos.Location.Y + r * Math.Sin(a));
    }

    /// <summary>Item de representação (inclusive operandos e meios-espaços de booleanos) → produto dono.</summary>
    private static Dictionary<int, IIfcProduct> Owners(IModel model)
    {
        var map = new Dictionary<int, IIfcProduct>();
        void Walk(IPersistEntity? e, IIfcProduct p, int depth)
        {
            if (e is null || depth > 64 || !map.TryAdd(e.EntityLabel, p)) return;
            if (e is IIfcBooleanResult b) { Walk(b.FirstOperand as IPersistEntity, p, depth + 1); Walk(b.SecondOperand as IPersistEntity, p, depth + 1); }
        }
        foreach (IIfcProduct p in model.Instances.OfType<IIfcProduct>())
            foreach (var rep in p.Representation?.Representations ?? Enumerable.Empty<IIfcRepresentation>())
                foreach (var item in rep.Items) Walk(item, p, 0);
        return map;
    }

    private static string Describe(Dictionary<int, IIfcProduct> owners, IPersistEntity item)
        => owners.TryGetValue(item.EntityLabel, out IIfcProduct? p)
            ? $"{p.ExpressType.Name} \"{(string?)p.Name}\" ({p.GlobalId})"
            : $"{item.ExpressType.Name} #{item.EntityLabel}";
}
