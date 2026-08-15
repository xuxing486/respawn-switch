namespace RespawnSwitch.Riot.Http;

public static class RiotEndpoint
{
    public static Uri Origin { get; } = new("https://127.0.0.1:2999/");
    private static readonly HashSet<string> AllowedPaths = new(StringComparer.Ordinal)
    {
        "/liveclientdata/activeplayername", "/liveclientdata/playerlist", "/liveclientdata/gamestats",
        "/liveclientdata/allgamedata", "/swagger/v3/openapi.json"
    };

    public static bool Allows(Uri requestUri) => requestUri.IsAbsoluteUri &&
        requestUri.Scheme == Uri.UriSchemeHttps && requestUri.Host == "127.0.0.1" && requestUri.Port == 2999 &&
        string.IsNullOrEmpty(requestUri.UserInfo) && string.IsNullOrEmpty(requestUri.Query) &&
        string.IsNullOrEmpty(requestUri.Fragment) && AllowedPaths.Contains(requestUri.AbsolutePath);
}
