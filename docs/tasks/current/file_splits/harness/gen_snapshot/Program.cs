// Snapshot of the output of ContainerGenerator for the assemblies of client/project.
//
// usage: gen_snapshot <ContainerGenerator.dll> <source root> <empty output dir> <project tree>
//
// - <source root> gives every first party .cs file: the list comes from the asmdef folders, not from the Compile items
//   of the csproj, so a file added by a split is compiled too. The csproj list is only compared and reported.
// - <project tree> gives the generated csproj, Library and the references. Absolute paths of the csproj written for
//   another tree are rewritten to it.
// - First party assemblies are compiled in memory in dependency order (with MemoryPack.Generator, whose output they
//   need), so manifests of referenced assemblies come from the given generator, not from Library/ScriptAssemblies.
// - Syntax trees use the path relative to client/project and are sorted: the output depends on neither root nor order.
using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

if (args.Length != 4)
    throw new ArgumentException("usage: gen_snapshot <ContainerGenerator.dll> <source root> <empty output dir> <project tree>");

var generatorDll = Path.GetFullPath(args[0]);
var sourceRoot = Path.GetFullPath(args[1]).TrimEnd('/');
var outDir = Path.GetFullPath(args[2]);
var projectTree = Path.GetFullPath(args[3]).TrimEnd('/');

if (Directory.Exists(outDir) && Directory.EnumerateFileSystemEntries(outDir).Any())
    throw new InvalidOperationException($"Output dir is not empty: {outDir}");
Directory.CreateDirectory(outDir);

var sourceProject = Path.Combine(sourceRoot, "client/project");
var csprojDir = Path.Combine(projectTree, "client/project");
var foreignPrefix = new Regex(@"^/projects/framework(?:/\.claude/worktrees/[^/]+)?/");

// A path of the csproj as it is on this machine: never a file of another tree.
string Rewrite(string path)
{
    var rewritten = Path.IsPathRooted(path)
        ? foreignPrefix.Replace(path, projectTree + "/")
        : Path.Combine(csprojDir, path);
    if (rewritten.StartsWith("/projects/framework/") && !rewritten.StartsWith(projectTree + "/"))
        throw new InvalidOperationException($"{path} stays in another tree: {rewritten}");
    if (!File.Exists(rewritten))
        throw new FileNotFoundException($"Not found: {rewritten} (from csproj path {path})");
    return rewritten;
}

var projects = Directory.GetFiles(csprojDir, "*.csproj").Select(Load).Where(p => p != null).Select(p => p!)
    .ToDictionary(p => p.Name);
if (!projects.ContainsKey("Internal"))
    throw new InvalidOperationException($"No Internal.csproj in {csprojDir}");

ProjectInfo? Load(string csproj)
{
    var xml = XDocument.Load(csproj);
    IEnumerable<XElement> All(string name) => xml.Descendants().Where(e => e.Name.LocalName == name);
    string[] Includes(string name) => All(name).Select(e => e.Attribute("Include")!.Value.Replace('\\', '/')).ToArray();

    var compile = Includes("Compile");
    if (compile.Length == 0 || !compile.All(c => c.StartsWith("Assets/")))
        return null;

    return new ProjectInfo(
        Path.GetFileNameWithoutExtension(csproj),
        compile,
        Includes("None").Where(n => n.EndsWith(".asmdef") || n.EndsWith(".asmref")).ToArray(),
        Includes("ProjectReference").Select(Path.GetFileNameWithoutExtension).Select(n => n!).ToArray(),
        All("HintPath").Select(e => e.Value).ToArray(),
        Includes("Analyzer"),
        All("LangVersion").First().Value,
        All("DefineConstants").First().Value.Split(';', StringSplitOptions.RemoveEmptyEntries),
        All("AllowUnsafeBlocks").First().Value.Equals("True", StringComparison.OrdinalIgnoreCase));
}

var generatorContext = new AssemblyLoadContext("generators");
object[] LoadGenerators(string dll) => generatorContext.LoadFromAssemblyPath(dll).GetTypes()
    .Where(t => t.GetCustomAttribute<GeneratorAttribute>() != null)
    .Select(t => Activator.CreateInstance(t)!).ToArray();
