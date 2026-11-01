using System;
using System.Collections.Generic;
using System.Linq;

namespace ContainerGenerator {
    internal static class ContainerApiWriter {
        public static void WriteContainerApi(
            CodeWriter writer,
            ScopePlan plan,
            ScopeGraph graph,
            string indent,
            string body) {
            if (plan.HasDiagnosticsType) {
                var diagnostics = plan.HasContainerDiagnostics ? "_diagnostics" : "null";
                writer.AppendLine(indent + "public global::Internal.IContainerDiagnostics Diagnostics => " + diagnostics + ";");
                writer.AppendLine();
            }

            writer.AppendLine(indent + "public global::Internal.IReadOnlyLifetime Lifetime => " + plan.LifetimeField + ";");
            writer.AppendLine();
            writer.AppendLine(indent + "public object Resolve(global::System.Type type) {");
            WriteThrowIfDisposed(writer, plan, body);
            writer.AppendLine(body + "if (_exports.TryGetValue(type, out var export) == true) {");
            writer.AppendLine(body + "    var value = GetExport(export);");
            writer.AppendLine(body + "    if (value != null)");
            writer.AppendLine(body + "        return value;");
            writer.AppendLine(body + "}");
            WriteTransientResolve(writer, plan, body, false);
            writer.AppendLine(body + "if (" + plan.ParentField + " != null)");
            writer.AppendLine(body + "    return " + plan.ParentField + ".Resolve(type);");
            writer.AppendLine(body + "throw new global::System.InvalidOperationException(\"No registration for type \" + type.FullName + \".\");");
            writer.AppendLine(indent + "}");
            writer.AppendLine();
            writer.AppendLine(indent + "public T Resolve<T>() {");
            writer.AppendLine(body + "return (T)Resolve(typeof(T));");
            writer.AppendLine(indent + "}");
            writer.AppendLine();
            writer.AppendLine(indent + "public bool TryResolve(global::System.Type type, out object instance) {");
            writer.AppendLine(body + "if (type == null) {");
            writer.AppendLine(body + "    instance = null;");
            writer.AppendLine(body + "    return false;");
            writer.AppendLine(body + "}");
            WriteThrowIfDisposed(writer, plan, body);
            writer.AppendLine(body + "if (_exports.TryGetValue(type, out var export) == true) {");
            writer.AppendLine(body + "    instance = GetExport(export);");
            writer.AppendLine(body + "    if (instance != null)");
            writer.AppendLine(body + "        return true;");
            writer.AppendLine(body + "}");
            WriteTransientResolve(writer, plan, body, true);
            writer.AppendLine(body + "if (" + plan.ParentField + " != null)");
            writer.AppendLine(body + "    return " + plan.ParentField + ".TryResolve(type, out instance);");
            writer.AppendLine(body + "instance = null;");
            writer.AppendLine(body + "return false;");
            writer.AppendLine(indent + "}");
            writer.AppendLine();
            ExportTableWriter.WriteGetExport(writer, plan, indent, body);
            WriteResolveAll(writer, plan, indent, body);
            writer.AppendLine(indent + "public void Inject(object target) {");
            writer.AppendLine(body + "if (target == null)");
            writer.AppendLine(body + "    throw new global::System.ArgumentNullException(nameof(target));");
            WriteThrowIfDisposed(writer, plan, body);
            WriteInjectCases(writer, plan, graph, body);
            writer.AppendLine(body + "throw new global::System.InvalidOperationException(\"No generated inject for \" + target.GetType().FullName + \".\");");
            writer.AppendLine(indent + "}");
            writer.AppendLine();
            if (plan.HasUnityGameObject) {
                writer.AppendLine(indent + "public void InjectGameObject(global::UnityEngine.GameObject target) {");
                writer.AppendLine(body + "if (target == null)");
                writer.AppendLine(body + "    throw new global::System.ArgumentNullException(nameof(target));");
                writer.AppendLine(body + "var behaviours = target.GetComponentsInChildren<global::UnityEngine.MonoBehaviour>(true);");
                writer.AppendLine(body + "for (var i = 0; i < behaviours.Length; i++) {");
                writer.AppendLine(body + "    var behaviour = behaviours[i];");
                writer.AppendLine(body + "    if (behaviour == null)");
                writer.AppendLine(body + "        continue;");
                writer.AppendLine(body + "    Inject(behaviour);");
                writer.AppendLine(body + "}");
                writer.AppendLine(indent + "}");
                writer.AppendLine();
            }
            else {
                writer.AppendLine(indent + "public void InjectGameObject(object target) {");
                writer.AppendLine(body + "Inject(target);");
                writer.AppendLine(indent + "}");
                writer.AppendLine();
            }

            // The lifetime ends first: its listeners still see live services and may resolve them. Then the services
            // go in reverse construction order. A throwing step does not skip the rest; the errors are rethrown at the end.
            // Transients are not tracked: whoever resolves one owns it and disposes it.
            writer.AppendLine(indent + "public void Dispose() {");
            writer.AppendLine(body + "if (_isDisposing == true)");
            writer.AppendLine(body + "    return;");
            writer.AppendLine(body + "_isDisposing = true;");
            writer.AppendLine(body + "global::System.Collections.Generic.List<global::System.Exception> errors = null;");
            WriteDisposeStep(writer, body, plan.LifetimeField + ".Terminate();");
            writer.AppendLine(body + "_disposed = true;");
            for (var i = 0; i < plan.DisposeOrder.Count; i++) {
                var slot = plan.Slots[plan.DisposeOrder[i]];
                if (string.IsNullOrEmpty(slot.FieldName))
                    continue;
                WriteDisposeStep(writer, body, slot.IsAlternative
                    ? "(" + slot.FieldName + " as global::System.IDisposable)?.Dispose();"
                    : "((global::System.IDisposable)" + slot.FieldName + ").Dispose();");
            }

            writer.AppendLine(body + "global::Internal.GeneratedScopes.ThrowDisposeErrors(errors);");
            writer.AppendLine(indent + "}");
            writer.AppendLine();
        }

