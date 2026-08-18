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
        var validChampion = RiotJson.TryGetString(player, "championName", schema, path, errors, out var championName);
        var validScores = TryGetScores(player, schema, path, errors, out var kills, out var deaths, out var assists);

        return validRiotId && validDead && validTimer && validChampion && validScores
            ? new RiotPlayerSnapshot(riotId!, isDead, respawnTimer, deaths, championName!, kills, assists)
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

    private static bool TryGetScores(JsonElement player, string schema, string path, List<RiotJsonError> errors, out int kills, out int deathsValue, out int assists)
    {
        kills = deathsValue = assists = default;
        if (!RiotJson.TryGetRequiredProperty(player, "scores", schema, $"{path}.scores", errors, out var scores))
        {
            return false;
        }

        if (scores.ValueKind != JsonValueKind.Object)
        {
            errors.Add(new RiotJsonError($"riot.schema.{schema}.scores-invalid-type", $"{path}.scores", "Expected a JSON object."));
            return false;
        }

        if (!TryScore(scores, "deaths", schema, path, errors, required: true, out deathsValue))
        {
            return false;
        }

        return TryScore(scores, "kills", schema, path, errors, required: false, out kills) &
               TryScore(scores, "assists", schema, path, errors, required: false, out assists);
    }

    private static bool TryScore(JsonElement scores, string name, string schema, string path, List<RiotJsonError> errors, bool required, out int value)
    {
        value = 0;
        if (!scores.TryGetProperty(name, out var property))
        {
            if (required) errors.Add(new RiotJsonError($"riot.schema.{schema}.{name}-missing", $"{path}.scores.{name}", $"Required property '{name}' is missing."));
            return !required;
        }
        if (property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out value)) return true;
        errors.Add(new RiotJsonError($"riot.schema.{schema}.{name}-invalid-type", $"{path}.scores.{name}", "Expected a 32-bit JSON integer."));
        return false;
    }
}
