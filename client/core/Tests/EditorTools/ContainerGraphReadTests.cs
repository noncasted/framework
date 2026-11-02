using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Internal.EditorTests
{
    // A diagnostics source that throws must not take the editor tools down: the failure is logged and shown as a placeholder.
    public class ContainerGraphReadTests
    {
        [Test]
        public void List_Value_IsReturned()
        {
            var list = new[] { 1, 2 };

            Assert.AreSame(list, ContainerGraphRead.List<int>(() => list));
        }

        [Test]
        public void List_Null_IsEmpty()
        {
            Assert.IsEmpty(ContainerGraphRead.List<int>(() => null));
        }

        [Test]
        public void List_Throwing_IsLoggedAndEmpty()
        {
            LogAssert.Expect(LogType.Exception, new Regex("list failed"));

            Assert.IsEmpty(ContainerGraphRead.List<int>(() => throw new InvalidOperationException("list failed")));
        }

        [Test]
        public void Parent_NullOrThrowing_IsNull()
        {
            LogAssert.Expect(LogType.Exception, new Regex("parent failed"));

            Assert.IsNull(ContainerGraphRead.Parent(null));
            Assert.IsNull(ContainerGraphRead.Parent(new ThrowingDiagnostics()));
        }

        [Test]
        public void Parent_Value_IsReturned()
        {
            var parent = new NamedDiagnostics("parent", null);

            Assert.AreSame(parent, ContainerGraphRead.Parent(new NamedDiagnostics("child", parent)));
        }

        [Test]
        public void Name_CoversAbsentAndFailing()
        {
            LogAssert.Expect(LogType.Exception, new Regex("name failed"));

            Assert.AreEqual("(none)", ContainerGraphRead.Name(null));
            Assert.AreEqual("(unnamed)", ContainerGraphRead.Name(new NamedDiagnostics(string.Empty, null)));
            Assert.AreEqual("Scope", ContainerGraphRead.Name(new NamedDiagnostics("Scope", null)));
            Assert.AreEqual("(error)", ContainerGraphRead.Name(new ThrowingDiagnostics()));
        }

        [Test]
        public void TypeName_PrintsGenericsWithoutArity()
        {
            Assert.AreEqual("—", ContainerGraphRead.TypeName(null));
            Assert.AreEqual("Int32", ContainerGraphRead.TypeName(typeof(int)));
            Assert.AreEqual("Dictionary<String, List<Int32>>", ContainerGraphRead.TypeName(typeof(Dictionary<string, List<int>>)));
        }

        [Test]
        public void Registration_PrintsSlotTypesAndLifetime()
        {
            var none = new RegistrationInfo(1, typeof(int), null, ServiceLifetime.Singleton, null, false, false);
            var one = new RegistrationInfo(2, typeof(int), new[] { typeof(string) }, ServiceLifetime.Singleton, null, false, false);
            var two = new RegistrationInfo(3, null, new[] { typeof(string), typeof(int) }, ServiceLifetime.Singleton, null, false, false);

            Assert.AreEqual("[1] Int32 : — · Singleton", ContainerGraphRead.Registration(none));
            Assert.AreEqual("[2] Int32 : String · Singleton", ContainerGraphRead.Registration(one));
            Assert.AreEqual("[3] — : String, Int32 · Singleton", ContainerGraphRead.Registration(two));
        }

        [Test]
        public void Asset_PrintsPlaceholders()
        {
            Assert.AreEqual("label · group", ContainerGraphRead.Asset(new LoadedAssetInfo("label", "group")));
            Assert.AreEqual("(no label) · (no group)", ContainerGraphRead.Asset(new LoadedAssetInfo(null, string.Empty)));
        }

        [Test]
        public void PathOf_IncludesTheParentOnly()
        {
            var root = new NamedDiagnostics("Root", null);
            var child = new NamedDiagnostics("Child", root);

            Assert.AreEqual("Root", ContainerGraphRead.PathOf(root, null));
            Assert.AreEqual("Root / Child", ContainerGraphRead.PathOf(child, root));
        }

        private class NamedDiagnostics : IContainerDiagnostics
        {
            public NamedDiagnostics(string name, IContainerDiagnostics parent)
            {
                Name = name;
                Parent = parent;
            }

            public string Name { get; }
            public IContainerDiagnostics Parent { get; }
            public IReadOnlyList<IContainerDiagnostics> Children => Array.Empty<IContainerDiagnostics>();
            public IReadOnlyList<RegistrationInfo> Registrations => Array.Empty<RegistrationInfo>();
            public IReadOnlyList<int> BuildOrder => Array.Empty<int>();
            public IReadOnlyList<LoadedAssetInfo> LoadedAssets => Array.Empty<LoadedAssetInfo>();
            public IReadOnlyList<AllocationRecord> Allocations => Array.Empty<AllocationRecord>();
        }

        private class ThrowingDiagnostics : IContainerDiagnostics
        {
            public string Name => throw new InvalidOperationException("name failed");
            public IContainerDiagnostics Parent => throw new InvalidOperationException("parent failed");
            public IReadOnlyList<IContainerDiagnostics> Children => throw new InvalidOperationException();
            public IReadOnlyList<RegistrationInfo> Registrations => throw new InvalidOperationException();
            public IReadOnlyList<int> BuildOrder => throw new InvalidOperationException();
            public IReadOnlyList<LoadedAssetInfo> LoadedAssets => throw new InvalidOperationException();
            public IReadOnlyList<AllocationRecord> Allocations => throw new InvalidOperationException();
        }
    }
}
