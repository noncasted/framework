namespace Framework.Analyzers.Verify
{
    // FWR001: public setters and public mutable fields, with every exception of CODE_STYLE_FULL.md.
    internal static class PublicSetterCases
    {
        private const string Stubs = @"
namespace MemoryPack { public sealed class MemoryPackableAttribute : System.Attribute { } }
namespace Microsoft.AspNetCore.Components
{
    public sealed class ParameterAttribute : System.Attribute { }
    public sealed class CascadingParameterAttribute : System.Attribute { }
}
";

        public static void Run(Harness harness)
        {
            harness.Case("FWR001 public set of a public class", One(@"
public class A
{
    public int Value { get; set; }
}"), new[] { "FWR001@4" });

            harness.Case("FWR001 private set, init, get-only, protected and internal set are allowed", One(@"
public class A
{
    public int B { get; private set; }
    public int C { get; init; }
    public int D { get; }
    public int E { get; protected set; }
    public int F { get; internal set; }
    public int G => B;
}
namespace System.Runtime.CompilerServices { internal static class IsExternalInit { } }"), new string[0]);

            harness.Case("FWR001 public mutable field, readonly and const allowed", One(@"
public class A
{
    public int Value;
    public readonly int Fixed = 1;
    public const int Constant = 2;
    public static int Shared;
    private int _own;
}"), new[] { "FWR001@4", "FWR001@7" });

            harness.Case("FWR001 a public member of an internal type counts", One(@"
internal class A
{
    public int Value { get; set; }
}"), new[] { "FWR001@4" });

            harness.Case("FWR001 a member of a private nested type does not", One(@"
public class A
{
    private class B
    {
        public int Value { get; set; }
        public int Field;
    }
}"), new string[0]);

            harness.Case("FWR001 static property", One(@"
public static class A
{
    public static int Value { get; set; }
}"), new[] { "FWR001@4" });

            harness.Case("FWR001 [MemoryPackable] type and its nested types are allowed", One(Stubs + @"
[MemoryPack.MemoryPackable]
public partial class Payload
{
    public int Value { get; set; }
    public int Field;
    public class Inner { public int Value { get; set; } }
}"), new string[0]);

            harness.Case("FWR001 [Serializable] data without logic: public fields allowed, setters not", One(@"
[System.Serializable]
public class TraceData
{
    public TraceData(int start) { Start = start; }
    public int Start;
    public int Duration;
    public int End => Start + Duration;
    public int Property { get; set; }
}"), new[] { "FWR001@9" });

            harness.Case("FWR001 [Serializable] type with a method keeps private fields", One(@"
[System.Serializable]
public class State
{
    public int Count;
    public void Add() { Count++; }
    public static State Merge(State a, State b) { return a; }
}"), new[] { "FWR001@5" });

            harness.Case("FWR001 Blazor parameters are allowed", One(Stubs + @"
public class Page
{
    [Microsoft.AspNetCore.Components.Parameter] public int Id { get; set; }
    [Microsoft.AspNetCore.Components.CascadingParameter] public int Theme { get; set; }
    public int Other { get; set; }
}"), new[] { "FWR001@13" });

            harness.Case("FWR001 named arguments of an attribute are allowed", One(@"
public sealed class MarkAttribute : System.Attribute
{
    public int Order { get; set; }
    public string Name;
}"), new string[0]);

            harness.Case("FWR001 override is allowed, the base of the sources is reported", One(@"
public abstract class Base
{
    public abstract int Value { get; set; }
}
public class Derived : Base
{
    public override int Value { get; set; }
}"), new[] { "FWR001@4" });

            harness.Case("FWR001 interface of the sources is reported, its implementation is not", One(@"
public interface IA
{
    int Value { get; set; }
}
public class A : IA
{
    public int Value { get; set; }
}"), new[] { "FWR001@4" });

            harness.Case("FWR001 implementation of a foreign interface and override of a foreign type are allowed", One(@"
public class A : Foreign.IState
{
    public object State { get; set; }
}
public class B : Foreign.Base
{
    public override int Value { get; set; }
}"), new string[0], references: new[]
            {
                Harness.Library("Foreign", @"
namespace Foreign
{
    public interface IState { object State { get; set; } }
    public abstract class Base { public abstract int Value { get; set; } }
}")
            });

            harness.Case("FWR001 a setter not required by the interface is reported", One(@"
public interface IA
{
    int Value { get; }
}
public class A : IA
{
    public int Value { get; set; }
}"), new[] { "FWR001@8" });

            harness.Case("FWR001 generated code is skipped", new[]
            {
                new Source("Runtime/A.g.cs", "public class A { public int Value { get; set; } }"),
                new Source("Runtime/B.cs", "// <auto-generated/>\npublic class B { public int Value { get; set; } }")
            }, new string[0]);

            harness.Case("FWR001 tests count too", new[]
            {
                new Source("Tests/A.cs", "public class A { public int Value { get; set; } }")
            }, new[] { "FWR001@1" }, assemblyName: "Game.Tests");
        }

        private static Source[] One(string text)
        {
            return new[] { new Source("Runtime/Case.cs", text) };
        }
    }
}
