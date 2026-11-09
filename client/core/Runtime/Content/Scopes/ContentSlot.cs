using System;
using Object = UnityEngine.Object;

namespace Internal
{
    // An object RequestContent loads before the container of the scope is built: Value is there after the build.
    public sealed class ContentSlot<T> where T : Object
    {
        internal ContentSlot(ContentReference<T> reference)
        {
            _reference = reference;
        }

        private readonly ContentReference<T> _reference;
        private T _value;

        public T Value
        {
            get
            {
                if (_value == null)
                    throw new InvalidOperationException($"[Content] {typeof(T).Name} {_reference} is requested, read it after the build of the scope");

                return _value;
            }
        }

        internal void Fill(T value)
        {
            _value = value;
        }
    }
}
