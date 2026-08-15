using RespawnSwitch.Application.Media;
using RespawnSwitch.Windows.Media;

namespace RespawnSwitch.Windows.Tests.Media;

public sealed class GsmtcSessionCatalogTests
{
    private static readonly GsmtcMediaProfile Profile = new("douyin.aumid", "fingerprint-v1");

    [Fact]
    public void Select_ReturnsNoMatch_WhenAumidIsNotAnOrdinalExactMatch()
    {
        var sessions = new[] { Session("token", "DOUYIN.AUMID", "fingerprint-v1") };

        var result = GsmtcSessionCatalog.Select(sessions, Profile);

        Assert.Null(result.SelectedSession);
        Assert.Equal(MediaFailureKind.NoMatch, result.FailureKind);
    }

    [Fact]
    public void Select_ReturnsTheOnlyExactAumidAndFingerprintMatch()
    {
        var expected = Session("selected", "douyin.aumid", "fingerprint-v1");
        var sessions = new[]
        {
            Session("wrong-fingerprint", "douyin.aumid", "other"),
            expected,
            Session("wrong-aumid", "browser.aumid", "fingerprint-v1")
        };

        var result = GsmtcSessionCatalog.Select(sessions, Profile);

        Assert.Same(expected, result.SelectedSession);
        Assert.Equal(MediaFailureKind.None, result.FailureKind);
    }

    [Fact]
    public void Select_ReturnsAmbiguousMatch_WhenTwoSessionsHaveTheCalibratedIdentity()
    {
        var sessions = new[]
        {
            Session("first", "douyin.aumid", "fingerprint-v1"),
            Session("system-current", "douyin.aumid", "fingerprint-v1")
        };

        var result = GsmtcSessionCatalog.Select(sessions, Profile);

        Assert.Null(result.SelectedSession);
        Assert.Equal(MediaFailureKind.AmbiguousMatch, result.FailureKind);
    }

    [Fact]
    public void Select_DoesNotNeedMediaTitleToKeepTheSameIdentity()
    {
        var beforeTitleChange = Session("stable-token", "douyin.aumid", "fingerprint-v1");
        var afterTitleChange = Session("stable-token", "douyin.aumid", "fingerprint-v1");

        Assert.Equal("stable-token", GsmtcSessionCatalog.Select([beforeTitleChange], Profile).SelectedSession?.SessionToken);
        Assert.Equal("stable-token", GsmtcSessionCatalog.Select([afterTitleChange], Profile).SelectedSession?.SessionToken);
    }

    private static GsmtcSessionDescriptor Session(string token, string aumid, string fingerprint) =>
        new(token, aumid, fingerprint, PlaybackState.Paused, CanPlay: true, CanPause: true);
}
