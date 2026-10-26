using System;

namespace Internal
{
    // A tab of the Project Tools window. The window creates every ProjectToolsTab marked with it through its public
    // parameterless constructor, in every editor assembly, so a project adds its own tabs by declaring them. Tabs go
    // by Order, then by title; the framework tabs take Scenes 100, Current Scene 200, Options 300, Assets 400,
    // User 500, so a project tab places itself between them (or before them with an order below 100).
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class ProjectToolsTabAttribute : Attribute
    {
        public ProjectToolsTabAttribute(int order)
        {
            Order = order;
        }

        public int Order { get; }
    }
}
