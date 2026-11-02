#!/usr/bin/env -S dotnet run
// Inventory of public setters and public mutable fields of the framework (rule P6), on Roslyn.
// Run from anywhere, the artifact is standalone:
//   dotnet run docs/tasks/current/public_setters/count_public_members.cs -- /projects/framework [--md out.md] [--json out.json]
// Without --md prints the totals only. Needs: the Unity editor of client/project, client/project/Library and a built
// backend (.build/bin/Tests/Debug/net10.0), used only as references to resolve symbols; nothing is written to the repo
// unless --md / --json points there.
#:package Microsoft.CodeAnalysis.CSharp@5.3.0
#:property ManagePackageVersionsCentrally=false
#:property PublishAot=false
#:property Nullable=disable
#:property NoWarn=RS1035

using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

var root = Path.GetFullPath(args[0]);
string mdOut = null, jsonOut = null;
for (var i = 1; i < args.Length; i++)
{
    if (args[i] == "--md") mdOut = args[++i];
    else if (args[i] == "--json") jsonOut = args[++i];
    else throw new Exception($"Unknown argument {args[i]}");
}

new Inventory(root).Run(mdOut, jsonOut);

sealed class Member
{
    public string File, Type, TypeKey, Name, Kind, Effective, Group, Sub, Serializers = "";
    public int Line;
    public List<string> Tags = new();
    public int WritesProd, WritesTests, WritesSelf, Unresolved;
    public HashSet<string> WriteSamples = new();
    public string Key => $"{File}:{Line}:{Name}";
}

sealed class TypeInfoRow
{
    public HashSet<string> Attrs = new();
    public HashSet<string> Serializers = new();
    public HashSet<string> Bases = new();
    public string OptionsBinding;
}

sealed class Inventory
{
    static readonly string[] Scope = { "common", "shared", "client/core", "client/flow", "client/modules", "client/platforms", "backend" };

    static readonly string[][] ClientSymbols =
    {
        new[] { "UNITY_5_3_OR_NEWER", "UNITY_EDITOR", "DEBUG", "UNITY_STANDALONE", "UNITY_WEBGL", "ENABLE_PROFILER", "PLATFORM_TELEGRAM" },
        new[] { "UNITY_5_3_OR_NEWER", "UNITY_WEBGL", "PLATFORM_TELEGRAM" },
        new[] { "UNITY_5_3_OR_NEWER", "UNITY_STANDALONE", "PLATFORM_STEAM" },
    };

    static readonly string[][] BackendSymbols = { new[] { "DEBUG" }, Array.Empty<string>() };

    static readonly string[] BlazorAttrs = { "Parameter", "CascadingParameter", "SupplyParameterFromQuery", "SupplyParameterFromForm", "Inject" };

    readonly string _root;
    readonly Dictionary<string, Member> _members = new();
    readonly Dictionary<string, int> _notCounted = new();
    readonly HashSet<string> _notCountedKeys = new();
    readonly Dictionary<string, TypeInfoRow> _types = new();
    readonly HashSet<string> _writeSites = new();
    readonly List<string> _genericJsonSites = new();
    readonly List<string> _generated = new();
    readonly Dictionary<string, string> _editorAsmdefCache = new();
    readonly HashSet<string> _razorFileSet = new();
    readonly HashSet<string> _interfaceSetters = new();
    int _resolvedWrites, _unresolvedWrites;

    public Inventory(string root) => _root = root;

    public void Run(string mdOut, string jsonOut)
    {
        var files = CollectFiles();
        var client = files.Where(f => IsClientGroup(f)).ToList();
        var backend = files.Where(f => IsBackendGroup(f)).ToList();
        var tools = files.Where(f => !IsClientGroup(f) && !IsBackendGroup(f) || IsCommonOrShared(f)).ToList();

        var clientRefs = ClientReferences();
        var backendRefs = BackendReferences();
        var toolRefs = RuntimeReferences().Concat(new[] { MetadataReference.CreateFromFile(typeof(CSharpCompilation).Assembly.Location), MetadataReference.CreateFromFile(typeof(Compilation).Assembly.Location) }).ToList();

        var compilations = new List<CSharpCompilation>();
        foreach (var symbols in ClientSymbols) compilations.Add(Compile("client", client, symbols, clientRefs));
        foreach (var symbols in BackendSymbols) compilations.Add(Compile("backend", backend, symbols, backendRefs));
        compilations.Add(Compile("tools", tools, BackendSymbols[0], toolRefs));

        foreach (var compilation in compilations) CollectMembers(compilation);
        foreach (var compilation in compilations) AnalyzeSerializers(compilation);
        foreach (var compilation in compilations) CollectWrites(compilation);
        ScanRazorMarkup(files);
        Classify();

        var byGroup = _members.Values.GroupBy(m => m.Group).ToDictionary(g => g.Key, g => g.Count());
        Console.WriteLine($"scope files: {files.Count} (razor with @code {_razorFileSet.Count}); generated excluded: {_generated.Count}");
        Console.WriteLine($"writes resolved: {_resolvedWrites}, by name only: {_unresolvedWrites}");
        Console.WriteLine($"public members total: {_members.Count}");
        foreach (var kind in new[] { "setter", "field" }) Console.WriteLine($"  {kind}: {_members.Values.Count(m => Base(m.Kind) == kind)}");
        foreach (var group in "ABCDEFG".Select(c => c.ToString()))
        {
            var list = _members.Values.Where(m => m.Group == group).ToList();
            Console.WriteLine($"  {group}: {list.Count} (setters {list.Count(m => Base(m.Kind) == "setter")}, fields {list.Count(m => Base(m.Kind) == "field")}, types {list.Select(m => m.Type).Distinct().Count()})");
        }
        foreach (var reason in new[] { "internal field", "internal setter", "public member of a private nested type", "set in an interface" })
            Console.WriteLine($"not counted, {reason}: {_notCounted.GetValueOrDefault(reason)}");
        var internalType = _members.Values.Count(m => m.Effective != "public");
        Console.WriteLine($"counted, public keyword inside a non-public type: {internalType}");

        if (jsonOut != null) File.WriteAllText(jsonOut, JsonSerializer.Serialize(_members.Values.OrderBy(m => m.Key).Select(m => new
        {
            m.File, m.Line, m.Type, m.Name, m.Kind, m.Effective, m.Group, m.Sub, m.Serializers, Tags = string.Join(",", m.Tags),
            m.WritesProd, m.WritesTests, m.WritesSelf, m.Unresolved, Samples = m.WriteSamples.OrderBy(s => s).Take(5).ToList(),
        }), new JsonSerializerOptions { WriteIndented = true }));
        if (mdOut != null) File.WriteAllText(mdOut, Markdown());
        if (jsonOut != null) File.WriteAllLines(jsonOut + ".excluded.txt", _notCountedKeys.OrderBy(k => k, StringComparer.Ordinal));
    }

