using System;
using System.Drawing;
using System.Windows.Forms;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;
using AcDoc = Autodesk.AutoCAD.ApplicationServices.Document;

[assembly: CommandClass(typeof(Pasb.Diagnostics.AdvanceSteel.JointsMenuCommand))]

namespace Pasb.Diagnostics.AdvanceSteel;

/// <summary>
/// Menu de ligações do PASB (PASB_LIGACOES) e o comando do sistema antidobra (PASB_ANTIDOBRA).
/// As ligações são nativas (PasbJoints.dll registrada por Registrar-Macros-PASB.cmd): o comando só chama
/// AstM4CrConByVB com o nome interno da ligação.
/// </summary>
public sealed class JointsMenuCommand
{
    /// <summary>Ligações do PASB: título, comando PASB e descrição curta.</summary>
    internal static readonly (string title, string command, string help)[] Joints =
    {
        ("Contraventamento em X", "PASB_CONTRAVENTAMENTO",
            "Barras redondas em X com esticador, gusset e chapa de base cortada no banzo. Clique os 2 cantos opostos (plano XY da UCS)."),
        ("Sistema antidobra (correntes de terça)", "PASB_ANTIDOBRA",
            "Agulhas entre as almas das terças, barras rígidas com cantoneiras parafusadas e tirantes diagonais. Selecione as terças de uma água e os pórticos A e B."),
    };

