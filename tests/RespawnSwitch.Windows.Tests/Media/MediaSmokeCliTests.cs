using RespawnSwitch.MediaSmoke;

namespace RespawnSwitch.Windows.Tests.Media;

public sealed class MediaSmokeCliTests
{
    [Fact]
    public void Parse_RecognizesHelpWithoutAProfile()
    {
        var options = MediaSmokeCli.Parse(["--help"]);

        Assert.Equal(MediaSmokeCommand.Help, options.Command);
        Assert.True(options.IsValid);
    }

    [Fact]
    public void Parse_RecognizesReadOnlyListWithoutAProfile()
    {
        var options = MediaSmokeCli.Parse(["list"]);

        Assert.Equal(MediaSmokeCommand.List, options.Command);
        Assert.True(options.IsValid);
    }

    [Fact]
    public void Parse_RejectsCommandsOtherThanReadOnlyListAndProbe()
    {
        var play = MediaSmokeCli.Parse(["play", "--aumid", "douyin.aumid", "--fingerprint", "fingerprint-v1"]);
        var pause = MediaSmokeCli.Parse(["pause", "--aumid", "douyin.aumid", "--fingerprint", "fingerprint-v1"]);

        Assert.False(play.IsValid);
        Assert.Equal(MediaSmokeCommand.Invalid, play.Command);
        Assert.False(pause.IsValid);
        Assert.Equal(MediaSmokeCommand.Invalid, pause.Command);
    }

    [Fact]
    public void Parse_cycle_test_requires_both_exact_identity_fields()
    {
        var valid = MediaSmokeCli.Parse(["cycle-test", "--aumid", "douyin.aumid", "--fingerprint", "fingerprint-v1"]);
        var invalid = MediaSmokeCli.Parse(["cycle-test", "--aumid", "douyin.aumid", "--fingerprint", ""]);

        Assert.True(valid.IsValid);
        Assert.Equal(MediaSmokeCommand.CycleTest, valid.Command);
        Assert.False(invalid.IsValid);
    }
}
