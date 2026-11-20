using System;
using System.Collections.Generic;

namespace Pasb.Georeferencing;

/// <summary>Modo de posicionamento (GEOREFERENCING.md).</summary>
public enum GeoMode
{
    /// <summary>A: SIRGAS 2000 / UTM, com zona e hemisfério; declarado no IFC com código EPSG.</summary>
    SirgasUtm,
    /// <summary>B: coordenadas X, Y, Z num sistema próprio da obra + rotação; CRS descritivo, sem EPSG.</summary>
    LocalXyz,
}

public enum Hemisphere { North, South }

public readonly record struct Point3(double X, double Y, double Z);

/// <summary>
/// Parâmetros informados pelo usuário.
///
/// Convenção (formalizada aqui, GEOREFERENCING.md seção "Transformação"):
/// - ModelPoint: ponto de controle no sistema do modelo, nas unidades do desenho (ex.: mm).
/// - TargetPoint: as coordenadas desse mesmo ponto no sistema de destino, em metros (E, N, H no modo A; X, Y, Z no modo B).
/// - RotationDegrees: ângulo α, em graus, sentido anti-horário positivo, do eixo X do modelo até o eixo X (Leste) do destino.
///   Para quem informa o "azimute do Norte do modelo" (ângulo horário do Norte de destino até o +Y do modelo), use
///   <see cref="RotationFromModelNorthAzimuth"/>.
/// - ModelUnitToMeters: fator das unidades do desenho para metros (mm → 0,001).
/// </summary>
public sealed record GeoParameters(
    GeoMode Mode,
    Point3 ModelPoint,
    Point3 TargetPoint,
    double RotationDegrees,
    double ModelUnitToMeters,
    int? UtmZone = null,
    Hemisphere? Hemisphere = null,
    string? LocalCrsName = null,
    string? LocalCrsDescription = null)
{
    /// <summary>α a partir do azimute (horário, a partir do Norte de destino) do eixo +Y do modelo.</summary>
    public static double RotationFromModelNorthAzimuth(double azimuthDegrees) => -azimuthDegrees;
}

/// <summary>Valores para IfcMapConversion (IFC4): origem do modelo no destino, eixo X e escala.</summary>
public sealed record MapConversion(double Eastings, double Northings, double OrthogonalHeight,
    double XAxisAbscissa, double XAxisOrdinate, double Scale);

/// <summary>Valores para IfcProjectedCRS.</summary>
public sealed record ProjectedCrs(string Name, string? Description, string? GeodeticDatum, string? MapProjection, string? MapZone);

public static class Georeference
{
    /// <summary>Valida os parâmetros. Lista vazia = válido.</summary>
    public static List<string> Validate(GeoParameters p)
    {
        var errors = new List<string>();
        if (!(p.ModelUnitToMeters > 0) || double.IsInfinity(p.ModelUnitToMeters)) errors.Add("Fator de unidade do modelo deve ser positivo.");
        if (!double.IsFinite(p.RotationDegrees)) errors.Add("Ângulo de rotação inválido.");
        foreach (double v in new[] { p.ModelPoint.X, p.ModelPoint.Y, p.ModelPoint.Z, p.TargetPoint.X, p.TargetPoint.Y, p.TargetPoint.Z })
            if (!double.IsFinite(v)) { errors.Add("Coordenada não finita."); break; }

        if (p.Mode == GeoMode.SirgasUtm)
        {
            if (p.UtmZone is not int z || z < 1 || z > 60) errors.Add("Zona UTM deve estar entre 1 e 60.");
            if (p.Hemisphere is null) errors.Add("Hemisfério obrigatório no modo SIRGAS 2000 / UTM.");
            // Faixas plausíveis de coordenadas UTM (não substituem a conferência com a topografia).
            if (p.TargetPoint.X < 100_000 || p.TargetPoint.X > 900_000) errors.Add("Easting fora da faixa UTM plausível (100.000 a 900.000 m).");
            if (p.Hemisphere == Pasb.Georeferencing.Hemisphere.South && (p.TargetPoint.Y < 1_000_000 || p.TargetPoint.Y > 10_000_000))
                errors.Add("Northing fora da faixa plausível para o hemisfério sul (1.000.000 a 10.000.000 m).");
            if (p.Hemisphere == Pasb.Georeferencing.Hemisphere.North && (p.TargetPoint.Y < 0 || p.TargetPoint.Y > 9_400_000))
                errors.Add("Northing fora da faixa plausível para o hemisfério norte (0 a 9.400.000 m).");
            if (p.UtmZone is int zz && p.Hemisphere is { } h && EpsgSirgas2000Utm(zz, h) is null)
                errors.Add($"Zona {zz}{(h == Pasb.Georeferencing.Hemisphere.South ? "S" : "N")} fora das zonas SIRGAS 2000 / UTM tabeladas.");
        }
        else if (string.IsNullOrWhiteSpace(p.LocalCrsName))
        {
            errors.Add("Informe um nome para o sistema de coordenadas próprio (modo X, Y, Z).");
        }
        return errors;
    }

