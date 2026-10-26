using System;

namespace Internal
{
    // A step of Tools/Framework/Codegen/Regenerate All declared by an editor assembly of the project: the framework
    // editor does not reference the game assemblies and does not know their generators. Put it on a public static void
    // method without parameters. The step runs without a refresh (Regenerate All refreshes once at the end) and throws
    // when it fails. The steps run after the catalogues and before the container asset map, ordered by name; Project
    // Tools -> Assets shows a button for each.
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class RegenerateAllStepAttribute : Attribute
    {
        public RegenerateAllStepAttribute(string name)
        {
            Name = name;
        }

        public string Name { get; }
    }
}
