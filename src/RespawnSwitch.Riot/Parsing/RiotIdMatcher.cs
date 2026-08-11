namespace RespawnSwitch.Riot.Parsing;

internal static class RiotIdMatcher
{
    public static IReadOnlyList<int> FindExactMatches(IReadOnlyList<string?> riotIds, string exactRiotId)
    {
        var matches = new List<int>();
        for (var index = 0; index < riotIds.Count; index++)
        {
            if (string.Equals(riotIds[index], exactRiotId, StringComparison.Ordinal))
            {
                matches.Add(index);
            }
        }

        return matches;
    }
}
