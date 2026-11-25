namespace Internal
{
    public sealed class LifetimeRecord
    {
        public LifetimeRecord(int id, int parentId, int secondParentId, string owner, string stack, float createdAt)
        {
            Id = id;
            ParentId = parentId;
            SecondParentId = secondParentId;
            Owner = owner;
            Stack = stack;
            CreatedAt = createdAt;
        }

        public int Id { get; }
        public int ParentId { get; }

        // Second Intersect source; 0 for regular lifetimes.
        public int SecondParentId { get; }
        public string Owner { get; }
        public string Stack { get; }

        // Time since Play Mode start (editor clock when the queue is drained: realtimeSinceStartup resets in Play).
        public float CreatedAt { get; }
    }
}
