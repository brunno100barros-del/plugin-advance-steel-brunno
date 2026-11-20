using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using Autodesk.AdvanceSteel.CADAccess;
using Autodesk.AdvanceSteel.ConstructionTypes;
using Autodesk.AdvanceSteel.Geometry;
using Autodesk.AdvanceSteel.Modeler;
using Autodesk.AdvanceSteel.Modelling;
using Pasb.Diagnostics.Core;

namespace Pasb.Diagnostics.AdvanceSteel;

/// <summary>
/// Métodos de leitura aprovados, chamados por código explícito (docs/DIAGNOSTIC-DESIGN.md, seção 5.3).
/// Regra: só métodos classificados como leitura em docs/API-CLASS-MATRIX.md, revisados um a um.
/// Proibido aqui: setters, Set*/Add*/Del*/Remove*/Move*/Connect/Transform*/Update*/Save*/Load*/Export*/Create*/Check*/Presize*.
/// </summary>
internal static class ApprovedMethods
{
    /// <summary>Quantidade de User Attributes: kUserAttr1..kUserAttr10 em Modelling.eObjectsAttribute.</summary>
    public const int UserAttributeCount = 10;

    public static void Read(FilerObject o, RecordBuilder b)
    {
        if (o is ConstructionElement ce) AttributeStatus(ce, b);
        if (o is AtomicElement ae) Atomic(ae, b);
        if (o is Beam beam) BeamMethods(beam, b);
        if (o is PlateBase plate) PlateMethods(plate, b);
        if (o is BoltPattern bolts) BoltMethods(bolts, b);
        if (o is WeldPattern weld) WeldMethods(weld, b);
        if (o is FeatureObject feat) M(b, "FeatureObject", "GetOwner", null, typeof(FilerObject), () => feat.GetOwner());
        if (o is UserAutoConstructionObject joint) JointMethods(joint, b);
    }

    private static void M(RecordBuilder b, string declaring, string name, string? arg, Type t, Func<object?> f) =>
        b.Read(name, declaring, ReadSource.Method, arg, t, f);

    /// <summary>Converte saídas "out" num objeto JSON, sem perda.</summary>
    private static JsonObject Outs(params (string name, object? value)[] values)
    {
        var builder = new RecordBuilder(AsValueSerializer.TrySerialize);
        var j = new JsonObject();
        foreach (var (name, value) in values)
        {
            PropertyRecord r = builder.Serialize(name, "", "", null, value?.GetType() ?? typeof(object), value);
            j[name] = r.Status == ReadStatus.Null ? null : r.RawValue?.DeepClone();
        }
        return j;
    }

    // ------------------------------------------------------------------ AtomicElement

    private static void Atomic(AtomicElement ae, RecordBuilder b)
    {
        const string d = "AtomicElement";
        M(b, d, "GetSinglePartPositionNumber", null, typeof(string), () => ae.GetSinglePartPositionNumber());
        M(b, d, "GetMainPartPositionNumber", null, typeof(string), () => ae.GetMainPartPositionNumber());
        M(b, d, "GetNumberingStatus", null, typeof(JsonObject), () =>
        {
            ae.GetNumberingStatus(out bool sp, out bool mp);
            return Outs(("hasValidSPNumber", sp), ("hasValidMPNumber", mp));
        });
        M(b, d, "GetBalancepoint", null, typeof(JsonObject), () =>
        {
            bool ok = ae.GetBalancepoint(out Point3d center, out double weight);
            return Outs(("returned", ok), ("ptCenter", center), ("dWeight", weight));
        });
        M(b, d, "GetDSTVName", null, typeof(JsonObject), () =>
        {
            string name = ae.GetDSTVName(out int alt);
            return Outs(("returned", name), ("alternativSearched", alt));
        });
        M(b, d, "GetName", null, typeof(JsonObject), () =>
        {
            string name = ae.GetName(out int alt);
            return Outs(("returned", name), ("alternativSearched", alt));
        });
        M(b, d, "GetExternalNames", null, typeof(JsonObject), () =>
        {
            int n = ae.GetExternalNames(out IEnumerable<string> names, out IEnumerable<int> found, out IEnumerable<int> alt);
            return Outs(("returned", n), ("externalNames", names?.ToList()), ("found", found?.ToList()), ("alternativeSearching", alt?.ToList()));
        });
        M(b, d, "GetQuantityInModel", null, typeof(int), () => ae.GetQuantityInModel());
        M(b, d, "GetConnection", null, typeof(object), () => ae.GetConnection());
        M(b, d, "GetFeatures", "true", typeof(object), () => ae.GetFeatures(true)?.ToList());
        M(b, d, "GetFeatures", "false", typeof(object), () => ae.GetFeatures(false)?.ToList());

        // Todos os índices, inclusive vazios e com erro (corrige a perda do MCP, Objects.cs:343-353).
        for (int i = 0; i < UserAttributeCount; i++)
        {
            int idx = i;
            M(b, d, "GetUserAttribute", idx.ToString(), typeof(string), () => ae.GetUserAttribute(idx));
            M(b, d, "GetUserAttributeUsedForNumbering", idx.ToString(), typeof(int), () => ae.GetUserAttributeUsedForNumbering(idx));
        }

        // Geometria sólida: na primeira fase, só se existe corpo e quantos (seção 5.3).
        M(b, d, "GetModelerBodyList", nameof(BodyContext.eBodyContext.kNormal), typeof(JsonObject), () =>
        {
            bool ok = ae.GetModelerBodyList(BodyContext.eBodyContext.kNormal, out ModelerBody[] bodies);
            return Outs(("returned", ok), ("bodyCount", bodies?.Length));
        });
    }

