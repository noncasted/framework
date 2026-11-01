// Output of ContainerGenerator on the assemblies of a game, read only: the sources are the Compile items of the
// generated csproj. Nothing of the game is built or written.
// - The assemblies are compiled in memory in the order of their references, so the manifests of the references come from
//   the given generator. Assemblies without sources come from Library/ScriptAssemblies.
// - Other generators do not run: a compile error from a missing generated type does not concern the container generator.
// - _inputs.md5 is the md5 of the sources read: when two runs differ, it tells whether the game changed between them.
// usage: game_snapshot <ContainerGenerator.dll> <client dir of the game> <empty output dir>
using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

if (args.Length != 3)
    throw new ArgumentException("usage: game_snapshot <ContainerGenerator.dll> <client dir> <empty output dir>");

var generatorDll = Path.GetFullPath(args[0]);
var clientDir = Path.GetFullPath(args[1]).TrimEnd('/');
var outDir = Path.GetFullPath(args[2]);
if (Directory.Exists(outDir) && Directory.EnumerateFileSystemEntries(outDir).Any())
    throw new InvalidOperationException($"Output dir is not empty: {outDir}");
Directory.CreateDirectory(outDir);

var generators = new AssemblyLoadContext("generators").LoadFromAssemblyPath(generatorDll).GetTypes()
    .Where(t => t.GetCustomAttribute<GeneratorAttribute>() != null)
    .Select(t => ((IIncrementalGenerator)Activator.CreateInstance(t)!).AsSourceGenerator()).ToArray();
if (generators.Length == 0)
    throw new InvalidOperationException($"No generators in {generatorDll}");

var scriptAssemblies = Path.Combine(clientDir, "Library/ScriptAssemblies");
var stackFrame = new Regex(@"^\s*at .*$\r?\n?", RegexOptions.Multiline);
var inputs = new List<string>();
var summary = new List<string>();
string Md5(byte[] data) => Convert.ToHexString(MD5.HashData(data)).ToLowerInvariant();
string Clean(string text) => stackFrame.Replace(text, "");

var building = new HashSet<string>();
var built = new Dictionary<string, MetadataReference>();
var csprojs = Directory.GetFiles(clientDir, "*.csproj").ToDictionary(p => Path.GetFileNameWithoutExtension(p));

foreach (var name in csprojs.Keys.OrderBy(n => n, StringComparer.Ordinal))
    Build(name);

