using RespawnSwitch.Application.Douyin;

namespace RespawnSwitch.Windows.DouyinDiscovery;

public interface IDouyinCandidateValidator
{
    ValueTask<DouyinCandidate?> ValidateAsync(
        string path,
        DouyinDiscoverySource source,
        bool isRunning,
        CancellationToken cancellationToken);
}