    // ------------------------------------------------------------------ ConstructionElement

    /// <summary>
    /// getAttributeStatus para cada atributo de eObjectsAttribute aplicável à classe:
    /// os de prefixo da categoria (kBeam_, kPlate_...) e os genéricos, sem "_" (kMaterial, kUserAttr1...).
    /// </summary>
    private static void AttributeStatus(ConstructionElement ce, RecordBuilder b)
    {
        string[] prefixes = PrefixesFor(ce);
        foreach (string name in Enum.GetNames(typeof(eObjectsAttribute)))
        {
            if (name == nameof(eObjectsAttribute.kMaxAttribute)) continue;
            bool generic = !name.Contains('_');
            if (!generic && !prefixes.Any(p => name.StartsWith(p, StringComparison.Ordinal))) continue;
            var attr = (eObjectsAttribute)Enum.Parse(typeof(eObjectsAttribute), name);
            M(b, "ConstructionElement", "getAttributeStatus", name, typeof(int), () => ce.getAttributeStatus(attr));
        }
    }

    private static string[] PrefixesFor(ConstructionElement ce) => ce switch
    {
        Beam => new[] { "kBeam_", "kBeamPoly_", "kBeamUnfolded_" },
        PlateBase => new[] { "kPlate_", "kFoldedPlaterelation_" },
        AnchorPattern => new[] { "kScrew_", "kAnchor_" },
        ScrewBoltPattern => new[] { "kScrew_", "kCountAble_", "kCircelScrew_", "kFinitRect_", "kInfinitRect_" },
        WeldPattern => new[] { "kWeld_", "kWeldLevel1_" },
        SpecialPart => new[] { "kSpecialPart_" },
        _ => NamePrefixes(ce.GetType()),
    };

    /// <summary>Classes sem tipo referenciado aqui (concreto, grid, conector): por nome do tipo.</summary>
    private static string[] NamePrefixes(Type t)
    {
        var names = new List<string>();
        for (Type? cur = t; cur is not null; cur = cur.BaseType) names.Add(cur.Name);
        if (names.Contains("Wall")) return new[] { "kWall_" };
        if (names.Contains("Slab")) return new[] { "kSlab_" };
        if (names.Contains("FootingIsolated")) return new[] { "kFootingIsolated_" };
        if (names.Contains("Grid")) return new[] { "kGrid_" };
        if (names.Contains("Connector")) return new[] { "kConnector_" };
        return Array.Empty<string>();
    }

    // ------------------------------------------------------------------ Beam

    private static void BeamMethods(Beam beam, RecordBuilder b)
    {
        const string d = "Beam";
        M(b, d, "GetLength", null, typeof(double), () => beam.GetLength());
        M(b, d, "GetPaintArea", null, typeof(double), () => beam.GetPaintArea());
        M(b, d, "GetWeightPerMeter", null, typeof(double), () => beam.GetWeightPerMeter());
        // Código 1, o mesmo usado pelo MCP (Objects.cs:253); outros códigos dependem de investigação.
        M(b, d, "GetWeight", "1", typeof(double), () => beam.GetWeight(1));
        M(b, d, "GetPointAtStart", null, typeof(Point3d), () => beam.GetPointAtStart());
        M(b, d, "GetPointAtEnd", null, typeof(Point3d), () => beam.GetPointAtEnd());
        M(b, d, "GetLengthOffset", null, typeof(JsonObject), () =>
        {
            beam.GetLengthOffset(out double start, out double end);
            return Outs(("dStart", start), ("dEnd", end));
        });
        M(b, d, "GetSawInformation", null, typeof(JsonObject), () =>
        {
            int r = beam.GetSawInformation(out double saw, out double fs, out double ws, out double fe, out double we);
            return Outs(("returned", r), ("sawLength", saw), ("flangeAngleAtStart", fs), ("webAngleAtStart", ws),
                        ("flangeAngleAtEnd", fe), ("webAngleAtEnd", we));
        });
        M(b, d, "GetProfType", null, typeof(object), () => beam.GetProfType());
    }

