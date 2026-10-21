using Cluster.Discovery;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Cluster.Coordination;

public interface IClusterStartupConfig
{
    // Roles that must be present in the service discovery before a participant finishes its startup,
    // in the order the console lists them.
    IReadOnlyList<ServiceTag> RequiredServices { get; }
}

public class ClusterStartupOptions
{
    // Set by the composition root through ConfigureRequiredServices; there is no default set.
    public IReadOnlyList<ServiceTag> RequiredServices { get; set; } = [];
}

public class ClusterStartupConfig : IClusterStartupConfig
{
    public ClusterStartupConfig(IOptions<ClusterStartupOptions> options)
    {
        var required = options.Value.RequiredServices;

        if (required.Count == 0)
            throw new InvalidOperationException(
                "Cluster startup requires at least one service role: call ConfigureRequiredServices in the composition root");

        if (required.Distinct().Count() != required.Count)
            throw new InvalidOperationException(
                $"Cluster startup required services contain duplicates: {string.Join(", ", required)}");

        RequiredServices = required.ToArray();
    }

    public IReadOnlyList<ServiceTag> RequiredServices { get; }
}

public static class ClusterStartupConfigExtensions
{
    public static IHostApplicationBuilder ConfigureRequiredServices(
        this IHostApplicationBuilder builder,
        params ServiceTag[] required)
    {
        builder.Services.Configure<ClusterStartupOptions>(options => options.RequiredServices = required);
        return builder;
    }
}
