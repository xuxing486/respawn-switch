using RespawnSwitch.Application.Media;

namespace RespawnSwitch.Windows.Media;

public sealed class MediaControllerFactory : IMediaControllerFactory
{
    public async ValueTask<IDouyinMediaController?> CreateAsync(
        IReadOnlyList<MediaControlProfile> profilesInPriorityOrder,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(profilesInPriorityOrder);

        foreach (var profile in profilesInPriorityOrder)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (profile is not GsmtcMediaProfile gsmtcProfile)
            {
                continue;
            }

            var controller = new GsmtcDouyinMediaController(gsmtcProfile);
            if ((await controller.ProbeAsync(cancellationToken)).IsUsable)
            {
                return controller;
            }
        }

        return null;
    }
}
