using System.Text.Json;

namespace RespawnSwitch.Riot.Parsing;

internal static class GameStatsParser
{
    public static RiotParseResult<RiotGameStatsSnapshot> Parse(string json) =>
        RiotJson.ParseDocument(json, "gamestats", (root, errors) => ParseElement(root, "gamestats", "$", errors));

    internal static RiotGameStatsSnapshot? ParseElement(
        JsonElement stats,
        string schema,
        string path,
        List<RiotJsonError> errors)
    {
        if (stats.ValueKind != JsonValueKind.Object)
        {
            errors.Add(new RiotJsonError($"riot.schema.{schema}.root-invalid-type", path, "Expected a JSON object."));
            return null;
        }

        var validTime = RiotJson.TryGetFiniteDouble(stats, "gameTime", schema, path, errors, out var gameTime);
        var validMode = RiotJson.TryGetString(stats, "gameMode", schema, path, errors, out var gameMode);
        return validTime && validMode ? new RiotGameStatsSnapshot(gameTime, gameMode!) : null;
    }
}
