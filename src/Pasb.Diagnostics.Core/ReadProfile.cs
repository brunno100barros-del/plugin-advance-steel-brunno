namespace Pasb.Diagnostics.Core;

/// <summary>Como o modelo é lido (docs/EXPORT-PERFORMANCE.md).</summary>
public enum ReadProfile
{
    /// <summary>
    /// Diagnóstico: tudo, com o trace gravado no disco físico antes de cada leitura, para localizar a leitura que derruba o AutoCAD.
    /// Usado por PASB_DIAG_SEL, PASB_DIAG_ALL e pela leitura de objetos do Conector IA.
    /// </summary>
    Diagnostic,

    /// <summary>
    /// Trabalho (exportação IFC, lista de material, numeração, leitura do modelo pelo Conector IA): as mesmas propriedades,
    /// menos as leituras caras de ApprovedMethods.OmittedInExport; trace só por objeto e sem esperar o disco físico.
    /// </summary>
    Export,
}
