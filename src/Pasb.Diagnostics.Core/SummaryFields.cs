using System;
using System.Collections.Generic;

namespace Pasb.Diagnostics.Core;

/// <summary>
/// Getters lidos na exportação IFC resumida (<see cref="ReadProfile.ExportSummary"/>), como "ClasseDeclaradora.Membro".
/// Entram as propriedades pedidas (material, perfil, comprimento, peso por metro, peso total, área de pintura; equivalentes
/// para chapas, parafusos, chumbadores, soldas e peças especiais), a marca, e o que a geometria e os grupos de ligação usam.
/// Os métodos equivalentes ficam em ApprovedMethods (plugin), também filtrados pelo perfil.
/// </summary>
public static class SummaryFields
{
    public static readonly IReadOnlySet<string> Getters = new HashSet<string>(StringComparer.Ordinal)
    {
        // Comuns: tipo de aço, revestimento, função, nome; posição (CS) e caixa envolvente para a geometria de reserva.
        "AtomicElement.Material", "AtomicElement.Coating", "AtomicElement.Denotation", "ConstructionElement.Role",
        "ActiveConstructionElement.CS", "ConstructionElement.GeomExtents",

        // Perfis: nome e tipo de seção, comprimento de sistema; posição da seção para o corpo paramétrico de reserva.
        "Beam.ProfSectionType", "Beam.ProfSectionName", "Beam.ProfName", "Beam.SysLength", "Beam.PhysCSStart", "Beam.IsCrossSectionMirrored",

        // Chapas
        "PlateBase.Thickness", "PlateBase.Length", "PlateBase.Width", "PlateBase.PlateNormal", "PlateBase.Portioning",

        // Parafusos e chumbadores
        "BoltPattern.Normal", "BoltPattern.XDirection", "BoltPattern.YDirection", "BoltPattern.NumberOfScrews",
        "ScrewBoltPattern.Standard", "ScrewBoltPattern.Grade", "ScrewBoltPattern.BoltAssembly",
        "ScrewBoltPattern.ScrewDiameter", "ScrewBoltPattern.ScrewLength",
        "ScrewBoltPattern.BoltHeadDiameter", "ScrewBoltPattern.BoltHeadHeight", "ScrewBoltPattern.NutDiameter", "ScrewBoltPattern.NutHeight",
        "AnchorPattern.NumberOfScrews", "AnchorPattern.Length", "AnchorPattern.AnchorType", "AnchorPattern.AnchorPartName",

        // Soldas
        "WeldPattern.Thickness", "WeldPattern.Length",

        // Peças especiais
        "SpecialPart.BlockName", "SpecialPart.Weight", "SpecialPart.PaintArea",

        // Ligações: só as referências às peças, para montar os grupos IFC.
        "PassiveConstructionObject.Drivers", "PassiveConstructionObject.Drivens",
        "UserAutoConstructionObject.InputObjects", "UserAutoConstructionObject.CreatedObjects",
    };
}
