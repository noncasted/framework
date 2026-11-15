using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Internal.EditorTests
{
    // The live graph reads the whole tree before it clears its nodes: a failure must reach it, not become "no children".
    public class ContainerGraphTreeTests
    {
        [Test]
        public void ReadChildren_CoversTheWholeTree()
        {
            var grandChild = new Diagnostics();
            var child = new Diagnostics(grandChild);
            var root = new Diagnostics(child);

            var children = ContainerGraphTree.ReadChildren(new IContainerDiagnostics[] { root, null });

            Assert.AreEqual(3, children.Count);
            CollectionAssert.AreEqual(new[] { child }, children[root]);
            CollectionAssert.AreEqual(new[] { grandChild }, children[child]);
            Assert.IsEmpty(children[grandChild]);
        }

        [Test]
        public void ReadChildren_FailingChild_Throws()
        {
            var root = new Diagnostics(new Diagnostics(null));

            Assert.Throws<InvalidOperationException>(() => ContainerGraphTree.ReadChildren(new IContainerDiagnostics[] { root }));
        }

        private class Diagnostics : IContainerDiagnostics
        {
            // Children null: the read throws.
            public Diagnostics(params IContainerDiagnostics[] children)
            {
                _children = children;
            }

            private readonly IContainerDiagnostics[] _children;

            public string Name => "Scope";
            public IContainerDiagnostics Parent => null;
            public IReadOnlyList<IContainerDiagnostics> Children => _children ?? throw new InvalidOperationException("children failed");
            public IReadOnlyList<RegistrationInfo> Registrations => Array.Empty<RegistrationInfo>();
            public IReadOnlyList<int> BuildOrder => Array.Empty<int>();
            public IReadOnlyList<LoadedAssetInfo> LoadedAssets => Array.Empty<LoadedAssetInfo>();
            public IReadOnlyList<AllocationRecord> Allocations => Array.Empty<AllocationRecord>();
        }
    }
}