    // ------------------------------------------------------------------ PlateBase

    private static void PlateMethods(PlateBase p, RecordBuilder b)
    {
        const string d = "PlateBase";
        M(b, d, "GetPhysLengthAndWidth", null, typeof(JsonObject), () =>
        {
            p.GetPhysLengthAndWidth(out double length, out double width);
            return Outs(("dLength", length), ("dWidth", width));
        });
        M(b, d, "GetArea", null, typeof(double), () => p.GetArea());
        M(b, d, "GetWeight", null, typeof(double), () => p.GetWeight());
        M(b, d, "GetCircumference", null, typeof(double), () => p.GetCircumference());
        M(b, d, "GetPaintArea", null, typeof(double), () => p.GetPaintArea());
        M(b, d, "IsRectangular", null, typeof(bool), () => p.IsRectangular());
        // dZPos = 0, como o MCP (Objects.cs:288); outros valores a investigar.
        M(b, d, "GetBaseContourPolygon", "0", typeof(Point3d[]), () =>
        {
            p.GetBaseContourPolygon(0, out Point3d[] pts);
            return pts;
        });
    }

    // ------------------------------------------------------------------ Bolts / anchors

    private static void BoltMethods(BoltPattern bp, RecordBuilder b)
    {
        M(b, "BoltPattern", "GetMidpoints", null, typeof(Point3d[]), () =>
        {
            bp.GetMidpoints(out Point3d[] pts);
            return pts;
        });
        M(b, "BoltPattern", "GetHoles", null, typeof(object), () =>
        {
            bp.GetHoles(out Autodesk.AdvanceSteel.Connection.ConnectionHoleFeature[] holes);
            return holes;
        });
        // AnchorPattern redeclara GetWeight; chama a versão mais derivada.
        if (bp is AnchorPattern ap) M(b, "AnchorPattern", "GetWeight", null, typeof(double), () => ap.GetWeight());
        else if (bp is ScrewBoltPattern sb) M(b, "ScrewBoltPattern", "GetWeight", null, typeof(double), () => sb.GetWeight());
    }

    // ------------------------------------------------------------------ Welds

    private static void WeldMethods(WeldPattern w, RecordBuilder b)
    {
        const string d = "WeldPattern";
        foreach (WeldPattern.eSeamPosition pos in new[] { WeldPattern.eSeamPosition.kUpper, WeldPattern.eSeamPosition.kLower })
        {
            WeldPattern.eSeamPosition s = pos;
            string arg = s.ToString();
            M(b, d, "GetWeldType", arg, typeof(WeldPattern.eWeldType), () => w.GetWeldType(s));
            M(b, d, "GetSeamThickness", arg, typeof(double), () => w.GetSeamThickness(s));
            M(b, d, "GetFaceShape", arg, typeof(object), () => w.GetFaceShape(s));
            M(b, d, "GetSeamExecuting", arg, typeof(object), () => w.GetSeamExecuting(s));
        }
        M(b, d, "GetCenterPoint", null, typeof(Point3d), () => w.GetCenterPoint());
        M(b, d, "IsCombiWelding", null, typeof(int), () => w.IsCombiWelding());
        M(b, d, "IsLowerWeldingChangeable", null, typeof(int), () => w.IsLowerWeldingChangeable());
    }

    // ------------------------------------------------------------------ Joints

    /// <summary>
    /// Só consultas. Save() + IFiler.GetItems() continuam proibidos até o teste isolado (seção 5.5).
    /// InputObjects, CreatedObjects etc. já são lidos como getters.
    /// </summary>
    private static void JointMethods(UserAutoConstructionObject j, RecordBuilder b)
    {
        const string d = "UserAutoConstructionObject";
        // getPrimaryJoint derrubou o AutoCAD (eNotOpenForWrite) na ligação 305 do Portal.dwg tanto em transação
        // somente leitura (Teste 4) quanto com escrita permitida e descarte (Teste 6). Excluído em qualquer modo.
        b.Unavailable("getPrimaryJoint", d, ReadSource.Method, typeof(FilerObject),
            "Excluído: a chamada derrubou o AutoCAD (eNotOpenForWrite) nos Testes 4 e 6, em qualquer modo de transação.");
        M(b, d, "isPrimaryOrPartOfPrimaryJoint", null, typeof(bool), () => j.isPrimaryOrPartOfPrimaryJoint());
        M(b, d, "isSecondaryOrPartOfSecondaryJoint", null, typeof(bool), () => j.isSecondaryOrPartOfSecondaryJoint());
    }
}
