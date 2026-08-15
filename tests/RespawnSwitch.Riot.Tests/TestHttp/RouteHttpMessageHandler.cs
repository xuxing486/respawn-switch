using System.Collections.Concurrent;
using System.Net;

namespace RespawnSwitch.Riot.Tests.TestHttp;

internal sealed class RouteHttpMessageHandler(
    Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
{
    private readonly ConcurrentDictionary<string, int> _counts = new(StringComparer.Ordinal);

    public int Count(string path) => _counts.TryGetValue(path, out var value) ? value : 0;

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        _counts.AddOrUpdate(request.RequestUri!.AbsolutePath, 1, (_, current) => current + 1);
        return respond(request, cancellationToken);
    }

    public static HttpResponseMessage Json(string json) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(json)
    };
}