        private static void WriteDisposeStep(CodeWriter writer, string indent, string statement) {
            writer.AppendLine(indent + "try {");
            writer.AppendLine(indent + "    " + statement);
            writer.AppendLine(indent + "}");
            writer.AppendLine(indent + "catch (global::System.Exception exception) {");
            writer.AppendLine(indent + "    (errors ??= new global::System.Collections.Generic.List<global::System.Exception>()).Add(exception);");
            writer.AppendLine(indent + "}");
        }

        // A disposed scope resolves nothing: its singletons are disposed and its lifetime is over.
        private static void WriteThrowIfDisposed(CodeWriter writer, ScopePlan plan, string indent) {
            writer.AppendLine(indent + "if (_disposed == true)");
            writer.AppendLine(indent + "    throw new global::System.ObjectDisposedException(" + EmitLiterals.Literal(plan.ClassName) + ");");
        }

        private static void WriteTransientResolve(CodeWriter writer, ScopePlan plan, string indent, bool assign) {
            // One branch per type. As a constructor parameter, Resolve gives the last registration: a type whose last
            // registration is a singleton is exported (checked earlier) and gets no branch.
            var exported = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < plan.Exports.Count; i++)
                exported.Add(plan.Exports[i].ServiceType);

            var owners = new Dictionary<string, int>(StringComparer.Ordinal);
            for (var i = 0; i < plan.Slots.Count; i++) {
                foreach (var type in TransientTypes(plan, i)) {
                    if (exported.Contains(type) == false)
                        owners[type] = i;
                }
            }

