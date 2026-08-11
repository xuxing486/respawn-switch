using System.Text.Json;

namespace RespawnSwitch.Riot.Parsing;

internal static class PlayerListParser
{
    public static RiotParseResult<RiotPlayerSnapshot> Parse(string json, string exactRiotId) =>
        RiotJson.ParseDocument(json, "playerlist", (root, errors) => ParseArray(root, exactRiotId, "playerlist", "$", errors));

    internal static RiotPlayerSnapshot? ParseArray(
        JsonElement players,
        string exactRiotId,
        string schema,
        string arrayPath,
        List<RiotJsonError> errors)
    {
        if (players.ValueKind != JsonValueKind.Array)
        {
            errors.Add(new RiotJsonError($"riot.schema.{schema}.root-invalid-type", arrayPath, "Expected a JSON array."));
            return null;
        }

        var elements = players.EnumerateArray().ToList();
        var ids = new List<string?>();
        foreach (var entry in elements)
        {
            ids.Add(entry.ValueKind == JsonValueKind.Object && entry.TryGetProperty("riotId", out var id) && id.ValueKind == JsonValueKind.String
                ? id.GetString()
                : null);
        }

        var matches = RiotIdMatcher.FindExactMatches(ids, exactRiotId);
        if (matches.Count == 0)
        {
            for (var candidateIndex = 0; candidateIndex < elements.Count; candidateIndex++)
            {
                var entry = elements[candidateIndex];
                var entryPath = $"{arrayPath}[{candidateIndex}]";
                if (entry.ValueKind != JsonValueKind.Object || !entry.TryGetProperty("riotId", out var id))
                {
                    errors.Add(new RiotJsonError($"riot.schema.{schema}.riotid-missing", $"{entryPath}.riotId", "Required property 'riotId' is missing."));
                }
                else if (id.ValueKind != JsonValueKind.String)
                {
                    errors.Add(new RiotJsonError($"riot.schema.{schema}.riotid-invalid-type", $"{entryPath}.riotId", "Expected a JSON string."));
                }
            }

            errors.Add(new RiotJsonError($"riot.schema.{schema}.player-not-found", arrayPath, "No player has the requested exact Riot ID."));
            return null;
        }

        if (matches.Count > 1)
        {
            errors.Add(new RiotJsonError($"riot.schema.{schema}.riotid-ambiguous", arrayPath, "More than one player has the requested exact Riot ID."));
            return null;
        }

        var index = matches[0];
        var player = elements[index];
        var path = $"{arrayPath}[{index}]";
        if (player.ValueKind != JsonValueKind.Object)
        {
            errors.Add(new RiotJsonError($"riot.schema.{schema}.player-invalid-type", path, "Expected a JSON object."));
            return null;
        }

        var validRiotId = RiotJson.TryGetString(player, "riotId", schema, path, errors, out var riotId);
        var validDead = TryGetBoolean(player, schema, path, errors, out var isDead);
        var validTimer = RiotJson.TryGetFiniteDouble(player, "respawnTimer", schema, path, errors, out var respawnTimer);
        var validDeaths = TryGetDeaths(player, schema, path, errors, out var deaths);

        return validRiotId && validDead && validTimer && validDeaths
            ? new RiotPlayerSnapshot(riotId!, isDead, respawnTimer, deaths)
            : null;
    }

    private static bool TryGetBoolean(JsonElement player, string schema, string path, List<RiotJsonError> errors, out bool value)
    {
        value = default;
        if (!RiotJson.TryGetRequiredProperty(player, "isDead", schema, $"{path}.isDead", errors, out var property))
        {
            return false;
        }

        if (property.ValueKind is not JsonValueKind.True and not JsonValueKind.False)
        {
            errors.Add(new RiotJsonError($"riot.schema.{schema}.isdead-invalid-type", $"{path}.isDead", "Expected a JSON boolean."));
            return false;
        }

        value = property.GetBoolean();
        return true;
    }

    private static bool TryGetDeaths(JsonElement player, string schema, string path, List<RiotJsonError> errors, out int value)
    {
        value = default;
        if (!RiotJson.TryGetRequiredProperty(player, "scores", schema, $"{path}.scores", errors, out var scores))
        {
            return false;
        }

        if (scores.ValueKind != JsonValueKind.Object)
        {
            errors.Add(new RiotJsonError($"riot.schema.{schema}.scores-invalid-type", $"{path}.scores", "Expected a JSON object."));
            return false;
        }

        if (!RiotJson.TryGetRequiredProperty(scores, "deaths", schema, $"{path}.scores.deaths", errors, out var deaths))
        {
            return false;
        }

        if (deaths.ValueKind != JsonValueKind.Number || !deaths.TryGetInt32(out value))
        {
            errors.Add(new RiotJsonError($"riot.schema.{schema}.deaths-invalid-type", $"{path}.scores.deaths", "Expected a 32-bit JSON integer."));
            return false;
        }

        return true;
    }
}
