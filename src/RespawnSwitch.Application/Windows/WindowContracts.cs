using RespawnSwitch.Core.Game;
using RespawnSwitch.Core.Respawn;

namespace RespawnSwitch.Application.Windows;

public readonly record struct NativeWindowHandle(nint Value)
{
    public bool IsNull => Value == 0;
}

public readonly record struct PixelRect(int Left, int Top, int Right, int Bottom)
{
    public int Width => Right - Left;
    public int Height => Bottom - Top;
}

public sealed record ProcessIdentity(int ProcessId, DateTimeOffset StartedAtUtc, string NormalizedExecutablePath, string SignatureSubject, string SignatureThumbprint);
public sealed record WindowIdentity(NativeWindowHandle Handle, int ProcessId, string WindowClass);
public sealed record GameWindowTarget(WindowIdentity Identity, PixelRect Bounds, string ProcessName, string TimelineKey, bool IsBorderless);
public sealed record DouyinWindowTarget(WindowIdentity Identity, ProcessIdentity Process, PixelRect Bounds);

public interface IGamePresenceProbe
{
    ValueTask<GamePresenceSnapshot> ProbeAsync(CancellationToken cancellationToken);
}

public interface ILeagueWindowController
{
    ValueTask<GameWindowTarget?> TryFindAsync(string timelineKey, CancellationToken cancellationToken);
    ValueTask<bool> TryRestoreFocusOnceAsync(GameWindowTarget target, CancellationToken cancellationToken);
    ValueTask FlashTaskbarAsync(GameWindowTarget target, CancellationToken cancellationToken);
}

public sealed record WindowAttachRequest(RespawnCycleId CycleId, GameWindowTarget GameWindow, PixelRect TargetWorkArea, string CalibratedExecutablePath, string CalibratedWindowClass, bool AllowLaunch);
public sealed record WindowOperationResult(bool RequestIssued, bool PostconditionVerified, NativeWindowHandle? Window, string FailureCode);

public interface IDouyinWindowController
{
    ValueTask<WindowOperationResult> AttachAsync(WindowAttachRequest request, CancellationToken cancellationToken);
    ValueTask<WindowOperationResult> RestoreAsync(RespawnCycleId cycleId, CancellationToken cancellationToken);
}
