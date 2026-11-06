using System.Threading;
using Cysharp.Threading.Tasks;

namespace Measurement
{
    /// <summary>
    ///     One feature the measured player exercises, so that its engine and managed code stay in the build.
    ///     Returns a short summary for the log; throws when the feature does not work in the player.
    /// </summary>
    public interface IMeasurementPart
    {
        string Name { get; }

        UniTask<string> Run(CancellationToken cancellation);
    }
}