    static string Base(string kind) => kind.StartsWith("field") ? "field" : "setter";

    // --- files -------------------------------------------------------------------------------------------------

    List<string> CollectFiles()
    {
        var result = new List<string>();
        foreach (var scope in Scope)
        foreach (var path in Directory.EnumerateFiles(Path.Combine(_root, scope), "*.*", SearchOption.AllDirectories))
        {
            if (!path.EndsWith(".cs") && !path.EndsWith(".razor")) continue;
            var rel = Path.GetRelativePath(_root, path).Replace('\\', '/');
            var segments = rel.Split('/');
            if (segments.Contains("bin") || segments.Contains("obj")) continue;
            if (rel.EndsWith(".g.cs") || rel.EndsWith(".Designer.cs")) { _generated.Add(rel); continue; }
            if (rel.EndsWith(".cs"))
            {
                var head = File.ReadLines(path).Take(5);
                if (head.Any(l => l.Contains("<auto-generated"))) { _generated.Add(rel); continue; }
            }
            result.Add(rel);
        }
        result.Sort(StringComparer.Ordinal);
        return result;
    }

    static bool IsCommonOrShared(string rel) => (rel.StartsWith("common/") && !rel.StartsWith("common/Tests~")) || rel.StartsWith("shared/");
    static bool IsTool(string rel) => rel.Contains("/Source~/") || rel.StartsWith("common/Tests~");
    static bool IsClientGroup(string rel) => !IsTool(rel) && (rel.StartsWith("client/") || IsCommonOrShared(rel));
    static bool IsBackendGroup(string rel) => !IsTool(rel) && (rel.StartsWith("backend/") || IsCommonOrShared(rel));
    static bool IsTests(string rel) => rel.StartsWith("backend/Tests/") || rel.Contains("/Tests/") || rel.Contains("Tests~/") || rel.Contains("/Source~/");