    /// <summary>Ponto do modelo (unidades do desenho) → destino (m).</summary>
    public static Point3 ToTarget(GeoParameters p, Point3 model)
    {
        double a = p.RotationDegrees * Math.PI / 180.0, c = Math.Cos(a), s = Math.Sin(a), k = p.ModelUnitToMeters;
        double dx = model.X - p.ModelPoint.X, dy = model.Y - p.ModelPoint.Y, dz = model.Z - p.ModelPoint.Z;
        return new Point3(
            p.TargetPoint.X + k * (c * dx - s * dy),
            p.TargetPoint.Y + k * (s * dx + c * dy),
            p.TargetPoint.Z + k * dz);
    }

    /// <summary>Destino (m) → modelo (unidades do desenho). Inversa exata de <see cref="ToTarget"/>.</summary>
    public static Point3 ToModel(GeoParameters p, Point3 target)
    {
        double a = p.RotationDegrees * Math.PI / 180.0, c = Math.Cos(a), s = Math.Sin(a), k = p.ModelUnitToMeters;
        double de = (target.X - p.TargetPoint.X) / k, dn = (target.Y - p.TargetPoint.Y) / k, dh = (target.Z - p.TargetPoint.Z) / k;
        return new Point3(
            p.ModelPoint.X + c * de + s * dn,
            p.ModelPoint.Y - s * de + c * dn,
            p.ModelPoint.Z + dh);
    }

    /// <summary>
    /// IfcMapConversion para geometria IFC já convertida para METROS e no sistema do modelo:
    /// Eastings/Northings/OrthogonalHeight = posição da origem do modelo no destino; X axis = (cos α, sin α); Scale = 1.
    /// </summary>
    public static MapConversion ToIfcMapConversion(GeoParameters p)
    {
        Point3 origin = ToTarget(p, new Point3(0, 0, 0));
        double a = p.RotationDegrees * Math.PI / 180.0;
        return new MapConversion(origin.X, origin.Y, origin.Z, Math.Cos(a), Math.Sin(a), 1.0);
    }

    public static ProjectedCrs ToIfcProjectedCrs(GeoParameters p)
    {
        if (p.Mode == GeoMode.SirgasUtm && p.UtmZone is int z && p.Hemisphere is { } h)
        {
            int? epsg = EpsgSirgas2000Utm(z, h);
            string zone = z + (h == Pasb.Georeferencing.Hemisphere.South ? "S" : "N");
            return new ProjectedCrs(epsg is null ? $"SIRGAS 2000 / UTM zone {zone}" : $"EPSG:{epsg}",
                $"SIRGAS 2000 / UTM zone {zone}", "SIRGAS 2000", "UTM", zone);
        }
        return new ProjectedCrs(p.LocalCrsName ?? "Sistema local", p.LocalCrsDescription, null, null, null);
    }

    /// <summary>
    /// Códigos EPSG de SIRGAS 2000 / UTM. Tabela a conferir no registro EPSG antes de uso em produção
    /// (ver GEOREFERENCING.md, seção "Implementação").
    /// Norte: zonas 11N–22N = 31965–31976. Sul: zonas 17S–25S = 31977–31985.
    /// </summary>
    public static int? EpsgSirgas2000Utm(int zone, Hemisphere h) => h switch
    {
        Pasb.Georeferencing.Hemisphere.North when zone >= 11 && zone <= 22 => 31965 + (zone - 11),
        Pasb.Georeferencing.Hemisphere.South when zone >= 17 && zone <= 25 => 31977 + (zone - 17),
        _ => null,
    };
}
