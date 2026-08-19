// Author: Stress Monster
namespace RespawnSwitch.Windows.Media;

internal interface IMediaVerificationDelay
{
    ValueTask WaitAsync(TimeSpan delay, CancellationToken cancellationToken);
}

internal sealed class SystemMediaVerificationDelay : IMediaVerificationDelay
{
    public async ValueTask WaitAsync(TimeSpan delay, CancellationToken cancellationToken) =>
        await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
}
