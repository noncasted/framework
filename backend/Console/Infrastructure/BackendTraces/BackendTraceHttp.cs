using System.Net;
using Microsoft.Extensions.Logging;

namespace Console.Infrastructure.BackendTraces;

// The HTTP boundary of the sources: one GET of a trace. A network failure, a timeout or an answer other than 200 / 404 is a
// message of the page, not an exception; the text of a 200 goes to the parser of the store, which throws on another shape.
public static class BackendTraceHttp
{
    public static async Task<BackendTraceResult> Read(
        HttpClient client,
        string path,
        BackendTraceStore store,
        Func<string, BackendTraceResult> parse,
        ILogger logger,
        CancellationToken cancellation)
    {
        string json;

        try
        {
            using var response = await client.GetAsync(path, cancellation);

            if (response.StatusCode == HttpStatusCode.NotFound)
                return new BackendTraceResult.NotFound(store.NotFound);

            if (!response.IsSuccessStatusCode)
                return Failed(logger, path, $"{store.Name} answered {(int)response.StatusCode} {response.ReasonPhrase}");

            json = await response.Content.ReadAsStringAsync(cancellation);
        }
        catch (HttpRequestException exception)
        {
            return Failed(logger, path, $"{store.Name} is not reachable: {exception.Message}");
        }
        catch (TaskCanceledException) when (!cancellation.IsCancellationRequested)
        {
            return Failed(logger, path, $"{store.Name} has not answered in {client.Timeout.TotalSeconds:0} s");
        }

        return parse(json);
    }

    private static BackendTraceResult Failed(ILogger logger, string path, string message)
    {
        logger.LogWarning("[Console] Backend trace {Path}: {Message}", path, message);
        return new BackendTraceResult.Failed(message);
    }
}
