namespace Framework.Analyzers.Verify
{
    // FWR005: lock on a lifetime outside Internal.Lifetime.
    internal static class LifetimeLockCases
    {
        private const string Lifetimes = @"
namespace Internal
{
    public interface IReadOnlyLifetime { bool IsTerminated { get; } }
    public interface ILifetime : IReadOnlyLifetime { void Terminate(); }
    public class Lifetime : ILifetime
    {
        private bool _isTerminated;
        public bool IsTerminated { get { lock (this) { return _isTerminated; } } }
        public void Terminate() { lock (this) { _isTerminated = true; } }
    }
}
";

        public static void Run(Harness harness)
        {
            harness.Case("FWR005 lock inside Lifetime is allowed", One(Lifetimes), new string[0]);

            harness.Case("FWR005 lock on ILifetime, IReadOnlyLifetime and Lifetime", One(Lifetimes + @"
public class Service
{
    private readonly object _lock = new();
    public void Use(Internal.ILifetime a, Internal.IReadOnlyLifetime b, Internal.Lifetime c)
    {
        lock (a) { }
        lock (b) { }
        lock (c) { }
        lock (_lock) { }
    }
}"), new[] { "FWR005@19", "FWR005@20", "FWR005@21" });

            harness.Case("FWR005 a derived lifetime and a generic lifetime", One(Lifetimes + @"
public class Child : Internal.Lifetime
{
    public void Use() { lock (this) { } }
}
public class Service
{
    public void Use<T>(T lifetime) where T : Internal.ILifetime { lock (lifetime) { } }
}"), new[] { "FWR005@16", "FWR005@20" });

            harness.Case("FWR005 without the reactive primitives", One(@"
public class Service
{
    public void Use(object value) { lock (value) { } }
}"), new string[0]);
        }

        private static Source[] One(string text)
        {
            return new[] { new Source("Runtime/Case.cs", text) };
        }
    }
}
