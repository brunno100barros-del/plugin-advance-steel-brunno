using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;

namespace ApiInventory;

/// <summary>
/// Inventário somente leitura das assemblies .NET do Advance Steel.
/// Lê os metadados dos arquivos com System.Reflection.Metadata: nenhuma assembly da Autodesk
/// é carregada no processo e nenhum código dela é executado.
/// </summary>
internal static class Program
{
    private const string DefaultAsmDir = @"C:\Program Files\Autodesk\AutoCAD 2027\ADVS";
    private const string DefaultPattern = "AS*Mgd.dll";

    /// <summary>Classes resumidas no relatório de classes centrais.</summary>
    private static readonly string[] KeyTypes =
    {
        "Autodesk.AdvanceSteel.CADAccess.FilerObject",
        "Autodesk.AdvanceSteel.ConstructionTypes.ConstructionElement",
        "Autodesk.AdvanceSteel.ConstructionTypes.AtomicElement",
        "Autodesk.AdvanceSteel.Modelling.Beam",
        "Autodesk.AdvanceSteel.Modelling.StraightBeam",
        "Autodesk.AdvanceSteel.Modelling.PolyBeam",
        "Autodesk.AdvanceSteel.Modelling.PlateBase",
        "Autodesk.AdvanceSteel.Modelling.Plate",
        "Autodesk.AdvanceSteel.Modelling.ScrewBoltPattern",
        "Autodesk.AdvanceSteel.Modelling.AnchorPattern",
        "Autodesk.AdvanceSteel.Modelling.WeldPattern",
        "Autodesk.AdvanceSteel.Modelling.SpecialPart",
        "Autodesk.AdvanceSteel.ConstructionTypes.FeatureObject",
        "Autodesk.AdvanceSteel.ConstructionTypes.PassiveConstructionObject",
        "Autodesk.AdvanceSteel.ConstructionTypes.UserAutoConstructionObject",
        "Autodesk.AdvanceSteel.ConstructionTypes.IJoint",
        "Autodesk.AdvanceSteel.ConstructionTypes.IFiler",
    };

    private static int Main(string[] args)
    {
        string asmDir = Arg(args, "--asm-dir") ?? DefaultAsmDir;
        string pattern = Arg(args, "--pattern") ?? DefaultPattern;
        string outDir = Path.GetFullPath(Arg(args, "--out") ?? "inventory-output");

        if (!Directory.Exists(asmDir))
        {
            Console.Error.WriteLine($"Pasta de assemblies não encontrada: {asmDir}");
            return 1;
        }

        string[] files = Directory.GetFiles(asmDir, pattern).OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToArray();
        if (files.Length == 0)
        {
            Console.Error.WriteLine($"Nenhum arquivo '{pattern}' em {asmDir}");
            return 1;
        }

        var assemblies = new List<AssemblyInfo>();
        foreach (string file in files)
        {
            try { assemblies.Add(AssemblyReader.Read(file)); }
            catch (Exception ex) { assemblies.Add(AssemblyInfo.Failed(file, ex)); }
        }

        Directory.CreateDirectory(outDir);
        var inventory = new Inventory(
            Tool: "ApiInventory",
            GeneratedAtUtc: DateTime.UtcNow,
            AssemblyDirectory: asmDir,
            Assemblies: assemblies);

        var json = new JsonSerializerOptions { WriteIndented = true };
        File.WriteAllText(Path.Combine(outDir, "api-2027.json"), JsonSerializer.Serialize(inventory, json), new UTF8Encoding(false));
        Reports.WriteMembersCsv(Path.Combine(outDir, "api-2027-members.csv"), assemblies);
        string keyReport = Reports.KeyClasses(assemblies, KeyTypes);
        File.WriteAllText(Path.Combine(outDir, "api-2027-key-classes.txt"), keyReport, new UTF8Encoding(false));

        foreach (AssemblyInfo a in assemblies)
            Console.WriteLine(a.Error is null
                ? $"{a.File,-22} {a.FileVersion,-14} tipos públicos: {a.Types.Count,5}  membros: {a.Types.Sum(t => t.Members.Count),6}"
                : $"{a.File,-22} ERRO: {a.Error}");
        Console.WriteLine($"Saída: {outDir}");
        return assemblies.Any(a => a.Error is not null) ? 2 : 0;
    }