    [CommandMethod("PASB", "PASB_ANTIDOBRA", CommandFlags.Modal)]
    public void SagRods()
    {
        AcDoc? doc = AcApp.DocumentManager.MdiActiveDocument;
        if (doc == null) return;
        Editor ed = doc.Editor;
        if (!string.Equals(doc.Database.Insunits.ToString(), "Millimeters", StringComparison.OrdinalIgnoreCase))
        {
            ed.WriteMessage($"\nPASB: o sistema antidobra usa medidas em milímetros, mas o desenho está em \"{doc.Database.Insunits}\".\n");
            return;
        }
        JointsState st = NativeJointsState();
        if (st == JointsState.Missing)
        {
            ed.WriteMessage("\nPASB: a ligação antidobra precisa das ligações nativas do PASB, que não estão registradas neste computador.\n");
            MessageBox.Show(NativeJointsWarning(st), "PASB — Sistema antidobra", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (st == JointsState.Outdated)
            MessageBox.Show(NativeJointsWarning(st), "PASB — Sistema antidobra", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        StartJoint(doc, "PasbAntidobra");
    }

    private const string HandoffKey = "PASB_NEW_JOINT_HANDLES";   // igual a Pasb.Joints.JointHandoff.Key

    /// <summary>
    /// Cria a ligação nativa e, quando o comando terminar, abre a janela de parâmetros da ligação recém-criada
    /// (a ligação avisa qual é pelo AppDomain). Se não der para abrir, a ligação fica criada e edita com duplo clique.
    /// </summary>
    internal static void StartJoint(AcDoc doc, string internalName)
    {
        AppDomain.CurrentDomain.SetData(HandoffKey, null);
        void Done(object? s, Autodesk.AutoCAD.ApplicationServices.CommandEventArgs e)
        {
            if (e.GlobalCommandName.IndexOf("CRCONBYVB", StringComparison.OrdinalIgnoreCase) < 0) return;
            doc.CommandEnded -= Done; doc.CommandCancelled -= Cancelled; doc.CommandFailed -= Cancelled;
            try { OpenNewJoint(doc); } catch (System.Exception ex) { StartupLog.Write("Abrir janela da ligação falhou: " + ex); }
        }
        void Cancelled(object? s, Autodesk.AutoCAD.ApplicationServices.CommandEventArgs e)
        {
            if (e.GlobalCommandName.IndexOf("CRCONBYVB", StringComparison.OrdinalIgnoreCase) < 0) return;
            doc.CommandEnded -= Done; doc.CommandCancelled -= Cancelled; doc.CommandFailed -= Cancelled;
        }
        doc.CommandEnded += Done; doc.CommandCancelled += Cancelled; doc.CommandFailed += Cancelled;
        doc.SendStringToExecute($"_AstM4CrConByVB {internalName} ", true, false, false);
    }

    private static void OpenNewJoint(AcDoc doc)
    {
        if (AppDomain.CurrentDomain.GetData(HandoffKey) is not string data || data.Length == 0) return;
        AppDomain.CurrentDomain.SetData(HandoffKey, null);
        foreach (string h in data.Split('|'))
        {
            if (h.Length == 0) continue;
            try
            {
                if (!doc.Database.TryGetObjectId(new Autodesk.AutoCAD.DatabaseServices.Handle(Convert.ToInt64(h, 16)), out var id) || id.IsNull || id.IsErased) continue;
                if (!id.ObjectClass.IsDerivedFrom(Autodesk.AutoCAD.Runtime.RXObject.GetClass(typeof(Autodesk.AutoCAD.DatabaseServices.Entity)))) continue;
                doc.Editor.SetImpliedSelection(new[] { id });
                doc.SendStringToExecute("_AstM4ChangeUserConstructionObject ", true, false, false);
                return;
            }
            catch (System.Exception) { }
        }
        doc.Editor.WriteMessage("\nPASB: ligação criada. Para ver as opções, dê duplo clique na caixa cinza.\n");
    }

    [CommandMethod("PASB", "PASB_LIGACOES", CommandFlags.Modal)]
    public void Menu()
    {
        AcDoc? doc = AcApp.DocumentManager.MdiActiveDocument;
        if (doc == null) return;
        using var dlg = new JointsMenuForm(NativeJointsInstalled());
        if (Autodesk.AutoCAD.ApplicationServices.Application.ShowModalDialog(dlg) != DialogResult.OK || dlg.Command == null) return;
        doc.SendStringToExecute(dlg.Command + " ", true, false, false);
    }

    /// <summary>PasbJoints.dll está na pasta do Advance Steel (onde ele carrega as ligações registradas).</summary>
    internal static bool NativeJointsInstalled() => NativeJointsState() != JointsState.Missing;

    internal enum JointsState { Missing, Outdated, Ok }

    /// <summary>
    /// Missing: PasbJoints.dll não está na pasta do Advance Steel (macros nunca registradas neste computador).
    /// Outdated: está, mas é diferente da DLL desta versão do plugin (Contents\Joints): o Advance Steel usa a antiga.
    /// </summary>
    internal static JointsState NativeJointsState()
    {
        try
        {
            string advs = System.IO.Path.GetDirectoryName(typeof(Autodesk.AdvanceSteel.CADAccess.FilerObject).Assembly.Location) ?? "";
            string installed = System.IO.Path.Combine(advs, "PasbJoints.dll");
            if (!System.IO.File.Exists(installed)) return JointsState.Missing;
            string bundled = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(typeof(JointsMenuCommand).Assembly.Location) ?? "", "Joints", "PasbJoints.dll");
            if (!System.IO.File.Exists(bundled)) return JointsState.Ok;
            return SameFile(installed, bundled) ? JointsState.Ok : JointsState.Outdated;
        }
        catch (System.Exception) { return JointsState.Missing; }
    }

    private static bool SameFile(string a, string b)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        using var fa = System.IO.File.OpenRead(a);
        using var fb = System.IO.File.OpenRead(b);
        return sha.ComputeHash(fa).AsSpan().SequenceEqual(System.Security.Cryptography.SHA256.Create().ComputeHash(fb));
    }

    /// <summary>Texto do aviso quando as ligações nativas faltam ou estão desatualizadas (null = tudo certo).</summary>
    internal static string? NativeJointsWarning(JointsState st) => st switch
    {
        JointsState.Missing =>
            "As ligações nativas do PASB (Contraventamento em X estilo steelXbim e Sistema antidobra) não estão registradas neste computador.\n\n" +
            "Para registrar: feche o Advance Steel e rode o instalador do PASB de novo (ele registra as ligações; o Windows pede permissão de administrador), " +
            "ou dê dois cliques em Registrar-Macros-PASB.cmd, no pacote .zip.",
        JointsState.Outdated =>
            "As ligações nativas do PASB registradas no Advance Steel são de outra versão do plugin: o Advance Steel vai usar a versão antiga.\n\n" +
            "Para atualizar: feche o Advance Steel e rode o instalador do PASB de novo (ou Registrar-Macros-PASB.cmd).",
        _ => null,
    };

    private sealed class JointsMenuForm : Form
    {
        public string? Command { get; private set; }

        public JointsMenuForm(bool native)
        {
            Text = "PASB — Ligações";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(520, 300);
            Font = new Font("Segoe UI", 9f);

            var list = new ListView
            {
                View = View.Details, FullRowSelect = true, MultiSelect = false, HideSelection = false,
                Location = new Point(12, 12), Size = new Size(496, 150), HeaderStyle = ColumnHeaderStyle.Nonclickable,
            };
            list.Columns.Add("Ligação", 480);
            foreach (var j in Joints) list.Items.Add(new ListViewItem(j.title) { Tag = j.command });
            var help = new Label { Location = new Point(12, 170), Size = new Size(496, 64), ForeColor = Color.FromArgb(60, 60, 60) };
            var status = new Label
            {
                Location = new Point(12, 236), Size = new Size(330, 40),
                Text = native ? "Ligações nativas: duplo clique na caixa cinza para editar."
                              : "Macros nativas não registradas: rode Registrar-Macros-PASB.cmd com o Advance Steel fechado.",
                ForeColor = native ? Color.FromArgb(40, 110, 60) : Color.FromArgb(170, 60, 30),
            };
            var ok = new Button { Text = "Criar", Location = new Point(352, 262), Size = new Size(75, 26), Enabled = false };
            var cancel = new Button { Text = "Fechar", Location = new Point(433, 262), Size = new Size(75, 26), DialogResult = DialogResult.Cancel };
            list.SelectedIndexChanged += (_, _) =>
            {
                int i = list.SelectedIndices.Count > 0 ? list.SelectedIndices[0] : -1;
                help.Text = i >= 0 ? Joints[i].help : "";
                ok.Enabled = i >= 0;
            };
            void Accept() { if (list.SelectedItems.Count == 0) return; Command = list.SelectedItems[0].Tag as string; DialogResult = DialogResult.OK; Close(); }
            ok.Click += (_, _) => Accept();
            list.DoubleClick += (_, _) => Accept();
            Controls.AddRange(new Control[] { list, help, status, ok, cancel });
            AcceptButton = ok; CancelButton = cancel;
            if (list.Items.Count > 0) list.Items[0].Selected = true;
        }
    }
}
