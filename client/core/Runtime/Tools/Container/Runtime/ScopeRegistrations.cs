using System;
using System.Collections.Generic;

namespace Internal
{
    // What an installer wrote for one scope and the lookups of the generated class over it. A mutable struct kept in
    // a field of ContainerBuilder: a scope pays only for the lists, taken from the pool at the first registration.
    internal struct ScopeRegistrations
    {
        // Every registration by its index, in the order the installer wrote them: the implementation type
        // (installer if/switch branches are looked up by it) and the ready instance with its declared type.
        private List<ContainerEntry> _entries;

        // WithParameter parameters by the index of their registration.
        private List<ContainerHole> _holes;
        private List<object> _injections;

        public int AddEntry(ContainerEntry entry)
        {
            _entries ??= ContainerRegistrationPool.TakeEntries();
            _entries.Add(entry);
            return _entries.Count - 1;
        }

        public void AddInjection(object target)
        {
            _injections ??= new List<object>();
            _injections.Add(target);
        }

        public void SetParameter(int index, Type type, object value)
        {
            // A repeated WithParameter of the same type on the same registration replaces the value.
            for (var i = 0; i < (_holes?.Count ?? 0); i++)
            {
                if (_holes[i].Index == index && _holes[i].Type == type)
                {
                    _holes[i] = new ContainerHole(index, type, value);
                    return;
                }
            }

            _holes ??= ContainerRegistrationPool.TakeHoles();
            _holes.Add(new ContainerHole(index, type, value));
        }

        public void ReturnToPool()
        {
            if (_entries != null)
            {
                ContainerRegistrationPool.Return(_entries);
                _entries = null;
            }

            if (_holes != null)
            {
                ContainerRegistrationPool.Return(_holes);
                _holes = null;
            }
        }

        public readonly Type GetImplementation(int index)
        {
            return _entries[index].Implementation;
        }

        public readonly bool HasRegistration(Type implementation)
        {
            for (var i = 0; i < (_entries?.Count ?? 0); i++)
            {
                if (_entries[i].Implementation == implementation)
                    return true;
            }

            return false;
        }

        // The instance of the occurrence-th RegisterInstance / RegisterComponent with this declared type: the
        // generated class has a slot per such call and takes the instances in the order the installer wrote them.
        public readonly object GetInstance(string scopeName, Type serviceType, int occurrence)
        {
            var seen = 0;

            for (var i = 0; i < (_entries?.Count ?? 0); i++)
            {
                if (_entries[i].InstanceType != serviceType)
                    continue;

                if (seen == occurrence)
                    return _entries[i].Instance;

                seen++;
            }

            throw new InvalidOperationException(
                $"Scope '{scopeName}' has {seen} instance(s) of {serviceType.FullName}, the generated class expects at least " +
                $"{occurrence + 1}: a registration of it did not run (an if without else around RegisterInstance).");
        }

        // The WithParameter value for a parameter of this type on the occurrence-th registration of the implementation.
        public readonly bool TryGetParameter(
            string scopeName,
            Type implementation,
            int occurrence,
            Type type,
            out object value)
        {
            var seen = 0;

            for (var i = 0; i < (_entries?.Count ?? 0); i++)
            {
                if (_entries[i].InstanceType != null || _entries[i].Implementation != implementation)
                    continue;

                if (seen++ != occurrence)
                    continue;

                for (var h = 0; h < (_holes?.Count ?? 0); h++)
                {
                    if (_holes[h].Index == i && _holes[h].Type == type)
                    {
                        value = _holes[h].Value;
                        return true;
                    }
                }

                // A value the parameter can hold: the generator has checked that there is one such (CINGR014).
                for (var h = 0; h < (_holes?.Count ?? 0); h++)
                {
                    if (_holes[h].Index == i && type.IsAssignableFrom(_holes[h].Type) == true)
                    {
                        value = _holes[h].Value;
                        return true;
                    }
                }

                value = null;
                return false;
            }

            throw new InvalidOperationException(
                $"Scope '{scopeName}' has {seen} registration(s) of {implementation.FullName}, the generated class expects " +
                $"at least {occurrence + 1}.");
        }

        public readonly void VerifyCovered(string rootId, Type[] constructed, Type[] instances)
        {
            if (_entries != null)
                ContainerCoverage.Verify(rootId, _entries, constructed, instances);
        }

        public readonly bool TryGetHole(Type type, out object instance)
        {
            // By type only, the last registration wins; within it, the ready instance wins, then the parameter.
            // What a generated slot owns is taken by its registration (GetInstance, TryGetParameter): this lookup is
            // left to the switch arm parameters and the builder.Inject slots.
            for (var i = (_entries?.Count ?? 0) - 1; i >= 0; i--)
            {
                if (_entries[i].InstanceType != null && type.IsInstanceOfType(_entries[i].Instance) == true)
                {
                    instance = _entries[i].Instance;
                    return true;
                }

                for (var h = (_holes?.Count ?? 0) - 1; h >= 0; h--)
                {
                    if (_holes[h].Index == i && _holes[h].Type == type)
                    {
                        instance = _holes[h].Value;
                        return true;
                    }
                }
            }

            // builder.Inject(x): the generated class receives the instance as a hole and calls Construct itself.
            for (var i = (_injections?.Count ?? 0) - 1; i >= 0; i--)
            {
                if (type.IsInstanceOfType(_injections[i]) == true)
                {
                    instance = _injections[i];
                    return true;
                }
            }

            instance = null;
            return false;
        }

        // Every instance passed to Inject with this type (identical scene services): [Inject] is called on each.
        public readonly T[] GetInjections<T>()
        {
            var count = 0;

            for (var i = 0; i < (_injections?.Count ?? 0); i++)
            {
                if (_injections[i] is T)
                    count++;
            }

            var result = new T[count];
            var next = 0;

            for (var i = 0; i < (_injections?.Count ?? 0); i++)
            {
                if (_injections[i] is T injection)
                    result[next++] = injection;
            }

            return result;
        }
    }
}
