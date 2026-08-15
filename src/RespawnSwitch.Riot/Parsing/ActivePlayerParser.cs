using System.Text.Json;

namespace RespawnSwitch.Riot.Parsing;

internal static class ActivePlayerParser
{
    public static RiotParseResult<ActivePlayerSnapshot> Parse(string json) =>
        RiotJson.ParseDocument(json, "activeplayer", ParseElement);

    private static ActivePlayerSnapshot? ParseElement(JsonElement root, List<RiotJsonError> errors)
    {
        if (root.ValueKind == JsonValueKind.String && !string.IsNullOrEmpty(root.GetString()))
        {
            return new ActivePlayerSnapshot(root.GetString()!);
        }
        if (root.ValueKind != JsonValueKind.Object)
        {
            errors.Add(new RiotJsonError("riot.schema.activeplayer.root-invalid-type", "$", "Expected a JSON object."));
            return null;
        }

        return RiotJson.TryGetString(root, "riotId", "activeplayer", "$", errors, out var riotId)
            ? new ActivePlayerSnapshot(riotId!)
            : null;
    }
}
