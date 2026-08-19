namespace RespawnSwitch.App.Browser;

public sealed record BrowserCommand(Guid CycleId, long Sequence, string Command);
public sealed record BrowserCommandResult(Guid CycleId, long Sequence, bool Ok, string State, string Browser, int TabCount, string ErrorCode);

public sealed class BrowserBridgeState
{
    private readonly object gate = new();
    private long sequence;
    private BrowserCommand? latest;
    private TaskCompletionSource<BrowserCommandResult>? pending;

    public Task<BrowserCommandResult> IssueAsync(Guid cycleId, string command, TimeSpan timeout, CancellationToken cancellationToken)
    {
        TaskCompletionSource<BrowserCommandResult> completion;
        long issuedSequence;
        lock (gate)
        {
            var previous = latest;
            issuedSequence = ++sequence;
            latest = new BrowserCommand(cycleId, issuedSequence, command);
            completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
            if (previous is not null)
                pending?.TrySetResult(new(previous.CycleId, previous.Sequence, false, "superseded", "", 0, "superseded"));
            pending = completion;
        }
        return WaitAsync(cycleId, issuedSequence, completion.Task, timeout, cancellationToken);
    }

    public BrowserCommand? ReadAfter(long lastSequence)
    {
        lock (gate) return latest is { } command && command.Sequence > lastSequence ? command : null;
    }

    public void Publish(BrowserCommandResult result)
    {
        lock (gate)
        {
            if (latest?.CycleId != result.CycleId || latest.Sequence != result.Sequence) return;
            pending?.TrySetResult(result);
            pending = null;
        }
    }

    private static async Task<BrowserCommandResult> WaitAsync(Guid cycleId, long sequence, Task<BrowserCommandResult> task, TimeSpan timeout, CancellationToken token)
    {
        try { return await task.WaitAsync(timeout, token); }
        catch (TimeoutException) { return new(cycleId, sequence, false, "timeout", "", 0, "extension-timeout"); }
    }
}
