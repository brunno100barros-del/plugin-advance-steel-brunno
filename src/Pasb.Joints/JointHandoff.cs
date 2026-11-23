using System;
using Autodesk.AdvanceSteel.CADAccess;
using Autodesk.AdvanceSteel.CADLink.Database;

namespace Pasb.Joints;

/// <summary>
/// Avisa o plugin PASB (outra DLL, mesmo processo) de qual ligação acabou de ser criada, para ele abrir a janela de
/// parâmetros logo depois dos cliques. Usa um dado do AppDomain: sem referência entre as DLLs.
/// </summary>
internal static class JointHandoff
{
    public const string Key = "PASB_NEW_JOINT_HANDLES";

    public static void Publish(ObjectId jointId)
    {
        try
        {
            string own = jointId.Handle ?? "", repr = "";
            try { if (DatabaseManager.Open(jointId) is FilerObject j) repr = DatabaseManager.GetReprId(j)?.Handle ?? ""; } catch (Exception) { }
            AppDomain.CurrentDomain.SetData(Key, repr + "|" + own);
        }
        catch (Exception) { }
    }
}