            for (var i = 0; i < plan.Slots.Count; i++) {
                var slot = plan.Slots[i];
                var types = TransientTypes(plan, i);
                for (var t = 0; t < types.Count; t++) {
                    if (owners.TryGetValue(types[t], out var owner) == false || owner != i)
                        continue;
                    writer.AppendLine(indent + "if (type == typeof(" + types[t] + ")) {");
                    if (slot.IsAlternative || AlternativeExpressions.Chain(plan, types[t]).Count > 1) {
                        var value = AlternativeExpressions.ForResolve(plan, types[t]);
                        if (assign) {
                            writer.AppendLine(indent + "    instance = " + value + ";");
                            writer.AppendLine(indent + "    if (instance != null)");
                            writer.AppendLine(indent + "        return true;");
                        }
                        else {
                            writer.AppendLine(indent + "    var created = " + value + ";");
                            writer.AppendLine(indent + "    if (created != null)");
                            writer.AppendLine(indent + "        return created;");
                        }
                    }
                    else if (assign) {
                        writer.AppendLine(indent + "    instance = " + slot.CreateMethod + "();");
                        writer.AppendLine(indent + "    return true;");
                    }
                    else {
                        writer.AppendLine(indent + "    return " + slot.CreateMethod + "();");
                    }

                    writer.AppendLine(indent + "}");
                }
            }
        }

        private static List<string> TransientTypes(ScopePlan plan, int slotIndex) {
            var types = new List<string>();
            var slot = plan.Slots[slotIndex];
            if (slot.IsTransient == false || string.IsNullOrEmpty(slot.CreateMethod))
                return types;
            if (string.IsNullOrEmpty(slot.ImplementationType) == false)
                types.Add(slot.ImplementationType);
            for (var s = 0; s < slot.Registration.ServiceTypes.Count; s++) {
                var service = slot.Registration.ServiceTypes[s];
                if (string.IsNullOrEmpty(service) == false && types.Contains(service) == false)
                    types.Add(service);
            }

            return types;
        }

        private static void WriteResolveAll(CodeWriter writer, ScopePlan plan, string indent, string body) {
            writer.AppendLine(indent + "public global::System.Collections.Generic.IReadOnlyList<T> ResolveAll<T>() {");
            WriteThrowIfDisposed(writer, plan, body);
            for (var i = 0; i < plan.Markers.Count; i++) {
                var marker = plan.Markers[i];
                var list = PlanMarkers.HasTransientSlot(plan, marker.Slots) ? MarkerWriter.MarkerArray(plan, i) : marker.FieldName;
                writer.AppendLine(body + "if (typeof(T) == typeof(" + marker.ElementType + "))");
                writer.AppendLine(body + "    return (global::System.Collections.Generic.IReadOnlyList<T>)(object)" + list + ";");
            }

            // A type with a single registration gets no marker array.
            writer.AppendLine(body + "if (_exports.TryGetValue(typeof(T), out var single) == true) {");
            writer.AppendLine(body + "    var value = GetExport(single);");
            writer.AppendLine(body + "    return value != null ? new T[] { (T)value } : global::System.Array.Empty<T>();");
            writer.AppendLine(body + "}");
            writer.AppendLine(body + "return global::System.Array.Empty<T>();");
            writer.AppendLine(indent + "}");
            writer.AppendLine();
        }

        private static void WriteInjectCases(
            CodeWriter writer,
            ScopePlan plan,
            ScopeGraph graph,
            string indent) {
            var used = new HashSet<string>(StringComparer.Ordinal);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < plan.Slots.Count; i++) {
                var slot = plan.Slots[i];
                if (string.IsNullOrEmpty(slot.ImplementationType))
                    continue;
                if (seen.Add(slot.ImplementationType) == false)
                    continue;
                var args = ArgumentExpressions.Arguments(plan, graph, slot.Registration, "Construct");
                if (args == null)
                    continue;

                var local = ScopeNames.Unique(ScopeNames.Camel(ScopeNames.Short(slot.ImplementationType)), used);
                var method = ConstructorWriter.ConstructName(slot.Registration);
                writer.AppendLine(indent + "if (target is " + slot.ImplementationType + " " + local + ") {");
                if (args.Count == 0)
                    writer.AppendLine(indent + "    " + local + "." + method + "();");
                else
                    writer.AppendLine(indent + "    " + local + "." + method + "(" + string.Join(", ", args) + ");");
                writer.AppendLine(indent + "    return;");
                writer.AppendLine(indent + "}");
            }
        }
    }
}