ISourceGenerator Wrap(object g) => g is IIncrementalGenerator i ? i.AsSourceGenerator() : (ISourceGenerator)g;

var containerGenerators = LoadGenerators(generatorDll).Select(Wrap).ToArray();
if (containerGenerators.Length == 0)
    throw new InvalidOperationException($"No generators in {generatorDll}");

var memoryPackDll = Rewrite(projects["Internal"].Analyzers.Single(a => a.EndsWith("/MemoryPack.Generator.dll")));
var helperGenerators = LoadGenerators(memoryPackDll).Select(Wrap).ToArray();

// Files of an assembly in the source root: the asmdef / asmref folders without nested assemblies and ignored folders.
List<string> SourceFiles(ProjectInfo project)
{
    var result = new List<string>();
    foreach (var marker in project.AsmFiles)
    {
        var folder = Path.GetDirectoryName(marker)!;
        Walk(Path.Combine(sourceProject, folder), folder, true);
    }
    result.Sort(StringComparer.Ordinal);
    return result;

    void Walk(string dir, string virtualDir, bool isRoot)
    {
        if (!Directory.Exists(dir))
            throw new DirectoryNotFoundException($"No assembly folder {dir}: the tree does not match {project.Name}.csproj");
        if (!isRoot && Directory.EnumerateFiles(dir).Any(f => f.EndsWith(".asmdef") || f.EndsWith(".asmref")))
            return;
        result.AddRange(Directory.GetFiles(dir, "*.cs").Select(f => virtualDir + "/" + Path.GetFileName(f)));
        foreach (var sub in Directory.GetDirectories(dir))
        {
            var name = Path.GetFileName(sub);
            if (!name.EndsWith('~') && !name.StartsWith('.'))
                Walk(sub, virtualDir + "/" + name, false);
        }
    }
}

// Every source must really come from the given root, symlinks of client/project/Assets included.
string ReadSource(string virtualPath)
{
    var path = Path.Combine(sourceProject, virtualPath);
    var real = RealPath(path);
    if (!real.StartsWith(sourceRoot + "/"))
        throw new InvalidOperationException($"{virtualPath} resolves outside the source root: {real}");
    return File.ReadAllText(path);
}

static string RealPath(string path)
{
    var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
    var current = "/";
    foreach (var part in parts)
    {
        current = Path.Combine(current, part);
        var info = new FileInfo(current);
        if (info.LinkTarget != null)
            current = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(current)!, info.LinkTarget));
    }
    return current;
}

var built = new Dictionary<string, MetadataReference>();
var summary = new List<string>();
var scriptAssemblies = Path.Combine(csprojDir, "Library/ScriptAssemblies");

