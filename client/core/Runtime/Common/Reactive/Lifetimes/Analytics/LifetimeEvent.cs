#if UNITY_EDITOR
namespace Internal
{
    public readonly struct LifetimeEvent
    {
        public LifetimeEvent(
            LifetimeEventKind kind,
            int id,
            int parentId,
            int secondParentId,
            string owner,
            string stack)
        {
            Kind = kind;
            Id = id;
            ParentId = parentId;
            SecondParentId = secondParentId;
            Owner = owner;
            Stack = stack;
        }

        public LifetimeEventKind Kind { get; }
        public int Id { get; }

        // 0 = root: there is no parent or it is not a Lifetime (TerminatedLifetime and the like).
        public int ParentId { get; }

        // Second parent of Intersect (0 for regular lifetimes): terminates together with either of the two.
        public int SecondParentId { get; }

        // "Class:Method" of the first stack frame outside the lifetime infrastructure.
        public string Owner { get; }
        public string Stack { get; }
    }
}
#endif
