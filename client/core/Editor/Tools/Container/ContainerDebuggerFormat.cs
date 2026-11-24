using System.Collections.Generic;
using UnityEditor;

namespace Internal
{
    // The texts of the debugger. The reads of the diagnostics go through ContainerGraphRead: a source that throws shows a placeholder.
    internal static class ContainerDebuggerFormat
    {
        public static string ContainerLabel(IContainerDiagnostics diagnostics)
        {
            var name = ContainerGraphRead.Name(diagnostics);
            var count = ContainerGraphRead.List(() => diagnostics.Registrations).Count;
            return $"{name}  ({count}) · {Bytes(OwnBytes(diagnostics))}";
        }

        public static long OwnBytes(IContainerDiagnostics diagnostics)
        {
            long bytes = 0;

            foreach (var record in ContainerGraphRead.List(() => diagnostics.Allocations))
                bytes += record.Bytes;

            return bytes;
        }

        public static long SubtreeBytes(IContainerDiagnostics diagnostics, ref int containers, int depth)
        {
            containers++;
            var bytes = OwnBytes(diagnostics);

            if (depth >= 64)
                return bytes;

            foreach (var child in ContainerGraphRead.List(() => diagnostics.Children))
                bytes += SubtreeBytes(child, ref containers, depth + 1);

            return bytes;
        }

        public static string Phase(AllocationRecord record)
        {
            if (record.Phase == null)
                return "Build";

            return record.Phase.Name;
        }

        public static string Listener(AllocationRecord record)
        {
            if (record.Phase == null)
                return "(constructors)";

            return record.Listener == null ? "(resolve list)" : ContainerGraphRead.TypeName(record.Listener);
        }

        public static string Bytes(long bytes)
        {
            if (bytes < 1024)
                return $"{bytes} B";

            if (bytes < 1024 * 1024)
                return $"{bytes / 1024d:F1} KB";

            return $"{bytes / (1024d * 1024d):F2} MB";
        }

        public static string Path(IContainerDiagnostics diagnostics)
        {
            var parts = new List<string>();
            var current = diagnostics;
            var guard = 0;

            while (current != null && guard < 64)
            {
                parts.Add(ContainerGraphRead.Name(current));
                current = ContainerGraphRead.Parent(current);
                guard++;
            }

            parts.Reverse();
            return string.Join(" / ", parts);
        }

        public static string Flag(bool value)
        {
            return value ? "yes" : "no";
        }

        public static string Status(int rootCount, IReadOnlyCollection<IContainerDiagnostics> containers)
        {
            if (rootCount == 0)
                return EmptyMessage();

            var registrations = 0;
            long bytes = 0;

            foreach (var diagnostics in containers)
            {
                registrations += ContainerGraphRead.List(() => diagnostics.Registrations).Count;
                bytes += OwnBytes(diagnostics);
            }

            return $"{rootCount} root(s) · {containers.Count} container(s) · {registrations} registration(s)" +
                   $" · {Bytes(bytes)} allocated" +
                   (EditorApplication.isPlaying ? " · Play Mode" : " · Edit Mode");
        }

        public static string EmptyMessage()
        {
            return EditorApplication.isPlaying
                ? "No containers yet. Waiting for a container Build."
                : "No containers. Enter Play Mode to inspect a built graph.";
        }
    }
}
