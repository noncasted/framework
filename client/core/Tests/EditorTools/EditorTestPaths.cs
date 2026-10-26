using System;
using System.IO;
using NUnit.Framework;
using UnityEditor.Compilation;

namespace Internal.EditorTests
{
    // The folder of these tests: Assets/core/Tests/EditorTools in client/project, a Packages/ path in a game.
    public static class EditorTestPaths
    {
        public static string Root
        {
            get
            {
                var asmdef = CompilationPipeline.GetAssemblyDefinitionFilePathFromAssemblyName("Internal.Editor.Tests");
                return Path.GetDirectoryName(asmdef).Replace('\\', '/');
            }
        }

        // The generated bindings go only into a project folder, so the tests that generate them next to their
        // fixtures run where the framework lies under Assets/: client/project.
        public static void RequireAssets()
        {
            if (Root.StartsWith("Assets/", StringComparison.Ordinal) == false)
                Assert.Ignore("Runs in client/project, where the framework lies under Assets/.");
        }
    }
}