bool Build(string name)
{
    if (built.ContainsKey(name))
        return true;
    if (!building.Add(name))
        throw new InvalidOperationException($"Cycle of references at {name}");

    var xml = XDocument.Load(csprojs[name]);
    IEnumerable<XElement> All(string name) => xml.Descendants().Where(e => e.Name.LocalName == name);
    string[] Includes(string name) => All(name).Select(e => e.Attribute("Include")!.Value.Replace('\\', '/')).ToArray();
    string Full(string path) => Path.IsPathRooted(path) ? path : Path.GetFullPath(Path.Combine(clientDir, path));

    var compile = Includes("Compile").Select(Full).OrderBy(p => p, StringComparer.Ordinal).ToArray();
    if (compile.Length == 0 || !Includes("Analyzer").Any(a => a.EndsWith("/ContainerGenerator.dll")))
        return false;

    if (!LanguageVersionFacts.TryParse(All("LangVersion").First().Value, out var langVersion))
        throw new InvalidOperationException($"{name}: unknown LangVersion");
    var defines = All("DefineConstants").First().Value.Split(';', StringSplitOptions.RemoveEmptyEntries);
    var parse = new CSharpParseOptions(langVersion, DocumentationMode.Parse, SourceCodeKind.Regular, defines);

    var trees = new List<SyntaxTree>();
    var missing = 0;
    foreach (var file in compile)
    {
        // A csproj older than the sources lists files that moved: same input for both generators, counted in the summary.
        if (!File.Exists(file))
        {
            missing++;
            continue;
        }

        var bytes = File.ReadAllBytes(file);
        inputs.Add($"{Md5(bytes)}  {file}");
        trees.Add(CSharpSyntaxTree.ParseText(Encoding.UTF8.GetString(bytes), parse, file, Encoding.UTF8));
    }

    var references = new Dictionary<string, MetadataReference>();
    var missingReferences = 0;
    void AddReference(string key, string path)
    {
        if (File.Exists(path))
            references[key] = MetadataReference.CreateFromFile(path);
        else
            missingReferences++;
    }

    foreach (var hint in All("HintPath").Select(e => e.Value))
        AddReference(Path.GetFileNameWithoutExtension(hint), Full(hint));
    foreach (var reference in Includes("ProjectReference").Select(p => Path.GetFileNameWithoutExtension(p)!))
    {
        if (csprojs.ContainsKey(reference) && Build(reference))
            references[reference] = built[reference];
        else
            AddReference(reference, Path.Combine(scriptAssemblies, reference + ".dll"));
    }

    var compilation = CSharpCompilation.Create(name, trees,
        references.OrderBy(r => r.Key, StringComparer.Ordinal).Select(r => r.Value),
        new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
            allowUnsafe: All("AllowUnsafeBlocks").First().Value.Equals("True", StringComparison.OrdinalIgnoreCase),
            concurrentBuild: true, deterministic: true, nullableContextOptions: NullableContextOptions.Disable));

    var driver = CSharpGeneratorDriver.Create(generators, parseOptions: parse);
    var run = driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out _).GetRunResult();
    foreach (var failed in run.Results.Where(r => r.Exception != null))
        throw new InvalidOperationException($"{name}: the generator threw", failed.Exception);

    var dir = Path.Combine(outDir, name);
    var generated = run.Results.SelectMany(r => r.GeneratedSources).OrderBy(s => s.HintName, StringComparer.Ordinal).ToArray();
    var diagnostics = run.Results.SelectMany(r => r.Diagnostics)
        .Select(d => $"{d.Location.GetLineSpan().Path}({d.Location.GetLineSpan().StartLinePosition.Line + 1}): {d.Severity} {d.Id}: {Clean(d.GetMessage())}")
        .OrderBy(d => d, StringComparer.Ordinal).ToArray();
    if (generated.Length > 0 || diagnostics.Length > 0)
        Directory.CreateDirectory(dir);
    foreach (var source in generated)
        File.WriteAllText(Path.Combine(dir, source.HintName), source.SourceText.ToString(), new UTF8Encoding(false));
    if (diagnostics.Length > 0)
        File.WriteAllLines(Path.Combine(dir, "_diagnostics.txt"), diagnostics);
    summary.Add($"{name}: sources={compile.Length} generated={generated.Length} diagnostics={diagnostics.Length} errors={diagnostics.Count(d => d.Contains(": Error "))} missingSources={missing} missingReferences={missingReferences}");
    built[name] = output.ToMetadataReference();
    return true;
}

var manifest = Directory.GetFiles(outDir, "*", SearchOption.AllDirectories)
    .Select(f => Path.GetRelativePath(outDir, f)).OrderBy(f => f, StringComparer.Ordinal)
    .Select(f => $"{Md5(File.ReadAllBytes(Path.Combine(outDir, f)))}  {f}").ToArray();
File.WriteAllLines(Path.Combine(outDir, "MANIFEST.md5"), manifest);
File.WriteAllLines(Path.Combine(outDir, "_inputs.md5"), inputs);
File.WriteAllLines(Path.Combine(outDir, "_summary.txt"), summary);
Console.WriteLine($"assemblies={summary.Count} files={manifest.Length} inputs={Md5(Encoding.UTF8.GetBytes(string.Join("\n", inputs)))} output={Md5(Encoding.UTF8.GetBytes(string.Join("\n", manifest)))}");
