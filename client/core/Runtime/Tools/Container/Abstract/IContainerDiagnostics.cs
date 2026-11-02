using System.Collections.Generic;

namespace Internal
{
    public interface IContainerDiagnostics
    {
        string Name { get; }
        IContainerDiagnostics Parent { get; }
        IReadOnlyList<IContainerDiagnostics> Children { get; }
        IReadOnlyList<RegistrationInfo> Registrations { get; }
        IReadOnlyList<int> BuildOrder { get; }
        IReadOnlyList<LoadedAssetInfo> LoadedAssets { get; }
        IReadOnlyList<AllocationRecord> Allocations { get; }
    }
}