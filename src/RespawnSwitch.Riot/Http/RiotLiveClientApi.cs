using RespawnSwitch.Core.Game;
namespace RespawnSwitch.Riot.Http;
public sealed class RiotLiveClientApi(HttpClient client, RiotRequestTimeouts timeouts)
{
    public Task<string> GetActivePlayerAsync(CancellationToken ct) => GetAsync("/liveclientdata/activeplayername", timeouts.ActivePlayer, ct);
    public Task<string> GetPlayerListAsync(CancellationToken ct) => GetAsync("/liveclientdata/playerlist", timeouts.PlayerList, ct);
    public Task<string> GetGameStatsAsync(CancellationToken ct) => GetAsync("/liveclientdata/gamestats", timeouts.GameStats, ct);
    public Task<string> GetAllGameDataAsync(CancellationToken ct) => GetAsync("/liveclientdata/allgamedata", timeouts.AllGameData, ct);
    public Task<string> GetOpenApiAsync(CancellationToken ct) => GetAsync("/swagger/v3/openapi.json", timeouts.OpenApi, ct);
    private async Task<string> GetAsync(string path, TimeSpan timeout, CancellationToken caller)
    {
        var uri = new Uri(RiotEndpoint.Origin, path);
        if (!RiotEndpoint.Allows(uri)) throw new RiotApiException(ProbeFailureKind.Unexpected, "Rejected Riot endpoint.");
        using var timeoutCts = new CancellationTokenSource(timeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(caller, timeoutCts.Token);
        try { using var response = await client.GetAsync(uri, linked.Token).ConfigureAwait(false); response.EnsureSuccessStatusCode(); return await response.Content.ReadAsStringAsync(linked.Token).ConfigureAwait(false); }
        catch (OperationCanceledException) when (!caller.IsCancellationRequested && timeoutCts.IsCancellationRequested) { throw new RiotApiException(ProbeFailureKind.Timeout, "Riot request timed out."); }
        catch (HttpRequestException ex) { throw new RiotApiException(ProbeFailureKind.ConnectionRefused, "Riot Live Client request failed.", ex); }
    }
}