    private static string? Arg(string[] args, string name)
    {
        int i = Array.IndexOf(args, name);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }
}

internal sealed record Inventory(string Tool, DateTime GeneratedAtUtc, string AssemblyDirectory, List<AssemblyInfo> Assemblies);

internal sealed record AssemblyInfo(
    string File, string? FileVersion, string? AssemblyName, string? AssemblyVersion,
    bool HasXmlDocumentation, List<TypeInfo> Types, string? Error)
{
    public static AssemblyInfo Failed(string path, Exception ex) =>
        new(Path.GetFileName(path), null, null, null, false, new List<TypeInfo>(), ex.GetType().Name + ": " + ex.Message);
}

internal sealed record TypeInfo(
    string FullName, string Kind, string? BaseType, List<string> Interfaces,
    bool IsAbstract, bool IsSealed, bool Documented, List<MemberInfo> Members);

internal sealed record MemberInfo(
    string Kind, string Name, string Access, bool IsStatic, string Signature,
    bool? CanRead, bool? CanWrite, string? ConstantValue, bool Documented);

internal static class AssemblyReader
{
    public static AssemblyInfo Read(string path)
    {
        using var stream = File.OpenRead(path);
        using var pe = new PEReader(stream);
        if (!pe.HasMetadata)
            throw new BadImageFormatException("Arquivo sem metadados .NET.");

        MetadataReader md = pe.GetMetadataReader();
        var fmt = new SignatureFormatter();
        XmlDocs docs = XmlDocs.Load(Path.ChangeExtension(path, ".xml"));

        string? asmName = null, asmVersion = null;
        if (md.IsAssembly)
        {
            AssemblyDefinition ad = md.GetAssemblyDefinition();
            asmName = md.GetString(ad.Name);
            asmVersion = ad.Version.ToString();
        }

        var types = new List<TypeInfo>();
        foreach (TypeDefinitionHandle th in md.TypeDefinitions)
        {
            TypeDefinition td = md.GetTypeDefinition(th);
            if (!IsVisible(md, td)) continue;
            string fullName = Names.Of(md, th);
            if (fullName == "<Module>") continue;

            string? baseType = Names.Of(md, td.BaseType, fmt);
            string kind = KindOf(td, baseType);
            var interfaces = td.GetInterfaceImplementations()
                .Select(h => Names.Of(md, md.GetInterfaceImplementation(h).Interface, fmt) ?? "?")
                .ToList();

            string docType = fullName.Replace('+', '.');
            var members = new List<MemberInfo>();
            ReadFields(md, td, fmt, docs, docType, kind, members);
            ReadProperties(md, td, fmt, docs, docType, members);
            ReadMethods(md, td, fmt, docs, docType, members);
            ReadEvents(md, td, fmt, docs, docType, members);

            types.Add(new TypeInfo(
                fullName, kind, baseType, interfaces,
                (td.Attributes & TypeAttributes.Abstract) != 0,
                (td.Attributes & TypeAttributes.Sealed) != 0,
                docs.Has("T:" + docType),
                members));
        }

        return new AssemblyInfo(
            Path.GetFileName(path),
            System.Diagnostics.FileVersionInfo.GetVersionInfo(path).FileVersion,
            asmName, asmVersion, docs.Loaded,
            types.OrderBy(t => t.FullName, StringComparer.Ordinal).ToList(),
            null);
    }

    private static bool IsVisible(MetadataReader md, TypeDefinition td)
    {
        TypeAttributes vis = td.Attributes & TypeAttributes.VisibilityMask;
        if (vis == TypeAttributes.Public) return true;
        if (vis is TypeAttributes.NestedPublic or TypeAttributes.NestedFamily or TypeAttributes.NestedFamORAssem)
            return IsVisible(md, md.GetTypeDefinition(td.GetDeclaringType()));
        return false;
    }

    private static string KindOf(TypeDefinition td, string? baseType)
    {
        if ((td.Attributes & TypeAttributes.Interface) != 0) return "interface";
        return baseType switch
        {
            "System.Enum" => "enum",
            "System.ValueType" => "struct",
            "System.MulticastDelegate" => "delegate",
            _ => "class",
        };
    }

    private static string AccessOf(MethodAttributes a) => (a & MethodAttributes.MemberAccessMask) switch
    {
        MethodAttributes.Public => "public",
        MethodAttributes.Family => "protected",
        MethodAttributes.FamORAssem => "protected internal",
        _ => "",
    };

