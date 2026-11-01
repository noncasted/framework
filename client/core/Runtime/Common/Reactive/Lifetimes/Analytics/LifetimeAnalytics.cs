#if UNITY_EDITOR
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Threading;
using Unity.Scripting.LifecycleManagement;

namespace Internal
{
    public enum LifetimeEventKind
    {
        Created,
        Terminated
    }

    public readonly struct LifetimeEvent
    {
        public LifetimeEvent(
            LifetimeEventKind kind,
            int id,
            int parentId,
            int secondParentId,
            string owner,
            string stack)
        {
            Kind = kind;
            Id = id;
            ParentId = parentId;
            SecondParentId = secondParentId;
            Owner = owner;
            Stack = stack;
        }

        public LifetimeEventKind Kind { get; }
        public int Id { get; }

        // 0 = root: there is no parent or it is not a Lifetime (TerminatedLifetime and the like).
        public int ParentId { get; }

        // Second parent of Intersect (0 for regular lifetimes): terminates together with either of the two.
        public int SecondParentId { get; }

        // "Class:Method" of the first stack frame outside the lifetime infrastructure.
        public string Owner { get; }
        public string Stack { get; }
    }

    /// <summary>
    /// Editor-only feed of lifetime creation/termination for the Lifetime Graph, installed into
    /// <see cref="LifetimeTracker"/> of Framework.Common on load. Lifetimes can be created on any thread,
    /// so events go through a concurrent queue that the editor drains on the main thread.
    /// </summary>
    [NoAutoStaticsCleanup]
    public static class LifetimeAnalytics
    {
        [UnityEditor.InitializeOnLoadMethod]
        private static void Install()
        {
            LifetimeTracker.Install(new Tracker());
        }

        private const int MaxStackLines = 12;

        // Frames of these types create a lifetime on behalf of the caller: the label is taken from the first frame after them.
        private static readonly HashSet<Type> _infrastructure = new()
        {
            typeof(Lifetime),
            typeof(LifetimeExtensions),
            typeof(LifetimeAnalytics),
            typeof(GameObjectLifetime),
            typeof(GameObjectLifetimeExtensions),
            typeof(ViewableList<,>),
            typeof(ViewableList<>),
            typeof(ViewableDictionary<,,>),
            typeof(ViewableDictionary<,>),
            typeof(LifetimedValue<>)
        };

        private static readonly ConcurrentQueue<LifetimeEvent> _events = new();
        private static int _lastId;

        // Set by the editor (LifetimeTracking): stack capture is expensive, so it is enabled only by the flag.
        public static bool IsEnabled { get; private set; }

        public static void SetEnabled(bool enabled)
        {
            IsEnabled = enabled;
        }

        public static bool TryDequeue(out LifetimeEvent lifetimeEvent)
        {
            return _events.TryDequeue(out lifetimeEvent);
        }

        public static void ClearQueue()
        {
            while (_events.TryDequeue(out _))
            {
            }
        }

        private static int OnCreated(IReadOnlyLifetime parent, IReadOnlyLifetime secondParent)
        {
            if (IsEnabled == false)
                return 0;

            var id = Interlocked.Increment(ref _lastId);
            var parentId = IdOf(parent);
            var secondParentId = IdOf(secondParent);
            var (owner, stack) = CaptureCreator();

            // An untracked first parent must not hide the second one: the tree is built by the primary one.
            if (parentId == 0)
            {
                parentId = secondParentId;
                secondParentId = 0;
            }

            _events.Enqueue(new LifetimeEvent(LifetimeEventKind.Created, id, parentId, secondParentId, owner, stack));
            return id;
        }

        private static void OnTerminated(int id)
        {
            // A lifetime created before tracking was enabled is not present in the graph.
            if (id == 0)
                return;

            _events.Enqueue(new LifetimeEvent(LifetimeEventKind.Terminated, id, 0, 0, null, null));
        }

        private static int IdOf(IReadOnlyLifetime lifetime)
        {
            return lifetime is Lifetime tracked ? tracked.TrackingId : 0;
        }

        private static (string owner, string stack) CaptureCreator()
        {
            var frames = new StackTrace(2, false).GetFrames();
            string owner = null;
            var stack = new StringBuilder();
            var lines = 0;
            string previous = null;

            foreach (var frame in frames)
            {
                var method = frame.GetMethod();

                if (method == null)
                    continue;

                var (type, methodName) = Resolve(method);

                if (type == null || IsHidden(type))
                    continue;

                var label = $"{FormatType(type)}:{methodName}";

                if (owner == null)
                {
                    if (IsInfrastructure(type))
                        continue;

                    owner = label;
                }

                // An async state machine yields MoveNext and Start of the same method in a row: one line per method.
                if (label == previous)
                    continue;

                if (lines >= MaxStackLines)
                    break;

                stack.AppendLine(label);
                previous = label;
                lines++;
            }

            return (owner ?? "(unknown)", stack.ToString());
        }

        // The compiler puts lambdas, local functions and async state machines into nested types "<Method>d__N",
        // "<>c__DisplayClass" and methods "<Method>b__N": unwrap them back to the original class and method.
        private static (Type type, string methodName) Resolve(MethodBase method)
        {
            var type = method.DeclaringType;
            var methodName = method.Name;

            if (methodName.StartsWith("<", StringComparison.Ordinal))
                methodName = Unwrap(methodName);

            while (type != null && type.Name.StartsWith("<", StringComparison.Ordinal))
            {
                var unwrapped = Unwrap(type.Name);

                if (unwrapped.Length > 0 && methodName is "MoveNext" or "Invoke")
                    methodName = unwrapped;

                type = type.DeclaringType;
            }

            return (type, methodName);
        }

        // "<OnSetup>b__3_0" -> "OnSetup", "<OnSetup>g__Local|3_0" -> "OnSetup.Local".
        private static string Unwrap(string name)
        {
            var close = name.IndexOf('>');

            if (close < 1)
                return name;

            var outer = name.Substring(1, close - 1);
            var local = name.IndexOf("g__", close, StringComparison.Ordinal);

            if (local < 0)
                return outer;

            var start = local + 3;
            var end = name.IndexOf('|', start);
            var inner = end < 0 ? name.Substring(start) : name.Substring(start, end - start);

            return outer.Length > 0 ? $"{outer}.{inner}" : inner;
        }

        private static bool IsInfrastructure(Type type)
        {
            var definition = type.IsGenericType ? type.GetGenericTypeDefinition() : type;
            return _infrastructure.Contains(definition);
        }

        private static bool IsHidden(Type type)
        {
            var space = type.Namespace;

            if (space == null)
                return false;

            return space.StartsWith("System", StringComparison.Ordinal) ||
                   space.StartsWith("Cysharp", StringComparison.Ordinal);
        }

        private static string FormatType(Type type)
        {
            var name = type.Name;
            var tick = name.IndexOf('`');
            return tick >= 0 ? name.Substring(0, tick) : name;
        }

        private sealed class Tracker : ILifetimeTracker
        {
            public int OnCreated(IReadOnlyLifetime parent, IReadOnlyLifetime secondParent)
            {
                return LifetimeAnalytics.OnCreated(parent, secondParent);
            }

            public void OnTerminated(int id)
            {
                LifetimeAnalytics.OnTerminated(id);
            }
        }
    }
}
#endif
