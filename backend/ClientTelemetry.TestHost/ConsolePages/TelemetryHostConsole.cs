using BlazorBlueprint.Components;
using Cluster.Deploy;
using Cluster.Monitoring.Online;
using Cluster.State;
using Console;
using Common.Extensions;
using Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Npgsql;
using NSubstitute;

namespace Framework.ClientTelemetry.TestHost.ConsolePages;

// The console of a game around the pages of the framework, for a look in a browser over the data of this host. The audit log is real;
// its table comes from the deploy setup of a game, so it is created here. The features of the cluster and the online statistics are
// stand-ins: this silo has neither.
public static class TelemetryHostConsole
{
    private const string _auditLogTable = """
        CREATE TABLE audit_log (
            id uuid NOT NULL DEFAULT gen_random_uuid(),
            timestamp timestamptz NOT NULL DEFAULT now(),
            action text NOT NULL,
            details text NOT NULL DEFAULT '',
            PRIMARY KEY (id)
        )
        """;

    public static async Task Add(WebApplicationBuilder builder, NpgsqlDataSource database)
    {
        await using (var command = database.CreateCommand(_auditLogTable))
            await command.ExecuteNonQueryAsync();

        // The assets of the packages come from the build output in any environment, this process is never published.
        builder.WebHost.UseStaticWebAssets();

        var source = Substitute.For<IDbSource>();
        source.Value.Returns(database);

        var features = Substitute.For<IClusterFeatures>();
        features.IsInitialized.Returns(true);
        features.Value.Returns(new ClusterFeaturesState());

        builder.Services.AddSingleton(source);
        builder.Services.AddSingleton(features);
        builder.Services.AddSingleton<IDeploymentState<OnlineStatsLiveData>, TelemetryHostOnline>();
        builder.Services.AddSingleton<IAuditLogStorage, AuditLogStorage>();
        builder.AddCommonConsoleComponents();
        builder.Services.AddBlazorBlueprintComponents();
        builder.Services.AddRazorComponents().AddInteractiveServerComponents();
    }

    public static void Map(WebApplication app, string stylesFolder)
    {
        app.UseStaticFiles(new StaticFileOptions { FileProvider = new PhysicalFileProvider(stylesFolder) });
        app.UseAntiforgery();
        app.MapStaticAssets();
        app.MapRazorComponents<TelemetryHostApp>().AddInteractiveServerRenderMode().AddAdditionalAssemblies(typeof(global::Console.Infrastructure.InfrastructureDashboard).Assembly);
    }
}