    private static string AccessOf(FieldAttributes a) => (a & FieldAttributes.FieldAccessMask) switch
    {
        FieldAttributes.Public => "public",
        FieldAttributes.Family => "protected",
        FieldAttributes.FamORAssem => "protected internal",
        _ => "",
    };

    private static void ReadFields(MetadataReader md, TypeDefinition td, SignatureFormatter fmt, XmlDocs docs, string docType, string kind, List<MemberInfo> members)
    {
        foreach (FieldDefinitionHandle fh in td.GetFields())
        {
            FieldDefinition f = md.GetFieldDefinition(fh);
            string access = AccessOf(f.Attributes);
            if (access.Length == 0) continue;
            string name = md.GetString(f.Name);
            if (kind == "enum" && name == "value__") continue;

            string type;
            try { type = f.DecodeSignature(fmt, null); } catch (Exception ex) { type = "<erro: " + ex.Message + ">"; }

            string? constant = null;
            ConstantHandle ch = f.GetDefaultValue();
            if (!ch.IsNil) constant = ReadConstant(md, md.GetConstant(ch));

            members.Add(new MemberInfo(
                kind == "enum" ? "enum-value" : "field", name, access,
                (f.Attributes & FieldAttributes.Static) != 0, type, null, null, constant,
                docs.Has($"F:{docType}.{name}")));
        }
    }

    private static void ReadProperties(MetadataReader md, TypeDefinition td, SignatureFormatter fmt, XmlDocs docs, string docType, List<MemberInfo> members)
    {
        foreach (PropertyDefinitionHandle ph in td.GetProperties())
        {
            PropertyDefinition p = md.GetPropertyDefinition(ph);
            PropertyAccessors acc = p.GetAccessors();
            string getAccess = acc.Getter.IsNil ? "" : AccessOf(md.GetMethodDefinition(acc.Getter).Attributes);
            string setAccess = acc.Setter.IsNil ? "" : AccessOf(md.GetMethodDefinition(acc.Setter).Attributes);
            if (getAccess.Length == 0 && setAccess.Length == 0) continue;

            string name = md.GetString(p.Name);
            string sig;
            try
            {
                MethodSignature<string> s = p.DecodeSignature(fmt, null);
                sig = s.ParameterTypes.Length == 0 ? s.ReturnType : $"{s.ReturnType} [{string.Join(", ", s.ParameterTypes)}]";
            }
            catch (Exception ex) { sig = "<erro: " + ex.Message + ">"; }

            MethodDefinitionHandle any = acc.Getter.IsNil ? acc.Setter : acc.Getter;
            bool isStatic = (md.GetMethodDefinition(any).Attributes & MethodAttributes.Static) != 0;

            members.Add(new MemberInfo(
                "property", name, getAccess == "public" || setAccess == "public" ? "public" : (getAccess.Length > 0 ? getAccess : setAccess),
                isStatic, sig,
                getAccess.Length > 0, setAccess.Length > 0, null,
                docs.HasPrefix($"P:{docType}.{name}")));
        }
    }

    private static void ReadMethods(MetadataReader md, TypeDefinition td, SignatureFormatter fmt, XmlDocs docs, string docType, List<MemberInfo> members)
    {
        foreach (MethodDefinitionHandle mh in td.GetMethods())
        {
            MethodDefinition m = md.GetMethodDefinition(mh);
            string access = AccessOf(m.Attributes);
            if (access.Length == 0) continue;
            // Getters/setters/eventos já aparecem como propriedade/evento.
            bool special = (m.Attributes & MethodAttributes.SpecialName) != 0;
            string name = md.GetString(m.Name);
            if (special && name != ".ctor" && name != ".cctor" && !name.StartsWith("op_", StringComparison.Ordinal)) continue;
            if (name == ".cctor") continue;

            string sig;
            try
            {
                MethodSignature<string> s = m.DecodeSignature(fmt, null);
                string[] pnames = new string[s.ParameterTypes.Length];
                foreach (ParameterHandle parh in m.GetParameters())
                {
                    Parameter par = md.GetParameter(parh);
                    if (par.SequenceNumber > 0 && par.SequenceNumber <= pnames.Length)
                    {
                        string prefix = (par.Attributes & ParameterAttributes.Out) != 0 && s.ParameterTypes[par.SequenceNumber - 1].EndsWith('&') ? "out " : "";
                        pnames[par.SequenceNumber - 1] = prefix + md.GetString(par.Name);
                    }
                }
                var ps = s.ParameterTypes.Select((t, i) => pnames[i] is { } n ? $"{t} {n}" : t);
                sig = $"{s.ReturnType} {name}({string.Join(", ", ps)})";
            }
            catch (Exception ex) { sig = "<erro: " + ex.Message + ">"; }

            string docName = name == ".ctor" ? "#ctor" : name;
            members.Add(new MemberInfo(
                name == ".ctor" ? "constructor" : "method", name, access,
                (m.Attributes & MethodAttributes.Static) != 0, sig, null, null, null,
                docs.Has($"M:{docType}.{docName}") || docs.HasPrefix($"M:{docType}.{docName}(")));
        }
    }

