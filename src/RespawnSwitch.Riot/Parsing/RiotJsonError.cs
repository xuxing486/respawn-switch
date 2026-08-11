namespace RespawnSwitch.Riot.Parsing;

internal sealed record RiotJsonError(
    string Code,
    string JsonPath,
    string Detail);
