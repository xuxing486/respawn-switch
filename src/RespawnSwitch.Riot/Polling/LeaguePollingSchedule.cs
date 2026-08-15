namespace RespawnSwitch.Riot.Polling;
public sealed record LeaguePollingSchedule(TimeSpan PlayerListInterval, TimeSpan GameStatsInterval, TimeSpan NoGameInitialBackoff, TimeSpan NoGameMaximumBackoff);
