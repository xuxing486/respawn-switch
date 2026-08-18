using System.Text;
using System.Text.Json;
using RespawnSwitch.Core.Clock;
using RespawnSwitch.Core.Game;

namespace RespawnSwitch.Riot.Parsing;

internal sealed record RiotParseResult<T>(
    T? Value,
    IReadOnlyList<RiotJsonError> Errors)
{
    public bool IsSuccess => Value is not null && Errors.Count == 0;
}

internal sealed record RiotPlayerSnapshot(
    string RiotId,
    bool IsDead,
    double? RespawnTimerRaw,
    int? Deaths,
    string ChampionName = "Unknown",
    int Kills = 0,
    int Assists = 0);

internal sealed record RiotGameStatsSnapshot(
    double GameTimeSeconds,
    string GameMode);

internal sealed record ActivePlayerSnapshot(string RiotId);

internal static class RiotJson
{
    public static RiotParseResult<T> ParseDocument<T>(
        string json,
        string schema,
        Func<JsonElement, List<RiotJsonError>, T?> parse)
    {
        try
        {
            var utf8 = Encoding.UTF8.GetBytes(json);
            var reader = new Utf8JsonReader(utf8, new JsonReaderOptions
            {
                CommentHandling = JsonCommentHandling.Disallow,
                AllowTrailingCommas = false
            });
            if (!reader.Read())
            {
                return new RiotParseResult<T>(default,
                    [new RiotJsonError($"riot.schema.{schema}.json-invalid", "$", "Expected a JSON value.")]);
            }

            using var document = JsonDocument.ParseValue(ref reader);
            if (reader.Read())
            {
                return new RiotParseResult<T>(default,
                    [new RiotJsonError($"riot.schema.{schema}.json-invalid", "$", "Unexpected data follows the JSON value.")]);
            }

            var errors = new List<RiotJsonError>();
            var value = parse(document.RootElement, errors);
            return new RiotParseResult<T>(value, errors);
        }
        catch (JsonException exception)
        {
            return new RiotParseResult<T>(default,
                [new RiotJsonError($"riot.schema.{schema}.json-invalid", "$", exception.Message)]);
        }
    }

    public static bool TryGetRequiredProperty(
        JsonElement element,
        string propertyName,
        string schema,
        string path,
        List<RiotJsonError> errors,
        out JsonElement value)
    {
        value = default;
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(propertyName, out value))
        {
            errors.Add(new RiotJsonError($"riot.schema.{schema}.{propertyName.ToLowerInvariant()}-missing", path, $"Required property '{propertyName}' is missing."));
            return false;
        }

        return true;
    }

    public static bool TryGetString(
        JsonElement element,
        string propertyName,
        string schema,
        string parentPath,
        List<RiotJsonError> errors,
        out string? value)
    {
        value = null;
        if (!TryGetRequiredProperty(element, propertyName, schema, $"{parentPath}.{propertyName}", errors, out var property))
        {
            return false;
        }

        if (property.ValueKind != JsonValueKind.String)
        {
            errors.Add(new RiotJsonError($"riot.schema.{schema}.{propertyName.ToLowerInvariant()}-invalid-type", $"{parentPath}.{propertyName}", "Expected a JSON string."));
            return false;
        }

        value = property.GetString();
        if (string.IsNullOrEmpty(value))
        {
            errors.Add(new RiotJsonError($"riot.schema.{schema}.{propertyName.ToLowerInvariant()}-invalid-value", $"{parentPath}.{propertyName}", "Expected a non-empty string."));
            return false;
        }

        return true;
    }

    public static bool TryGetFiniteDouble(
        JsonElement element,
        string propertyName,
        string schema,
        string parentPath,
        List<RiotJsonError> errors,
        out double value)
    {
        value = default;
        if (!TryGetRequiredProperty(element, propertyName, schema, $"{parentPath}.{propertyName}", errors, out var property))
        {
            return false;
        }

        if (property.ValueKind != JsonValueKind.Number)
        {
            errors.Add(new RiotJsonError($"riot.schema.{schema}.{propertyName.ToLowerInvariant()}-invalid-type", $"{parentPath}.{propertyName}", "Expected a JSON number."));
            return false;
        }

        value = property.GetDouble();
        if (!double.IsFinite(value))
        {
            errors.Add(new RiotJsonError($"riot.schema.{schema}.{propertyName.ToLowerInvariant()}-nonfinite", $"{parentPath}.{propertyName}", "Expected a finite JSON number."));
            return false;
        }

        return true;
    }
}

internal static class RiotSampleAssembler
{
    public static GameSample Assemble(
        long sampleId,
        long observedAtTimestamp,
        RiotPlayerSnapshot player,
        RiotGameStatsSnapshot stats,
        SchemaSource source,
        string timelineKey,
        RespawnTimerSemantics semantics)
    {
        var normalizer = new RespawnTimerNormalizer();
        double? seconds = normalizer.TryNormalize(player.RespawnTimerRaw, semantics, out var normalized)
            ? normalized
            : null;

        return new GameSample(
            sampleId,
            observedAtTimestamp,
            player.RiotId,
            player.IsDead,
            player.RespawnTimerRaw,
            seconds,
            stats.GameTimeSeconds,
            stats.GameMode,
            source,
            timelineKey,
            player.ChampionName,
            player.Kills,
            player.Deaths.GetValueOrDefault(),
            player.Assists);
    }
}
