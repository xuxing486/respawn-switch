using System.Text.Json;

namespace RespawnSwitch.Riot.Parsing;

internal static class AllGameDataParser
{
    public static RiotParseResult<(RiotPlayerSnapshot Player, RiotGameStatsSnapshot Stats)> Parse(string json, string exactRiotId) =>
        RiotJson.ParseDocument<(RiotPlayerSnapshot Player, RiotGameStatsSnapshot Stats)>(
            json,
            "allgamedata",
            (root, errors) => ParseElement(root, exactRiotId, errors).GetValueOrDefault());

    private static (RiotPlayerSnapshot Player, RiotGameStatsSnapshot Stats)? ParseElement(
        JsonElement root,
        string exactRiotId,
        List<RiotJsonError> errors)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            errors.Add(new RiotJsonError("riot.schema.allgamedata.root-invalid-type", "$", "Expected a JSON object."));
            return null;
        }

        var playersPresent = RiotJson.TryGetRequiredProperty(root, "allPlayers", "allgamedata", "$.allPlayers", errors, out var players);
        var statsPresent = RiotJson.TryGetRequiredProperty(root, "gameData", "allgamedata", "$.gameData", errors, out var stats);
        if (!playersPresent || !statsPresent)
        {
            return null;
        }

        var player = PlayerListParser.ParseArray(players, exactRiotId, "allgamedata", "$.allPlayers", errors);
        var gameStats = GameStatsParser.ParseElement(stats, "allgamedata", "$.gameData", errors);
        return player is not null && gameStats is not null ? (player, gameStats) : null;
    }
}
