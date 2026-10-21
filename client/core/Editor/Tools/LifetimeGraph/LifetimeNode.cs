using System;
using Unity.GraphToolkit.Editor;
using UnityEngine;

namespace Internal
{
    [Serializable]
    [Node("Lifetimes")]
    [UseWithGraph(typeof(LifetimeLiveGraph))]
    public sealed class LifetimeNode : Node
    {
        internal const string ParentPortName = "parent";
        internal const string ChildrenPortName = "children";
        internal const string SecondParentPortName = "intersect";

        private static readonly Color RootColor = new(0.29f, 0.72f, 0.71f);
        private static readonly Color ChildColor = new(0.36f, 0.63f, 0.80f);

        // The parent is terminated but the lifetime is alive: the subscription is not tied to the parent (new Lifetime(parent) without Child()).
        private static readonly Color OrphanColor = new(0.86f, 0.30f, 0.27f);

        [SerializeField]
        private int _lifetimeId;

        [SerializeField]
        private bool _isOrphan;

        [SerializeField]
        private bool _isIntersection;

        [SerializeField]
        private int _descendantCount;

        [SerializeField]
        private bool _isCollapsed;

        [NonSerialized]
        private bool _isApplied;

        internal int LifetimeId => _lifetimeId;

        internal int DescendantCount => _descendantCount;

        internal bool IsCollapsed => _isCollapsed;

        // Before AddNode: only Intersect has a second parent port, and ports are built when the node is added.
        internal void Prepare(LifetimeRecord record)
        {
            _lifetimeId = record.Id;
            _isIntersection = record.SecondParentId != 0;
        }

        // Called on every sync for all visible nodes: titles are touched only when changed.
        internal void Apply(LifetimeRecord record, bool isOrphan, int descendantCount, bool isCollapsed)
        {
            if (_isApplied == true &&
                _isOrphan == isOrphan &&
                _descendantCount == descendantCount &&
                _isCollapsed == isCollapsed)
                return;

            _isApplied = true;
            _isOrphan = isOrphan;
            _descendantCount = descendantCount;
            _isCollapsed = isCollapsed;

            Title = record.Owner;
            Subtitle = FormatSubtitle(record, isOrphan, descendantCount, isCollapsed);
            Tooltip = record.Stack;
            DefaultColor = isOrphan ? OrphanColor : record.ParentId == 0 ? RootColor : ChildColor;
        }

        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            context.AddInputPort(ParentPortName)
                   .WithDisplayName("Parent")
                   .WithCapacity(PortCapacity.Single)
                   .Build();

            if (_isIntersection == true)
            {
                context.AddInputPort(SecondParentPortName)
                       .WithDisplayName("Intersect")
                       .WithCapacity(PortCapacity.Single)
                       .Build();
            }

            context.AddOutputPort(ChildrenPortName)
                   .WithDisplayName("Children")
                   .WithCapacity(PortCapacity.Multi)
                   .Build();
        }

        private static string FormatSubtitle(
            LifetimeRecord record,
            bool isOrphan,
            int descendantCount,
            bool isCollapsed)
        {
            var subtitle = $"#{record.Id} · {record.CreatedAt:0.0}s";

            if (record.SecondParentId != 0)
                subtitle += $" · ∩ #{record.ParentId} #{record.SecondParentId}";

            if (isOrphan == true)
                subtitle += $" · parent #{record.ParentId} terminated";

            if (isCollapsed == true)
                subtitle += $" · {descendantCount} hidden";

            return subtitle;
        }
    }
}
