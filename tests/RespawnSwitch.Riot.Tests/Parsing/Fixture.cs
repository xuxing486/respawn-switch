namespace RespawnSwitch.Riot.Tests.Parsing;

internal static class Fixture
{
    public static string Read(string name) => File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "Riot", name));
}
