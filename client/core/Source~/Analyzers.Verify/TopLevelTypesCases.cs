namespace Framework.Analyzers.Verify
{
    // FWR003 and FWR006: one top-level type per file (IX + X, X + XExtensions), the file named after it.
    internal static class TopLevelTypesCases
    {
        public static void Run(Harness harness)
        {
            harness.Case("FWR003+FWR006 one type named after the file", One("A.cs", "public class A { }"), new string[0]);

            harness.Case("FWR003 two classes", One("A.cs", @"
public class A { }
public class B { }"), new[] { "FWR003@3" });

            harness.Case("FWR003 the second type is reported, not the one the file is named after", One("B.cs", @"
public class A { }
public class B { }"), new[] { "FWR003@2" });

            harness.Case("FWR003 IX + X", One("A.cs", @"
public interface IA { }
public class A : IA { }"), new string[0]);

            harness.Case("FWR003 X + XExtensions", One("A.cs", @"
public class A { }
public static class AExtensions { public static void Use(this A a) { } }"), new string[0]);

            harness.Case("FWR003 IX + X + XExtensions", One("A.cs", @"
public interface IA { }
public class A : IA { }
public static class AExtensions { }"), new string[0]);

            harness.Case("FWR003 IX + Y is not a pair", One("A.cs", @"
public interface IA { }
public class B : IA { }"), new[] { "FWR003@3" });

            harness.Case("FWR003 generic variants of one name", One("Option.cs", @"
public class Option { }
public class Option<T> : Option { }"), new string[0]);

            harness.Case("FWR003 partial parts of one type", One("A.cs", @"
public partial class A { }
public partial class A { }"), new string[0]);

            harness.Case("FWR003 enum and delegate count", One("A.cs", @"
public class A { }
public enum Kind { One }
public delegate void Handler();"), new[] { "FWR003@3", "FWR003@4" });

            harness.Case("FWR003 nested types do not count", One("A.cs", @"
public class A
{
    public class Inner { }
    private enum State { On }
}"), new string[0]);

            harness.Case("FWR003 block and file-scoped namespaces", new[]
            {
                new Source("A.cs", @"
namespace Game
{
    namespace Inner
    {
        public class A { }
        public class B { }
    }
}"),
                new Source("C.cs", @"
namespace Game;
public class C { }
public record D(int Value);")
            }, new[] { "FWR003@7", "FWR003@4" });

            harness.Case("FWR003 tests count too", One("Tests/A.cs", @"
public class A { }
public class B { }"), new[] { "FWR003@3" }, assemblyName: "Game.Tests");

            harness.Case("FWR006 file named after another type", One("Wrong.cs", "public class A { }"), new[] { "FWR006@1" });

            harness.Case("FWR006 reported on the main type of the group", One("Wrong.cs", @"
public interface IA { }
public class A : IA { }"), new[] { "FWR006@3" });

            harness.Case("FWR006 a part of a partial type with a suffix", One("A.Host.cs", "public partial class A { }"),
                new string[0]);

            harness.Case("FWR006 IX.cs holding IX + X", One("IA.cs", @"
public interface IA { }
public class A : IA { }"), new string[0]);

            harness.Case("FWR006 a file without types", One("AssemblyInfo.cs", "[assembly: System.CLSCompliant(false)]"),
                new string[0]);

            harness.Case("FWR003+FWR006 generated code is skipped", One("Bindings.g.cs", @"
public class A { }
public class B { }"), new string[0]);
        }

        private static Source[] One(string path, string text)
        {
            return new[] { new Source(path, text) };
        }
    }
}
