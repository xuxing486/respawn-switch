using RespawnSwitch.Core.Game;
using RespawnSwitch.Riot.Http;
using RespawnSwitch.Riot.Tests.TestHttp;

namespace RespawnSwitch.Riot.Tests.Http;

public sealed class RiotHttpPolicyTests
{
    [Theory]
    [InlineData("https://127.0.0.1:2999/liveclientdata/playerlist", true)]
    [InlineData("https://localhost:2999/liveclientdata/playerlist", false)]
    [InlineData("https://[::1]:2999/liveclientdata/playerlist", false)]
    [InlineData("http://127.0.0.1:2999/liveclientdata/playerlist", false)]
    [InlineData("https://127.0.0.1:3000/liveclientdata/playerlist", false)]
    [InlineData("https://127.0.0.1:2999/liveclientdata/playerlist?q=1", false)]
    [InlineData("https://127.0.0.1:2999/liveclientdata/unknown", false)]
    public void Allows_OnlyFixedLiteralHttpsLoopbackPaths(string value, bool expected) =>
        Assert.Equal(expected, RiotEndpoint.Allows(new Uri(value)));

    [Fact]
    public async Task GetPlayerListAsync_ConstructsOnlyAnAllowedGetRequest()
    {
        HttpRequestMessage? captured = null;
        var handler = new RouteHttpMessageHandler((request, _) =>
        {
            captured = request;
            return Task.FromResult(RouteHttpMessageHandler.Json("[]"));
        });
        var api = CreateApi(handler, TimeSpan.FromSeconds(1));

        await api.GetPlayerListAsync(CancellationToken.None);

        Assert.Equal(HttpMethod.Get, captured!.Method);
        Assert.Equal("https://127.0.0.1:2999/liveclientdata/playerlist", captured.RequestUri!.AbsoluteUri);
    }

    [Fact]
    public async Task PerRequestTimeout_IsTypedAndCallerCancellationPropagates()
    {
        var handler = new RouteHttpMessageHandler(async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException("unreachable");
        });
        var api = CreateApi(handler, TimeSpan.FromMilliseconds(20));

        var timeout = await Assert.ThrowsAsync<RiotApiException>(() =>
            api.GetPlayerListAsync(CancellationToken.None));
        Assert.Equal(ProbeFailureKind.Timeout, timeout.Kind);

        using var caller = new CancellationTokenSource();
        caller.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            api.GetPlayerListAsync(caller.Token));
    }

    [Fact]
    public void Factory_DisablesRedirectsAndProxies()
    {
        using var handler = RiotHttpClientFactory.CreateHandler(new RiotTlsCertificateValidator());

        Assert.False(handler.AllowAutoRedirect);
        Assert.False(handler.UseProxy);
        Assert.Null(handler.Proxy);
    }

    private static RiotLiveClientApi CreateApi(HttpMessageHandler handler, TimeSpan timeout)
    {
        var client = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        var timeouts = new RiotRequestTimeouts(timeout, timeout, timeout, timeout, timeout);
        return new RiotLiveClientApi(client, timeouts);
    }
}