    bool IsEditor(string rel)
    {
        if (!rel.StartsWith("client/")) return false;
        var dir = Path.GetDirectoryName(rel);
        while (!string.IsNullOrEmpty(dir))
        {
            if (_editorAsmdefCache.TryGetValue(dir, out var cached)) return cached == "1";
            var asmdef = Directory.EnumerateFiles(Path.Combine(_root, dir), "*.asmdef").FirstOrDefault();
            if (asmdef != null)
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(asmdef));
                var isEditor = doc.RootElement.TryGetProperty("includePlatforms", out var platforms)
                               && platforms.EnumerateArray().Any(p => p.GetString() == "Editor");
                _editorAsmdefCache[dir] = isEditor ? "1" : "0";
                return isEditor;
            }
            dir = Path.GetDirectoryName(dir);
        }
        return rel.Split('/').Contains("Editor");
    }

    // --- compilations ------------------------------------------------------------------------------------------

    CSharpCompilation Compile(string name, List<string> files, string[] symbols, List<MetadataReference> references)
    {
        var options = new CSharpParseOptions(LanguageVersion.Preview, preprocessorSymbols: symbols);
        var trees = new List<SyntaxTree>();
        foreach (var rel in files)
        {
            var full = Path.Combine(_root, rel);
            var text = rel.EndsWith(".razor") ? RazorToCSharp(full, rel) : File.ReadAllText(full);
            if (text == null) continue;
            trees.Add(CSharpSyntaxTree.ParseText(text, options, rel));
        }
        var compilationOptions = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true, metadataImportOptions: MetadataImportOptions.All);
        return CSharpCompilation.Create(name, trees, references, compilationOptions);
    }

    // The @code block of a component as a class on the same lines, so that file:line stay true.
    string RazorToCSharp(string full, string rel)
    {
        var text = File.ReadAllText(full);
        var match = Regex.Match(text, @"^\s*@code\s*\{", RegexOptions.Multiline);
        if (!match.Success) return null;
        _razorFileSet.Add(rel);
        var open = match.Index + match.Length - 1;
        var depth = 0;
        var close = -1;
        for (var i = open; i < text.Length; i++)
        {
            if (text[i] == '{') depth++;
            else if (text[i] == '}' && --depth == 0) { close = i; break; }
        }
        if (close < 0) throw new Exception($"Unbalanced @code in {rel}");
        var usings = new StringBuilder("using Microsoft.AspNetCore.Components; ");
        var dir = Path.GetDirectoryName(full);
        while (dir.Length >= _root.Length)
        {
            var imports = Path.Combine(dir, "_Imports.razor");
            if (File.Exists(imports)) foreach (Match u in Regex.Matches(File.ReadAllText(imports), @"^@using\s+([\w.]+)", RegexOptions.Multiline)) usings.Append($"using {u.Groups[1].Value}; ");
            dir = Path.GetDirectoryName(dir);
        }
        foreach (Match u in Regex.Matches(text, @"^@using\s+([\w.]+)", RegexOptions.Multiline)) usings.Append($"using {u.Groups[1].Value}; ");
        var injects = new StringBuilder();
        foreach (Match inject in Regex.Matches(text, @"^@inject\s+(.+?)\s+(\w+)\s*$", RegexOptions.Multiline)) injects.Append($"private {inject.Groups[1].Value} {inject.Groups[2].Value}; ");
        var className = "Razor__" + Regex.Replace(rel, @"\W", "_");
        var lineOfBrace = text.Take(open).Count(c => c == '\n');
        return usings + "public class " + className + " " + new string('\n', lineOfBrace) + "{ " + injects + text.Substring(open + 1, close - open - 1) + "}";
    }

    List<MetadataReference> RuntimeReferences()
    {
        var dir = Path.GetDirectoryName(typeof(object).Assembly.Location);
        return Directory.EnumerateFiles(dir, "*.dll").Where(IsManaged).Select(p => (MetadataReference)MetadataReference.CreateFromFile(p)).ToList();
    }

    static bool IsManaged(string path)
    {
        try { System.Reflection.AssemblyName.GetAssemblyName(path); return true; }
        catch (BadImageFormatException) { return false; }
    }

    List<MetadataReference> ClientReferences()
    {
        var version = File.ReadLines(Path.Combine(_root, "client/project/ProjectSettings/ProjectVersion.txt")).First(l => l.StartsWith("m_EditorVersion:")).Split(':')[1].Trim();
        var managed = $"/tools/unity/{version}/Editor/Data/Managed";
        if (!Directory.Exists(managed)) throw new Exception($"No Unity editor at {managed}");
        var library = Path.Combine(_root, "client/project/Library");
        if (!Directory.Exists(library)) throw new Exception($"No {library}: open client/project once");

        var paths = new Dictionary<string, string>();
        void Add(string path)
        {
            var name = Path.GetFileName(path);
            if (name.StartsWith("Internal") || name.StartsWith("Framework.")) return;
            if (!paths.ContainsKey(name) && IsManaged(path)) paths[name] = path;
        }
        foreach (var dll in Directory.EnumerateFiles(Path.Combine(managed, "UnityEngine"), "*.dll")) Add(dll);
        foreach (var dll in Directory.EnumerateFiles(managed, "*.dll").Where(p => Path.GetFileName(p).StartsWith("UnityEditor") || Path.GetFileName(p) == "UnityEngine.dll")) Add(dll);
        foreach (var dll in Directory.EnumerateFiles(Path.Combine(library, "ScriptAssemblies"), "*.dll")) Add(dll);
        foreach (var dll in Directory.EnumerateFiles(Path.Combine(library, "PackageCache"), "*.dll", SearchOption.AllDirectories).Where(p => !p.Contains("/Source~/"))) Add(dll);
        foreach (var dll in Directory.EnumerateFiles(Path.Combine(_root, "client/packages"), "*.dll", SearchOption.AllDirectories).Where(p => !p.Contains("/Source~/") && !p.Contains("/Analyzers/"))) Add(dll);
        // The module UnityEngine.dll duplicates every type of the modules, the facade would make them ambiguous.
        paths.Remove("UnityEngine.dll");
        return RuntimeReferences().Concat(paths.Values.Select(p => (MetadataReference)MetadataReference.CreateFromFile(p))).ToList();
    }

    List<MetadataReference> BackendReferences()
    {
        var bin = Path.Combine(_root, ".build/bin/Tests/Debug/net10.0");
        if (!Directory.Exists(bin)) throw new Exception($"No {bin}: dotnet build framework.slnx first");
        var dotnet = Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(typeof(object).Assembly.Location))));
        var aspRoot = Path.Combine(dotnet, "shared/Microsoft.AspNetCore.App");
        var asp = Directory.EnumerateDirectories(aspRoot).OrderBy(d => d).Last();
        var own = new[] { "Cluster.dll", "Common.dll", "Console.dll", "Hosting.dll", "Infrastructure.dll", "Session.dll", "Tests.dll", "Framework.", "Internal" };
        var names = new HashSet<string>();
        var result = RuntimeReferences();
        foreach (var dll in Directory.EnumerateFiles(asp, "*.dll").Concat(Directory.EnumerateFiles(bin, "*.dll")))
        {
            var name = Path.GetFileName(dll);
            if (own.Any(o => name.StartsWith(o))) continue;
            if (names.Add(name) && IsManaged(dll)) result.Add(MetadataReference.CreateFromFile(dll));
        }
        return result;
    }

    // --- members ------------------------------------------------------------------------------------------------

    void CollectMembers(CSharpCompilation compilation)
    {
        foreach (var tree in compilation.SyntaxTrees)
        {
            var rel = tree.FilePath;
            var model = compilation.GetSemanticModel(tree);
            foreach (var node in tree.GetRoot().DescendantNodes())
            {
                switch (node)
                {
                    case FieldDeclarationSyntax field:
                        VisitField(rel, field, model);
                        break;
                    case PropertyDeclarationSyntax property:
                        VisitProperty(rel, property, property.Identifier.Text, property.AccessorList, property.Modifiers, property.AttributeLists, model);
                        break;
                    case IndexerDeclarationSyntax indexer:
                        VisitProperty(rel, indexer, "this[]", indexer.AccessorList, indexer.Modifiers, indexer.AttributeLists, model);
                        break;
                    case RecordDeclarationSyntax record when record.ClassOrStructKeyword.IsKind(SyntaxKind.StructKeyword) && record.ParameterList != null && !record.Modifiers.Any(SyntaxKind.ReadOnlyKeyword):
                        foreach (var parameter in record.ParameterList.Parameters) AddMember(rel, parameter, parameter.Identifier.Text, "setter-record-struct-param", record, new List<string>(), model, parameter.AttributeLists);
                        break;
                }
            }
        }
    }

    void VisitField(string rel, FieldDeclarationSyntax field, SemanticModel model)
    {
        var modifiers = field.Modifiers;
        if (modifiers.Any(SyntaxKind.ConstKeyword) || modifiers.Any(SyntaxKind.ReadOnlyKeyword)) return;
        var type = (TypeDeclarationSyntax)field.Parent as TypeDeclarationSyntax;
        if (type == null) return;
        var isPublic = modifiers.Any(SyntaxKind.PublicKeyword);
        var isInternal = modifiers.Any(SyntaxKind.InternalKeyword) && !modifiers.Any(SyntaxKind.ProtectedKeyword);
        foreach (var variable in field.Declaration.Variables)
        {
            if (isPublic)
            {
                var tags = new List<string>();
                if (modifiers.Any(SyntaxKind.StaticKeyword)) tags.Add("static");
                AddMember(rel, variable, variable.Identifier.Text, modifiers.Any(SyntaxKind.StaticKeyword) ? "field-static" : "field", type, tags, model, field.AttributeLists);
            }
            else if (isInternal) NotCounted("internal field", $"{rel}:{LineOf(variable)}:{variable.Identifier.Text}");
        }
    }

    void VisitProperty(string rel, MemberDeclarationSyntax node, string name, AccessorListSyntax accessors, SyntaxTokenList modifiers, SyntaxList<AttributeListSyntax> attrs, SemanticModel model)
    {
        var line = LineOf(node) - 1;
        if (accessors == null) return;
        var setter = accessors.Accessors.FirstOrDefault(a => a.IsKind(SyntaxKind.SetAccessorDeclaration));
        if (setter == null) return;
        if (node.Parent is InterfaceDeclarationSyntax)
        {
            NotCounted("set in an interface", $"{rel}:{line}:{name}");
            _interfaceSetters.Add($"{rel}:{line + 1} {name}");
            return;
        }
        if (node.Parent is not TypeDeclarationSyntax type) return;
        var isPublicProperty = modifiers.Any(SyntaxKind.PublicKeyword);
        var setterPublic = isPublicProperty && setter.Modifiers.Count == 0;
        var setterInternal = setter.Modifiers.Any(SyntaxKind.InternalKeyword) && !setter.Modifiers.Any(SyntaxKind.ProtectedKeyword)
                             || setter.Modifiers.Count == 0 && modifiers.Any(SyntaxKind.InternalKeyword) && !modifiers.Any(SyntaxKind.ProtectedKeyword);
        if (setterPublic)
        {
            var tags = new List<string>();
            if (modifiers.Any(SyntaxKind.StaticKeyword)) tags.Add("static");
            if (modifiers.Any(SyntaxKind.OverrideKeyword)) tags.Add("override");
            if (modifiers.Any(SyntaxKind.AbstractKeyword)) tags.Add("abstract");
            if (modifiers.Any(SyntaxKind.VirtualKeyword)) tags.Add("virtual");
            if (setter.Body != null || setter.ExpressionBody != null) tags.Add("body");
            var kind = name == "this[]" ? "setter-indexer" : "setter";
            AddMember(rel, node, name, kind, type, tags, model, attrs);
        }
        else if (setterInternal) NotCounted("internal setter", $"{rel}:{line}:{name}");
    }

    void NotCounted(string reason, string key)
    {
        if (!_notCountedKeys.Add(reason + key)) return;
        _notCounted.TryGetValue(reason, out var count);
        _notCounted[reason] = count + 1;
    }

    void AddMember(string rel, SyntaxNode node, string name, string kind, TypeDeclarationSyntax type, List<string> tags, SemanticModel model, SyntaxList<AttributeListSyntax> attrs)
    {
        var line = LineOf(node);
        var effective = EffectiveAccess(type);
        if (effective == "private")
        {
            NotCounted("public member of a private nested type", $"{rel}:{line}:{name}");
            return;
        }
        var member = new Member { File = rel, Line = line, Name = name, Kind = kind, Effective = effective, Type = TypeName(type), TypeKey = TypeKey(type), Tags = tags };
        if (_members.ContainsKey(member.Key)) return;
        foreach (var attr in attrs.SelectMany(a => a.Attributes)) member.Tags.Add("[" + AttrName(attr) + "]");
        foreach (var list in attrs) if (list.Target?.Identifier.Text == "field") member.Tags.Add("field:" + string.Join("+", list.Attributes.Select(AttrName)));
        _members[member.Key] = member;

        var symbol = model.GetDeclaredSymbol(type) as INamedTypeSymbol;
        var info = TypeRow(member.TypeKey);
        TagContract(member, node, model);
        if (symbol != null) FillType(info, symbol);
        else foreach (var list in type.AttributeLists) foreach (var attr in list.Attributes) info.Attrs.Add(AttrName(attr));
    }

    // A setter demanded by an interface or a base class. Foreign (not in this repository) cannot be changed here.
    static void TagContract(Member member, SyntaxNode node, SemanticModel model)
    {
        if (model.GetDeclaredSymbol(node) is not IPropertySymbol property) return;
        for (var over = property.OverriddenProperty; over != null; over = over.OverriddenProperty)
            if (over.SetMethod != null) member.Tags.Add((over.DeclaringSyntaxReferences.Any() ? "base:" : "foreign-base:") + over.ContainingType.Name);
        foreach (var contract in property.ContainingType.AllInterfaces)
        foreach (var item in contract.GetMembers().OfType<IPropertySymbol>())
        {
            if (item.SetMethod == null) continue;
            if (!SymbolEqualityComparer.Default.Equals(property.ContainingType.FindImplementationForInterfaceMember(item), property)) continue;
            member.Tags.Add((contract.DeclaringSyntaxReferences.Any() ? "iface:" : "foreign-iface:") + contract.Name);
        }
    }

    // The line of the name, not of the attributes above it.
    static int LineOf(SyntaxNode node)
    {
        var token = node switch
        {
            PropertyDeclarationSyntax property => property.Identifier,
            IndexerDeclarationSyntax indexer => indexer.ThisKeyword,
            VariableDeclaratorSyntax variable => variable.Identifier,
            ParameterSyntax parameter => parameter.Identifier,
            _ => node.GetFirstToken(),
        };
        return token.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
    }

    static string AttrName(AttributeSyntax attr)
    {
        var name = attr.Name switch
        {
            QualifiedNameSyntax q => q.Right.Identifier.Text,
            AliasQualifiedNameSyntax a => a.Name.Identifier.Text,
            SimpleNameSyntax s => s.Identifier.Text,
            _ => attr.Name.ToString(),
        };
        return name.EndsWith("Attribute") ? name.Substring(0, name.Length - 9) : name;
    }

    TypeInfoRow TypeRow(string name)
    {
        if (!_types.TryGetValue(name, out var row)) _types[name] = row = new TypeInfoRow();
        return row;
    }

    void FillType(TypeInfoRow row, INamedTypeSymbol symbol)
    {
        foreach (var syntax in symbol.DeclaringSyntaxReferences.Select(r => r.GetSyntax()).OfType<TypeDeclarationSyntax>())
        foreach (var attr in syntax.AttributeLists.SelectMany(l => l.Attributes))
            row.Attrs.Add(AttrName(attr));
        for (var current = symbol; current != null; current = current.BaseType) row.Bases.Add(current.Name);
        foreach (var i in symbol.AllInterfaces) row.Bases.Add(i.Name);
    }

    // Display name, without the namespace.
    static string TypeName(BaseTypeDeclarationSyntax type)
    {
        var parts = new List<string> { type.Identifier.Text };
        for (var parent = type.Parent; parent is TypeDeclarationSyntax outer; parent = outer.Parent) parts.Insert(0, outer.Identifier.Text);
        var name = string.Join(".", parts);
        return name.StartsWith("Razor__") ? Path.GetFileNameWithoutExtension(type.SyntaxTree.FilePath) + ".razor" : name;
    }

    // Key of a type across its partial parts: the same simple name may live in several namespaces.
    static string TypeKey(BaseTypeDeclarationSyntax type)
    {
        var space = type.Ancestors().OfType<BaseNamespaceDeclarationSyntax>().Select(n => n.Name.ToString()).Reverse();
        return string.Join(".", space) + "/" + TypeName(type);
    }

    // The widest place a public member of the type is visible from. Any private container hides it.
    static string EffectiveAccess(TypeDeclarationSyntax type)
    {
        var result = "public";
        for (SyntaxNode current = type; current is TypeDeclarationSyntax t; current = t.Parent)
        {
            var nested = t.Parent is TypeDeclarationSyntax;
            var m = t.Modifiers;
            if (m.Any(SyntaxKind.PrivateKeyword) && !m.Any(SyntaxKind.ProtectedKeyword)) return "private";
            if (m.Any(SyntaxKind.FileKeyword)) return "private";
            if (m.Any(SyntaxKind.PublicKeyword)) continue;
            if (m.Any(SyntaxKind.ProtectedKeyword)) { if (result == "public") result = "protected"; continue; }
            if (m.Any(SyntaxKind.InternalKeyword)) { result = "internal"; continue; }
            if (nested) return "private";
            result = "internal";
        }
        return result;
    }

    // --- serializers --------------------------------------------------------------------------------------------

    void AnalyzeSerializers(CSharpCompilation compilation)
    {
        var roots = new List<(string serializer, ITypeSymbol type)>();
        foreach (var tree in compilation.SyntaxTrees)
        {
            var model = compilation.GetSemanticModel(tree);
            foreach (var node in tree.GetRoot().DescendantNodes())
            {
                if (node is InvocationExpressionSyntax invocation) VisitJsonCall(invocation, model, roots);
                else if (node is GenericNameSyntax generic) VisitOptionsCall(generic, model);
                else if (node is TypeDeclarationSyntax type && model.GetDeclaredSymbol(type) is INamedTypeSymbol symbol)
                {
                    if (IsUnityObject(symbol)) roots.Add(("Unity", symbol));
                    if (IsStoredState(symbol, type)) roots.Add(("Stored", symbol));
                }
            }
        }
        var visited = new HashSet<string>();
        foreach (var (serializer, type) in roots) Walk(serializer, type, visited);
    }

    // Written to PostgreSQL by StateSerializer (Newtonsoft): [GrainState] or a state value, and what they hold.
    static bool IsStoredState(INamedTypeSymbol symbol, TypeDeclarationSyntax type)
    {
        if (symbol.AllInterfaces.Any(i => i.Name is "IStateValue")) return true;
        return type.AttributeLists.SelectMany(l => l.Attributes).Any(a => AttrName(a) == "GrainState");
    }

    static bool IsUnityObject(INamedTypeSymbol symbol)
    {
        for (var current = symbol; current != null; current = current.BaseType)
            if (current.Name == "Object" && current.ContainingNamespace?.ToDisplayString() == "UnityEngine") return true;
        return false;
    }

    void VisitOptionsCall(GenericNameSyntax generic, SemanticModel model)
    {
        var name = generic.Identifier.Text;
        string binding = name switch
        {
            "IOptions" or "IOptionsMonitor" or "IOptionsSnapshot" => "IOptions",
            "Configure" or "AddOptions" or "PostConfigure" or "AddEnvironmentOptions" => name == "AddEnvironmentOptions" ? "binder" : "Configure",
            "Get" when generic.Parent is MemberAccessExpressionSyntax access && model.GetTypeInfo(access.Expression).Type is { } t && t.AllInterfaces.Concat(new[] { t }).Any(i => i.Name == "IConfiguration") => "binder",
            _ => null,
        };
        if (binding == null || generic.TypeArgumentList.Arguments.Count != 1) return;
        if (model.GetTypeInfo(generic.TypeArgumentList.Arguments[0]).Type is not INamedTypeSymbol type) return;
        foreach (var syntax in type.DeclaringSyntaxReferences.Select(r => r.GetSyntax()).OfType<TypeDeclarationSyntax>())
        {
            var row = TypeRow(TypeKey(syntax));
            row.OptionsBinding = row.OptionsBinding == null || binding == "binder" ? binding : row.OptionsBinding;
        }
    }

    void VisitJsonCall(InvocationExpressionSyntax invocation, SemanticModel model, List<(string, ITypeSymbol)> roots)
    {
        var symbolInfo = model.GetSymbolInfo(invocation);
        var method = symbolInfo.Symbol as IMethodSymbol ?? symbolInfo.CandidateSymbols.OfType<IMethodSymbol>().FirstOrDefault();
        if (method == null) return;
        if (method.Name == "Bind" && method.ContainingType?.Name == "ConfigurationBinder" && invocation.ArgumentList.Arguments.Count > 0
            && model.GetTypeInfo(invocation.ArgumentList.Arguments[^1].Expression).Type is INamedTypeSymbol bound)
            foreach (var syntax in bound.DeclaringSyntaxReferences.Select(r => r.GetSyntax()).OfType<TypeDeclarationSyntax>())
                TypeRow(TypeKey(syntax)).OptionsBinding = "binder";
        var owner = method.ContainingType?.Name;
        var space = method.ContainingType?.ContainingNamespace?.ToDisplayString() ?? "";
        string serializer = owner switch
        {
            "JsonUtility" => "JsonUtility",
            "EditorJsonUtility" => "EditorJsonUtility",
            "JsonConvert" => "Newtonsoft",
            "JToken" or "JObject" or "JArray" when method.Name is "ToObject" or "FromObject" => "Newtonsoft",
            "JsonSerializer" when space.StartsWith("System.Text.Json") => "STJ",
            "JsonSerializer" when space.StartsWith("Newtonsoft") => "Newtonsoft",
            "HttpClientJsonExtensions" or "HttpContentJsonExtensions" => "STJ",
            "MessagePackSerializer" => "MessagePack",
            "ISaves" when method.TypeArguments.Length > 0 => "JsonUtility",
            _ => null,
        };
        if (serializer == null) return;

        var found = false;
        foreach (var argument in method.TypeArguments)
        {
            if (argument is ITypeParameterSymbol parameter)
            {
                GenericSite(serializer, invocation);
                foreach (var constraint in parameter.ConstraintTypes) roots.Add((serializer, constraint));
                found = true;
                continue;
            }
            roots.Add((serializer, argument));
            found = true;
        }
        foreach (var argument in invocation.ArgumentList.Arguments)
        {
            ITypeSymbol type = null;
            if (argument.Expression is TypeOfExpressionSyntax typeOf) type = model.GetTypeInfo(typeOf.Type).Type;
            else
            {
                var t = model.GetTypeInfo(argument.Expression).Type;
                if (t != null && t.SpecialType == SpecialType.None && t.TypeKind is TypeKind.Class or TypeKind.Struct or TypeKind.Array or TypeKind.TypeParameter && t.Name is not ("Type" or "JsonSerializerOptions" or "JsonSerializerSettings" or "HttpClient" or "HttpContent" or "CancellationToken")) type = t;
            }
            if (type == null) continue;
            if (type is ITypeParameterSymbol) { GenericSite(serializer, invocation); found = true; continue; }
            if (type.SpecialType == SpecialType.System_Object) { GenericSite(serializer, invocation); found = true; continue; }
            roots.Add((serializer, type));
            found = true;
        }
        if (!found) GenericSite(serializer, invocation);
    }

    void GenericSite(string serializer, SyntaxNode node)
    {
        var span = node.GetLocation().GetLineSpan();
        var site = $"{serializer} {span.Path}:{span.StartLinePosition.Line + 1}";
        if (!_genericJsonSites.Contains(site)) _genericJsonSites.Add(site);
    }

    void Walk(string serializer, ITypeSymbol type, HashSet<string> visited)
    {
        if (type == null) return;
        if (type is IArrayTypeSymbol array) { Walk(serializer, array.ElementType, visited); return; }
        if (type is not INamedTypeSymbol named) return;
        foreach (var argument in named.TypeArguments) Walk(serializer, argument, visited);
        if (!named.DeclaringSyntaxReferences.Any()) return;
        if (!visited.Add(serializer + "|" + named.OriginalDefinition.ToDisplayString())) return;
        foreach (var syntax in named.DeclaringSyntaxReferences.Select(r => r.GetSyntax()).OfType<TypeDeclarationSyntax>()) TypeRow(TypeKey(syntax)).Serializers.Add(serializer);
        var unityLike = serializer is "Unity" or "JsonUtility" or "EditorJsonUtility";
        for (var current = named; current != null && current.DeclaringSyntaxReferences.Any(); current = current.BaseType)
        {
            foreach (var syntax in current.DeclaringSyntaxReferences.Select(r => r.GetSyntax()).OfType<TypeDeclarationSyntax>()) TypeRow(TypeKey(syntax)).Serializers.Add(serializer);
            foreach (var member in current.GetMembers())
            {
                if (member.IsStatic) continue;
                if (member is IFieldSymbol field)
                {
                    var serialized = field.DeclaredAccessibility == Accessibility.Public || field.GetAttributes().Any(a => a.AttributeClass?.Name == "SerializeField");
                    if (field.AssociatedSymbol is IPropertySymbol) serialized = field.GetAttributes().Any(a => a.AttributeClass?.Name == "SerializeField");
                    if (field.GetAttributes().Any(a => a.AttributeClass?.Name is "NonSerializedAttribute" or "JsonIgnoreAttribute")) serialized = false;
                    if (serialized) Walk(serializer, field.Type, visited);
                }
                else if (!unityLike && member is IPropertySymbol property && property.DeclaredAccessibility == Accessibility.Public && !property.IsIndexer)
                    Walk(serializer, property.Type, visited);
            }
        }
    }

    // --- writes ---------------------------------------------------------------------------------------------------

    void CollectWrites(CSharpCompilation compilation)
    {
        var byName = _members.Values.GroupBy(m => m.Name).ToDictionary(g => g.Key, g => g.ToList());
        foreach (var tree in compilation.SyntaxTrees)
        {
            var model = compilation.GetSemanticModel(tree);
            foreach (var node in tree.GetRoot().DescendantNodes())
            {
                ExpressionSyntax target = node switch
                {
                    AssignmentExpressionSyntax assignment => assignment.Left,
                    PrefixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.PreIncrementExpression or (int)SyntaxKind.PreDecrementExpression } prefix => prefix.Operand,
                    PostfixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.PostIncrementExpression or (int)SyntaxKind.PostDecrementExpression } postfix => postfix.Operand,
                    ArgumentSyntax argument when !argument.RefKindKeyword.IsKind(SyntaxKind.None) && !argument.RefKindKeyword.IsKind(SyntaxKind.InKeyword) => argument.Expression,
                    _ => null,
                };
                if (target == null) continue;
                foreach (var single in Targets(target)) RecordWrite(single, model, tree.FilePath, byName);
            }
        }
    }

    static IEnumerable<ExpressionSyntax> Targets(ExpressionSyntax target)
    {
        if (target is TupleExpressionSyntax tuple)
        {
            foreach (var argument in tuple.Arguments)
            foreach (var inner in Targets(argument.Expression))
                yield return inner;
            yield break;
        }
        if (target is DeclarationExpressionSyntax) yield break;
        yield return target;
        // A write through a struct field changes the field too: a.Position.X = 1.
        var current = target;
        while (current is MemberAccessExpressionSyntax access)
        {
            current = access.Expression;
            if (current is MemberAccessExpressionSyntax) yield return current;
        }
    }

    void RecordWrite(ExpressionSyntax target, SemanticModel model, string path, Dictionary<string, List<Member>> byName)
    {
        var info = model.GetSymbolInfo(target);
        var symbol = info.Symbol ?? (info.CandidateSymbols.Length == 1 ? info.CandidateSymbols[0] : null);
        var span = target.GetLocation().GetLineSpan();
        var site = $"{path}:{span.StartLinePosition.Line + 1}:{span.StartLinePosition.Character}";
        if (symbol is IPropertySymbol or IFieldSymbol)
        {
            symbol = symbol.OriginalDefinition;
            if (target is MemberAccessExpressionSyntax throughStruct && !ReferenceEquals(target, target) && symbol is IPropertySymbol) return;
            foreach (var reference in symbol.DeclaringSyntaxReferences)
            {
                var declared = reference.GetSyntax();
                var line = LineOf(declared);
                var name = symbol is IPropertySymbol { IsIndexer: true } ? "this[]" : symbol.Name;
                if (!_members.TryGetValue($"{reference.SyntaxTree.FilePath}:{line}:{name}", out var member)) continue;
                if (!_writeSites.Add(site + "|" + member.Key)) return;
                _resolvedWrites++;
                var enclosing = model.GetEnclosingSymbol(target.SpanStart)?.ContainingType;
                var self = false;
                for (var current = enclosing; current != null; current = current.ContainingType)
                    if (SymbolEqualityComparer.Default.Equals(current.OriginalDefinition, symbol.ContainingType.OriginalDefinition)) self = true;
                if (self) member.WritesSelf++;
                else
                {
                    if (IsTests(path)) member.WritesTests++;
                    else member.WritesProd++;
                    member.WriteSamples.Add($"{path}:{span.StartLinePosition.Line + 1}");
                }
                return;
            }
            return;
        }
        if (symbol != null) return;
        var targetName = target switch
        {
            MemberAccessExpressionSyntax access => access.Name.Identifier.Text,
            IdentifierNameSyntax identifier when identifier.Parent is AssignmentExpressionSyntax { Parent: InitializerExpressionSyntax } => identifier.Identifier.Text,
            _ => null,
        };
        if (targetName == null || !byName.TryGetValue(targetName, out var candidates)) return;
        if (!_writeSites.Add(site + "|name")) return;
        _unresolvedWrites++;
        foreach (var member in candidates) member.Unresolved++;
    }

    void ScanRazorMarkup(List<string> files)
    {
        var byName = _members.Values.GroupBy(m => m.Name).ToDictionary(g => g.Key, g => g.ToList());
        foreach (var rel in files.Where(f => f.EndsWith(".razor")))
        {
            var text = File.ReadAllText(Path.Combine(_root, rel));
            var codeAt = Regex.Match(text, @"^\s*@code\s*\{", RegexOptions.Multiline);
            var markup = codeAt.Success ? text.Substring(0, codeAt.Index) : text;
            foreach (Match match in Regex.Matches(markup, @"@bind[-\w:]*=""@?[\w.]*?(\w+)""|\.(\w+)\s*=(?![=>])"))
            {
                var name = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
                if (!byName.TryGetValue(name, out var candidates)) continue;
                _unresolvedWrites++;
                foreach (var member in candidates) member.Unresolved++;
            }
        }
    }

    // --- groups ---------------------------------------------------------------------------------------------------

    void Classify()
    {
        foreach (var member in _members.Values)
        {
            var row = TypeRow(member.TypeKey);
            member.Serializers = string.Join("+", row.Serializers.OrderBy(s => s));
            (member.Group, member.Sub) = Group(member, row);
        }
    }

    (string, string) Group(Member member, TypeInfoRow row)
    {
        var rel = member.File;
        if (IsTests(rel)) return ("F", rel.Contains("/Source~/") ? "generator sources" : FolderOf(rel));
        if (row.Attrs.Contains("MemoryPackable") || row.Attrs.Contains("MemoryPackUnion")) return ("C", FolderOf(rel));

        if (member.Tags.Any(t => BlazorAttrs.Contains(t.Trim('[', ']')))) return ("G", "Blazor");
        if (row.Bases.Contains("Attribute")) return ("G", "attribute named argument");
        if (member.Tags.Any(t => t.StartsWith("foreign-"))) return ("G", "foreign interface or base");
        if (row.Attrs.Contains("StructLayout")) return ("G", "native interop struct");
        if (row.OptionsBinding != null) return ("G", "options: " + row.OptionsBinding);

        var isField = Base(member.Kind) == "field";
        var isStatic = member.Tags.Contains("static");
        var fieldSerialized = isField && !isStatic && !member.Tags.Contains("[NonSerialized]");
        var backingSerialized = member.Tags.Any(t => t.StartsWith("field:") && t.Contains("SerializeField"));
        var hasJson = row.Serializers.Overlaps(new[] { "Newtonsoft", "STJ", "MessagePack", "Stored" });
        var hasUnity = row.Serializers.Overlaps(new[] { "Unity", "JsonUtility", "EditorJsonUtility" });
        var isOrleans = row.Attrs.Contains("GenerateSerializer") || member.Tags.Contains("[Id]") || row.Attrs.Contains("GrainState");

        if (rel.StartsWith("backend/") || rel.StartsWith("shared/") && isOrleans)
        {
            if (isOrleans || hasJson || member.Tags.Contains("[JsonProperty]") || row.Bases.Contains("IStateValue")) return ("E", FolderOf(rel));
        }
        else if (fieldSerialized && (hasUnity || row.Attrs.Contains("Serializable")) || backingSerialized || hasJson && !isStatic)
            return ("D", IsEditor(rel) ? "editor" : "runtime");

        if (rel.StartsWith("backend/") && row.Attrs.Contains("Serializable")) return ("E", FolderOf(rel));
        if (IsEditor(rel)) return ("B", FolderOf(rel));
        return ("A", FolderOf(rel));
    }

    static string FolderOf(string rel)
    {
        var parts = rel.Split('/');
        if (parts[0] == "client" && parts[1] is "modules" or "platforms" && parts.Length > 3) return $"client/{parts[1]}/{parts[2]}";
        if (parts[0] == "client" && parts[1] == "core" && parts.Length > 3) return $"client/core/{parts[2]}";
        if (parts[0] == "client") return $"client/{parts[1]}";
        if (parts[0] == "backend" && parts.Length > 2) return $"backend/{parts[1]}";
        return parts[0];
    }

    // --- output -----------------------------------------------------------------------------------------------------

    string Markdown()
    {
        var sb = new StringBuilder();
        var titles = new Dictionary<string, string>
        {
            ["A"] = "A. Runtime, настоящие нарушения",
            ["B"] = "B. Editor, настоящие нарушения",
            ["C"] = "C. `[MemoryPackable]` (разрешено)",
            ["D"] = "D. Сериализуемые поля клиента (`JsonUtility`, Unity, `[Serializable]`)",
            ["E"] = "E. Orleans-состояния и JSON-модели backend",
            ["F"] = "F. Тесты и исходники генератора",
            ["G"] = "G. Контракты внешних механизмов",
        };
        foreach (var group in "ABCDEFG".Select(c => c.ToString()))
        {
            var list = _members.Values.Where(m => m.Group == group).ToList();
            sb.AppendLine($"### {titles[group]}: {list.Count}\n");
            foreach (var sub in list.GroupBy(m => m.Sub).OrderBy(g => g.Key, StringComparer.Ordinal))
            {
                sb.AppendLine($"#### {sub.Key}: {sub.Count()}\n");
                sb.AppendLine("| file:line | type | members | ext prod / tests / self / by name | serializer | notes |");
                sb.AppendLine("|---|---|---|---|---|---|");
                foreach (var type in sub.GroupBy(m => (m.File, m.TypeKey)).OrderBy(g => g.Key.File, StringComparer.Ordinal).ThenBy(g => g.Min(m => m.Line)))
                {
                    var first = type.OrderBy(m => m.Line).First();
                    var names = string.Join(", ", type.OrderBy(m => m.Line).Select(m => "`" + m.Name + (Base(m.Kind) == "field" ? "` f" : "`")));
                    var row = TypeRow(first.TypeKey);
                    var notes = new List<string>();
                    if (first.Effective != "public") notes.Add(first.Effective);
                    if (row.Attrs.Contains("Serializable")) notes.Add("[Serializable]");
                    foreach (var tag in type.SelectMany(m => m.Tags).Where(t => t is "static" or "override" or "abstract" or "virtual" or "body" or "[JsonProperty]" or "[Id]" || t.StartsWith("field:") || t.StartsWith("iface:") || t.StartsWith("base:") || t.StartsWith("foreign-")).Distinct().OrderBy(t => t)) notes.Add(tag);
                    if (row.OptionsBinding != null) notes.Add("options: " + row.OptionsBinding);
                    sb.AppendLine($"| `{first.File}:{first.Line}` | `{first.Type}` | {names} | {type.Sum(m => m.WritesProd)} / {type.Sum(m => m.WritesTests)} / {type.Sum(m => m.WritesSelf)} / {type.Sum(m => m.Unresolved)} | {(first.Serializers.Length == 0 ? "-" : first.Serializers)} | {string.Join(", ", notes)} |");
                }
                sb.AppendLine();
            }
        }
        if (_genericJsonSites.Count > 0)
        {
            sb.AppendLine("### Вызовы сериализаторов с типом, неизвестным статически\n");
            foreach (var site in _genericJsonSites.OrderBy(s => s, StringComparer.Ordinal)) sb.AppendLine($"- `{site}`");
            sb.AppendLine();
        }
        if (_interfaceSetters.Count > 0)
        {
            sb.AppendLine("### `set` в интерфейсах (не считается, но держит сеттер реализации)\n");
            foreach (var site in _interfaceSetters.OrderBy(s => s, StringComparer.Ordinal)) sb.AppendLine($"- `{site}`");
            sb.AppendLine();
        }
        if (_generated.Count > 0)
        {
            sb.AppendLine("### Исключено как сгенерированное\n");
            foreach (var file in _generated.OrderBy(s => s, StringComparer.Ordinal)) sb.AppendLine($"- `{file}`");
            sb.AppendLine();
        }
        return sb.ToString();
    }
}
