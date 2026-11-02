using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace ContainerGenerator {
    internal sealed class ScopePlan {
        public string RootId { get; init; } = "";
        public string Namespace { get; init; } = "";
        public string ClassName { get; init; } = "";
        public string HintName { get; init; } = "";
        public string LifetimeType { get; init; } = "global::Internal.ILifetime";
        public string LifetimeParam { get; init; } = "lifetime";
        public string LifetimeField { get; init; } = "_lifetime";
        public string ParentType { get; init; } = "global::Internal.IContainer";
        public string ParentParam { get; init; } = "parent";
        public string ParentField { get; init; } = "_parent";
        public string OriginRootId { get; init; } = "";
        public string ViewType { get; init; } = "";
        public bool NeedsRequest { get; private set; }
        public bool HasContainerInterface { get; init; }
        public bool HasUnityGameObject { get; init; }
        public bool HasProvides { get; init; }
        public bool HasDiagnosticsType { get; init; }
        public bool HasRegistrationInfo { get; init; }
        public bool HasServiceLifetime { get; init; }
        public bool HasRuntimeInitialize { get; init; }
        public bool HasContainerDiagnostics { get; init; }
        public INamedTypeSymbol? ProvidesDefinition { get; init; }
        public List<DiagnosticRegistration> DiagnosticRegistrations { get; } = new List<DiagnosticRegistration>();
        public List<CtorParam> CtorParams { get; } = new List<CtorParam>();
        public List<Field> Fields { get; } = new List<Field>();
        public List<Slot> Slots { get; } = new List<Slot>();
        public List<int> AlternativeGroups { get; } = new List<int>();
        public List<Marker> Markers { get; } = new List<Marker>();
        public List<Export> Exports { get; } = new List<Export>();
        public List<Factory> Factories { get; } = new List<Factory>();
        public List<Provide> Provides { get; } = new List<Provide>();
        public List<int> DisposeOrder { get; } = new List<int>();
        // The slots that give a type (its services and the implementation), in the order of registration.
        public Dictionary<string, List<int>> Providers { get; } = new Dictionary<string, List<int>>(StringComparer.Ordinal);
        public HashSet<int> DisposableSlots { get; } = new HashSet<int>();

        public void SetNeedsRequest(bool needsRequest) {
            NeedsRequest = needsRequest;
        }

        public bool HasTransientSlot(List<int> slots) {
            for (var i = 0; i < slots.Count; i++) {
                if (Slots[slots[i]].IsTransient)
                    return true;
            }

            return false;
        }

        public sealed class CtorParam {
            public string Type { get; init; } = "";
            public string Name { get; init; } = "";
            public int Slot { get; init; } = -1;
            public string FieldName { get; init; } = "";
            public bool IsLifetime { get; init; }
            public bool IsParent { get; init; }
            public bool IsRequest { get; init; }
            // All builder.Inject instances of the slot type: Type is the array, ElementType is the type itself.
            public bool IsInjections { get; init; }
            public string ElementType { get; init; } = "";
            // The occurrence-th RegisterInstance / RegisterComponent of Type: request.GetInstance<Type>(Occurrence).
            public bool IsInstance { get; init; }
            public int Occurrence { get; init; } = -1;
            // A WithParameter hole of one registration: request.GetParameter<Type>(typeof(Implementation), Occurrence);
            // of an alternative: only when its branch ran.
            public bool IsParameter { get; init; }
            public bool IsAlternativeParameter { get; init; }
            public string ParameterImplementation { get; init; } = "";
            public int OwnerSlot { get; init; } = -1;
            // An export of a parent scope: request.GetFromParent<Type>().
            public bool IsParentHole { get; init; }
            // The edges this parameter feeds: a hole is bound to its registration, not only to its type.
            public List<GraphEdge> Edges { get; } = new List<GraphEdge>();
        }

        public sealed class Field {
            public string Type { get; init; } = "";
            public string Name { get; init; } = "";
            public int Slot { get; init; } = -1;
            public bool IsMarker { get; init; }
            public bool IsLifetime { get; init; }
        }

        public sealed class Slot {
            private bool _isHole;

            public int Index { get; init; }
            // A transient alternative: whether its branch ran, kept for the factories that run after the build.
            public string RegisteredField { get; private set; } = "";
            public GraphRegistration Registration { get; init; } = null!;
            public string ImplementationType { get; init; } = "";
            public string FieldName { get; private set; } = "";
            public string FieldType { get; private set; } = "";
            public string CtorParamName { get; private set; } = "";
            // builder.Inject: the array of every passed instance and the loop index over it.
            public string InjectionsParamName { get; private set; } = "";
            public string InjectionsIndexName { get; private set; } = "";
            // Another builder.Inject call site of the same type owns the loop: GetInjections<T>() already returns
            // the instances of this call site, so this slot calls nothing (otherwise [Inject] would run twice).
            public bool IsInjectionsRepeat { get; private set; }
            public string CreateMethod { get; private set; } = "";
            public bool HasField { get; private set; }
            public bool IsTransient { get; init; }
            public bool IsHole { get => _isHole; init => _isHole = value; }
            public bool IsAlternative { get; init; }
            public bool IsSwitch { get; init; }
            public bool SkipEmit { get; init; }
            public bool IsUnity { get; private set; }
            public bool IsGroupPrimary { get; private set; }
            public List<int> GroupMembers { get; } = new List<int>();

            public void SetUnity(bool isUnity) {
                IsUnity = isUnity;
            }

            public void MarkHole() {
                _isHole = true;
            }

            public void SetCreateMethod(string name) {
                CreateMethod = name;
            }

            public void SetRegisteredField(string name) {
                RegisteredField = name;
            }

            public void JoinGroup(List<int> members, bool isPrimary) {
                GroupMembers.Clear();
                GroupMembers.AddRange(members);
                IsGroupPrimary = isPrimary;
            }

            public void AssignField(string type, string name) {
                HasField = true;
                FieldType = type;
                FieldName = name;
            }

            // A member of a group of alternatives: the field of the group, kept by its primary.
            public void ShareField(string type, string name) {
                FieldName = name;
                FieldType = type;
                HasField = IsGroupPrimary;
            }

            public void SetCtorParam(string name) {
                CtorParamName = name;
            }

            public void OwnInjections(string paramName, string indexName) {
                InjectionsParamName = paramName;
                InjectionsIndexName = indexName;
            }

            public void RepeatInjections(Slot owner) {
                InjectionsParamName = owner.InjectionsParamName;
                InjectionsIndexName = owner.InjectionsIndexName;
                IsInjectionsRepeat = true;
            }
        }

        public sealed class Marker {
            public string ElementType { get; init; } = "";
            public string FieldName { get; init; } = "";
            public List<int> Slots { get; } = new List<int>();
        }

        public sealed class Export {
            public string ServiceType { get; init; } = "";
            public int Slot { get; init; }
            public string Value { get; init; } = "";
        }

        public sealed class Factory {
            public int Slot { get; init; }
            public string MethodName { get; init; } = "";
            public string ReturnType { get; init; } = "";
            public string DiscriminantType { get; init; } = "";
            public string DiscriminantName { get; init; } = "discriminant";
        }

        public sealed class Provide {
            public string TargetType { get; init; } = "";
            public int Slot { get; init; }
        }

        // A row of the Internal.RegistrationInfo table; an empty type: typeof cannot be written from the assembly of the scope.
        public sealed class DiagnosticRegistration {
            public string ImplementationType { get; init; } = "";
            public List<string> ServiceTypes { get; } = new List<string>();
            public string Lifetime { get; init; } = "Singleton";
            public List<int> Dependencies { get; } = new List<int>();
            public bool IsInstantiated { get; init; }
            public bool IsExternal { get; init; }
        }
    }
}
