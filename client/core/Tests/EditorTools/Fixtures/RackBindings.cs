using System;
using UnityEngine;

namespace Internal.EditorTests.Menu
{
    // Bindings with the shapes the generator writes for a group of siblings: an array of references and a nested
    // class with one more. Written by hand, the tests never regenerate it.
    public class RackBindings : ObjectBindings
    {
        [SerializeField, HideInInspector] private Transform[] _slots;
        [SerializeField, HideInInspector] private ShelfBindings _shelf;

        public Transform[] Slots => _slots;
        public ShelfBindings Shelf => _shelf;

        [Serializable]
        public sealed class ShelfBindings
        {
            [SerializeField, HideInInspector] private Transform[] _items;

            public Transform[] Items => _items;
        }
    }
}