    private static void ReadEvents(MetadataReader md, TypeDefinition td, SignatureFormatter fmt, XmlDocs docs, string docType, List<MemberInfo> members)
    {
        foreach (EventDefinitionHandle eh in td.GetEvents())
        {
            EventDefinition e = md.GetEventDefinition(eh);
            EventAccessors acc = e.GetAccessors();
            if (acc.Adder.IsNil) continue;
            MethodAttributes a = md.GetMethodDefinition(acc.Adder).Attributes;
            string access = AccessOf(a);
            if (access.Length == 0) continue;
            string name = md.GetString(e.Name);
            members.Add(new MemberInfo(
                "event", name, access, (a & MethodAttributes.Static) != 0,
                Names.Of(md, e.Type, fmt) ?? "?", null, null, null,
                docs.Has($"E:{docType}.{name}")));
        }
    }

    private static string? ReadConstant(MetadataReader md, Constant c)
    {
        BlobReader r = md.GetBlobReader(c.Value);
        try
        {
            return c.TypeCode switch
            {
                ConstantTypeCode.Boolean => r.ReadBoolean().ToString(),
                ConstantTypeCode.Char => r.ReadChar().ToString(),
                ConstantTypeCode.SByte => r.ReadSByte().ToString(),
                ConstantTypeCode.Byte => r.ReadByte().ToString(),
                ConstantTypeCode.Int16 => r.ReadInt16().ToString(),
                ConstantTypeCode.UInt16 => r.ReadUInt16().ToString(),
                ConstantTypeCode.Int32 => r.ReadInt32().ToString(),
                ConstantTypeCode.UInt32 => r.ReadUInt32().ToString(),
                ConstantTypeCode.Int64 => r.ReadInt64().ToString(),
                ConstantTypeCode.UInt64 => r.ReadUInt64().ToString(),
                ConstantTypeCode.Single => r.ReadSingle().ToString(System.Globalization.CultureInfo.InvariantCulture),
                ConstantTypeCode.Double => r.ReadDouble().ToString(System.Globalization.CultureInfo.InvariantCulture),
                ConstantTypeCode.String => r.ReadUTF16(r.Length),
                ConstantTypeCode.NullReference => "null",
                _ => null,
            };
        }
        catch (Exception) { return null; }
    }
}

/// <summary>Nomes de membros presentes no XML de documentação (IntelliSense) ao lado da DLL.</summary>
internal sealed class XmlDocs
{
    private readonly HashSet<string> _names;
    private readonly List<string> _sorted;

    private XmlDocs(HashSet<string> names, bool loaded)
    {
        _names = names;
        _sorted = names.OrderBy(n => n, StringComparer.Ordinal).ToList();
        Loaded = loaded;
    }

    public bool Loaded { get; }

    public static XmlDocs Load(string path)
    {
        if (!File.Exists(path)) return new XmlDocs(new HashSet<string>(), false);
        try
        {
            var names = XDocument.Load(path).Descendants("member")
                .Select(m => (string?)m.Attribute("name"))
                .Where(n => !string.IsNullOrEmpty(n))
                .Select(n => n!)
                .ToHashSet(StringComparer.Ordinal);
            return new XmlDocs(names, true);
        }
        catch (Exception) { return new XmlDocs(new HashSet<string>(), false); }
    }

    public bool Has(string name) => _names.Contains(name);

    public bool HasPrefix(string prefix)
    {
        int i = _sorted.BinarySearch(prefix, StringComparer.Ordinal);
        if (i >= 0) return true;
        i = ~i;
        return i < _sorted.Count && _sorted[i].StartsWith(prefix, StringComparison.Ordinal);
    }
}

