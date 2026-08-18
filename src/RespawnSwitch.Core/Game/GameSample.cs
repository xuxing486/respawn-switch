namespace RespawnSwitch.Core.Game;

public enum SchemaSource
{
    PlayerList,
    AllGameData
}

public sealed record GameSample(
    long SampleId,
    long ObservedAtTimestamp,
    string RiotId,
    bool IsDead,
    double? RespawnTimerRaw,
    double? RespawnTimerSeconds,
    double GameTimeSeconds,
    string GameMode,
    SchemaSource SchemaSource,
    string TimelineKey,
    string ChampionName = "Unknown",
    int Kills = 0,
    int Deaths = 0,
    int Assists = 0);
