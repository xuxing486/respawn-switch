namespace RespawnSwitch.Riot.Http;

public sealed record RiotRequestTimeouts(TimeSpan ActivePlayer, TimeSpan PlayerList, TimeSpan GameStats, TimeSpan AllGameData, TimeSpan OpenApi);