internal static class Reports
{
    public static void WriteMembersCsv(string path, List<AssemblyInfo> assemblies)
    {
        var sb = new StringBuilder();
        sb.AppendLine("assembly;type;type_kind;base_type;member_kind;member;access;static;signature;can_read;can_write;constant;documented");
        foreach (AssemblyInfo a in assemblies)
            foreach (TypeInfo t in a.Types)
            {
                if (t.Members.Count == 0)
                    sb.AppendLine(string.Join(";", Csv(a.File), Csv(t.FullName), Csv(t.Kind), Csv(t.BaseType), "", "", "", "", "", "", "", "", t.Documented ? "1" : "0"));
                foreach (MemberInfo m in t.Members)
                    sb.AppendLine(string.Join(";",
                        Csv(a.File), Csv(t.FullName), Csv(t.Kind), Csv(t.BaseType), Csv(m.Kind), Csv(m.Name), Csv(m.Access),
                        m.IsStatic ? "1" : "0", Csv(m.Signature),
                        m.CanRead is null ? "" : m.CanRead.Value ? "1" : "0",
                        m.CanWrite is null ? "" : m.CanWrite.Value ? "1" : "0",
                        Csv(m.ConstantValue), m.Documented ? "1" : "0"));
            }
        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));
    }

    private static string Csv(string? s) => s is null ? "" : "\"" + s.Replace("\"", "\"\"") + "\"";

    /// <summary>Cadeia de herança e contagem de membros (próprios e herdados) das classes centrais.</summary>
    public static string KeyClasses(List<AssemblyInfo> assemblies, string[] keyTypes)
    {
        var byName = new Dictionary<string, (AssemblyInfo asm, TypeInfo type)>(StringComparer.Ordinal);
        foreach (AssemblyInfo a in assemblies)
            foreach (TypeInfo t in a.Types)
                byName.TryAdd(t.FullName, (a, t));

        var sb = new StringBuilder();
        foreach (string requested in keyTypes)
        {
            string? key = byName.ContainsKey(requested) ? requested
                : byName.Keys.FirstOrDefault(k => k.EndsWith("." + requested.Split('.').Last(), StringComparison.Ordinal));
            sb.AppendLine("=== " + requested);
            if (key is null) { sb.AppendLine("  não encontrado entre os tipos públicos"); sb.AppendLine(); continue; }
            if (key != requested) sb.AppendLine("  encontrado como: " + key);

            var chain = new List<string>();
            string? cur = key;
            while (cur is not null && byName.TryGetValue(cur, out var entry))
            {
                chain.Add(cur);
                cur = entry.type.BaseType;
            }
            if (cur is not null) chain.Add(cur + " (externo)");
            sb.AppendLine("  herança: " + string.Join(" -> ", chain));

            var t = byName[key].type;
            sb.AppendLine($"  assembly: {byName[key].asm.File}  tipo: {t.Kind}  documentado: {(t.Documented ? "sim" : "não")}");
            if (t.Interfaces.Count > 0) sb.AppendLine("  interfaces: " + string.Join(", ", t.Interfaces));

            int inherited = 0;
            foreach (string baseName in chain.Skip(1))
                if (byName.TryGetValue(baseName, out var b)) inherited += b.type.Members.Count(m => m.Kind is "property" or "method");
            sb.AppendLine($"  membros próprios: {t.Members.Count(m => m.Kind == "property")} propriedades, {t.Members.Count(m => m.Kind == "method")} métodos; herdados (prop+métodos): {inherited}");

            foreach (MemberInfo m in t.Members.Where(m => m.Kind is "property" or "method" or "event").OrderBy(m => m.Kind).ThenBy(m => m.Name, StringComparer.Ordinal))
            {
                string rw = m.Kind == "property" ? $" {{{(m.CanRead == true ? " get;" : "")}{(m.CanWrite == true ? " set;" : "")} }}" : "";
                sb.AppendLine($"    [{m.Kind[0]}] {(m.IsStatic ? "static " : "")}{m.Signature}{(m.Kind == "property" ? " " + m.Name + rw : "")}{(m.Documented ? "  (doc)" : "")}");
            }
            sb.AppendLine();
        }
        return sb.ToString();
    }
}