void Build(string name)
{
    if (built.ContainsKey(name))
        return;
    var project = projects[name];
    foreach (var reference in project.ProjectReferences.Where(projects.ContainsKey))
        Build(reference);

    if (!LanguageVersionFacts.TryParse(project.LangVersion, out var langVersion))
        throw new InvalidOperationException($"{name}: unknown LangVersion {project.LangVersion}");
    var parse = new CSharpParseOptions(langVersion, DocumentationMode.Parse,
        SourceCodeKind.Regular, project.Defines);
    var files = SourceFiles(project);
    var trees = files.Select(f => CSharpSyntaxTree.ParseText(ReadSource(f), parse, f, Encoding.UTF8)).ToArray();

    var references = new Dictionary<string, MetadataReference>();
    foreach (var hint in project.HintPaths)
    {
        var fileName = Path.GetFileNameWithoutExtension(hint);
        if (!projects.ContainsKey(fileName))
            references[fileName] = MetadataReference.CreateFromFile(Rewrite(hint));
    }
    foreach (var reference in project.ProjectReferences)
        references[reference] = projects.ContainsKey(reference)
            ? built[reference]
            : MetadataReference.CreateFromFile(Rewrite(Path.Combine(scriptAssemblies, reference + ".dll")));

    var compilation = CSharpCompilation.Create(name, trees,
        references.OrderBy(r => r.Key, StringComparer.Ordinal).Select(r => r.Value),
        new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: project.AllowUnsafe,
            concurrentBuild: true, deterministic: true, nullableContextOptions: NullableContextOptions.Disable));

    var driver = CSharpGeneratorDriver.Create(containerGenerators.Concat(helperGenerators), parseOptions: parse);
    var ran = driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out _);
    var run = ran.GetRunResult();

    var own = run.Results.Where(r => containerGenerators.Contains(r.Generator)).ToArray();
    foreach (var failed in run.Results.Where(r => r.Exception != null))
        throw new InvalidOperationException($"{name}: generator {failed.Generator} threw", failed.Exception);

    var dir = Path.Combine(outDir, name);
    var generated = own.SelectMany(r => r.GeneratedSources).OrderBy(s => s.HintName, StringComparer.Ordinal).ToArray();
    foreach (var source in generated)
    {
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, source.HintName), source.SourceText.ToString(), new UTF8Encoding(false));
    }

    // Diagnostics of the generator are behaviour too, they enter the md5.
    var diagnostics = own.SelectMany(r => r.Diagnostics).Select(Format).OrderBy(d => d, StringComparer.Ordinal).ToArray();
    if (diagnostics.Length > 0)
    {
        Directory.CreateDirectory(dir);
        File.WriteAllLines(Path.Combine(dir, "_diagnostics.txt"), diagnostics);
    }

    var errors = output.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error)
        .Select(Format).OrderBy(d => d, StringComparer.Ordinal).ToArray();
    var onlyTree = files.Except(project.Compile).Count();
    var onlyCsproj = project.Compile.Except(files).Count();
    summary.Add($"{name}: sources={files.Count} generated={generated.Length} generatorDiagnostics={diagnostics.Length} "
        + $"compileErrors={errors.Length} notInCsproj={onlyTree} missingFromTree={onlyCsproj}");
    if (errors.Length > 0)
        File.AppendAllLines(Path.Combine(outDir, "_compile_errors.log"), errors.Select(e => name + ": " + e));

    built[name] = output.ToMetadataReference();
}

static string Format(Diagnostic d)
{
    var span = d.Location.GetLineSpan();
    var where = d.Location.IsInSource ? $"{span.Path}({span.StartLinePosition.Line + 1},{span.StartLinePosition.Character + 1})" : "-";
    return $"{where}: {d.Severity} {d.Id}: {d.GetMessage()}";
}

foreach (var name in projects.Keys.OrderBy(n => n, StringComparer.Ordinal))
    Build(name);

// MANIFEST holds the md5 of every file, the total is the md5 of MANIFEST. Logs starting with "_compile" stay out of it.
string Md5(byte[] data) => Convert.ToHexString(MD5.HashData(data)).ToLowerInvariant();
var manifest = Directory.GetFiles(outDir, "*", SearchOption.AllDirectories)
    .Where(f => !Path.GetFileName(f).StartsWith("_compile"))
    .Select(f => Path.GetRelativePath(outDir, f))
    .OrderBy(f => f, StringComparer.Ordinal)
    .Select(f => $"{Md5(File.ReadAllBytes(Path.Combine(outDir, f)))}  {f}")
    .ToArray();
var manifestText = string.Join("\n", manifest) + "\n";
File.WriteAllText(Path.Combine(outDir, "MANIFEST.md5"), manifestText);
var total = Md5(Encoding.UTF8.GetBytes(manifestText));
File.WriteAllText(Path.Combine(outDir, "TOTAL.md5"), total + "\n");
File.WriteAllLines(Path.Combine(outDir, "_compile_summary.txt"), summary);

foreach (var line in summary)
    Console.WriteLine(line);
Console.WriteLine($"files={manifest.Length}");
Console.WriteLine($"TOTAL {total}");

record ProjectInfo(string Name, string[] Compile, string[] AsmFiles, string[] ProjectReferences, string[] HintPaths,
    string[] Analyzers, string LangVersion, string[] Defines, bool AllowUnsafe);
