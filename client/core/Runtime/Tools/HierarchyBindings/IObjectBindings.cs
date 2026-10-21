namespace Internal
{
    // Marker of a generated bindings class. The generator uses it to find the view boundary:
    // it does not descend into an object with its own bindings, it stores a reference to its component instead.
    // This also resets the serialization nesting depth, which Unity limits to seven levels.
    public interface IObjectBindings
    {
        // Snapshot of the hierarchy at generation time: component names and types in traversal order.
        // Used to catch a mismatch between the generated class and the live hierarchy.
        string StructureHash { get; }
    }
}