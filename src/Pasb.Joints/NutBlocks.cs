using System;
using System.Globalization;
using Autodesk.AutoCAD.DatabaseServices;
using AcColor = Autodesk.AutoCAD.Colors.Color;
using AcColorMethod = Autodesk.AutoCAD.Colors.ColorMethod;
using AcPoint = Autodesk.AutoCAD.Geometry.Point3d;

namespace Pasb.Joints;

/// <summary>
/// Blocos de porca e arruela próprios do PASB (sólidos 3D com furo), criados no desenho com as medidas pedidas.
/// O nome do bloco traz as medidas, então medidas iguais reaproveitam o mesmo bloco.
/// </summary>
internal static class NutBlocks
{
    public static string NutName(double acrossFlats, double height, double hole) =>
        $"PASB_PORCA_S{F(acrossFlats)}_H{F(height)}_D{F(hole)}";

    public static string WasherName(double outer, double inner, double thickness) =>
        $"PASB_ARRUELA_D{F(outer)}_d{F(inner)}_t{F(thickness)}";

    private static string F(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);

    /// <summary>Garante os dois blocos no banco de dados informado. Usa transação "abre e fecha", que pode ser aninhada.</summary>
    public static void Ensure(Database db, double acrossFlats, double height, double outer, double inner, double thickness)
    {
        string nut = NutName(acrossFlats, height, inner), washer = WasherName(outer, inner, thickness);
        using Transaction tr = db.TransactionManager.StartOpenCloseTransaction();
        var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
        if (!bt.Has(nut)) Define(tr, bt, nut, NutSolid(acrossFlats, height, inner / 2));
        if (!bt.Has(washer)) Define(tr, bt, washer, RingSolid(outer / 2, inner / 2, thickness));
        tr.Commit();
    }

    private static void Define(Transaction tr, BlockTable bt, string name, Solid3d solid)
    {
        if (!bt.IsWriteEnabled) bt.UpgradeOpen();
        var btr = new BlockTableRecord { Name = name, Origin = AcPoint.Origin };
        bt.Add(btr);
        tr.AddNewlyCreatedDBObject(btr, true);
        solid.Layer = "0";
        solid.Color = AcColor.FromColorIndex(AcColorMethod.ByBlock, 0);
        btr.AppendEntity(solid);
        tr.AddNewlyCreatedDBObject(solid, true);
    }

    /// <summary>Porca sextavada (entre faces s, altura h) com furo, centrada na origem, eixo Z.</summary>
    private static Solid3d NutSolid(double s, double h, double holeR)
    {
        double rc = s / Math.Sqrt(3);
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
}
