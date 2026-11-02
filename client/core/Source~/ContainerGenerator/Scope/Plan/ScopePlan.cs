using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace ContainerGenerator {
    internal sealed class ScopePlan {
        public string RootId = "";
        public string Namespace = "";
        public string ClassName = "";
        public string HintName = "";
        public string LifetimeType = "global::Internal.ILifetime";
        public string LifetimeParam = "lifetime";
        public string LifetimeField = "_lifetime";
        public string ParentType = "global::Internal.IContainer";
        public string ParentParam = "parent";
        public string ParentField = "_parent";
        public string OriginRootId = "";
        public string ViewType = "";
        public bool NeedsRequest;
        public bool HasContainerInterface;
        public bool HasUnityGameObject;
        public bool HasProvides;
        public bool HasDiagnosticsType;
        public bool HasRegistrationInfo;
        public bool HasServiceLifetime;
        public bool HasRuntimeInitialize;
        public bool HasContainerDiagnostics;
        public INamedTypeSymbol? ProvidesDefinition;
        public List<DiagnosticRegistration> DiagnosticRegistrations = new List<DiagnosticRegistration>();
        public List<CtorParam> CtorParams = new List<CtorParam>();
        public List<Field> Fields = new List<Field>();
        public List<Slot> Slots = new List<Slot>();
        public List<int> AlternativeGroups = new List<int>();
        public List<Marker> Markers = new List<Marker>();
        public List<Export> Exports = new List<Export>();
        public List<Factory> Factories = new List<Factory>();
        public List<Provide> Provides = new List<Provide>();
        public List<int> DisposeOrder = new List<int>();
        // The slots that give a type (its services and the implementation), in the order of registration.
        public Dictionary<string, List<int>> Providers = new Dictionary<string, List<int>>(StringComparer.Ordinal);
        public HashSet<int> DisposableSlots = new HashSet<int>();

        public bool HasTransientSlot(List<int> slots) {
            for (var i = 0; i < slots.Count; i++) {
                if (Slots[slots[i]].IsTransient)
                    return true;
            }

            return false;
        }

        public sealed class CtorParam {
            public string Type = "";
            public string Name = "";
            public int Slot = -1;
            public string FieldName = "";
            public bool IsLifetime;
            public bool IsParent;
            public bool IsRequest;
            // All builder.Inject instances of the slot type: Type is the array, ElementType is the type itself.
            public bool IsInjections;
            public string ElementType = "";
            // The occurrence-th RegisterInstance / RegisterComponent of Type: request.GetInstance<Type>(Occurrence).
            public bool IsInstance;
            public int Occurrence = -1;
            // A WithParameter hole of one registration: request.GetParameter<Type>(typeof(Implementation), Occurrence);
            // of an alternative: only when its branch ran.
            public bool IsParameter;
            public bool IsAlternativeParameter;
            public string ParameterImplementation = "";
            public int OwnerSlot = -1;
            // An export of a parent scope: request.GetFromParent<Type>().
            public bool IsParentHole;
            // The edges this parameter feeds: a hole is bound to its registration, not only to its type.
            public List<GraphEdge> Edges = new List<GraphEdge>();
        }

        public sealed class Field {
            public string Type = "";
            public string Name = "";
            public int Slot = -1;
            public bool IsMarker;
            public bool IsLifetime;
        }

        public sealed class Slot {
            public int Index;
            // A transient alternative: whether its branch ran, kept for the factories that run after the build.
            public string RegisteredField = "";
            public GraphRegistration Registration = null!;
            public string ImplementationType = "";
            public string FieldName = "";
            public string FieldType = "";
            public string CtorParamName = "";
            // builder.Inject: the array of every passed instance and the loop index over it.
            public string InjectionsParamName = "";
            public string InjectionsIndexName = "";
            // Another builder.Inject call site of the same type owns the loop: GetInjections<T>() already returns
            // the instances of this call site, so this slot calls nothing (otherwise [Inject] would run twice).
            public bool IsInjectionsRepeat;
            public string CreateMethod = "";
            public bool HasField;
            public bool IsTransient;
            public bool IsHole;
            public bool IsAlternative;
            public bool IsSwitch;
            public bool SkipEmit;
            public bool IsUnity;
            public bool IsGroupPrimary;
            public List<int> GroupMembers = new List<int>();
        }

        public sealed class Marker {
            public string ElementType = "";
            public string FieldName = "";
            public List<int> Slots = new List<int>();
        }

        public sealed class Export {
            public string ServiceType = "";
            public int Slot;
            public string Value = "";
        }

        public sealed class Factory {
            public int Slot;
            public string MethodName = "";
            public string ReturnType = "";
            public string DiscriminantType = "";
            public string DiscriminantName = "discriminant";
        }

        public sealed class Provide {
            public string TargetType = "";
            public int Slot;
        }

        // A row of the Internal.RegistrationInfo table; an empty type: typeof cannot be written from the assembly of the scope.
        public sealed class DiagnosticRegistration {
            public string ImplementationType = "";
            public List<string> ServiceTypes = new List<string>();
            public string Lifetime = "Singleton";
            public List<int> Dependencies = new List<int>();
            public bool IsInstantiated;
            public bool IsExternal;
        }
    }
}
