namespace RespawnSwitch.App.Browser;

public sealed record BrowserCommand(long Sequence, string Command);
public sealed record BrowserCommandResult(long Sequence, bool Ok, string State, string Browser, int TabCount, string ErrorCode);

public sealed class BrowserBridgeState
{
    private readonly object gate = new();
    private long sequence;
    private BrowserCommand? latest;
    private TaskCompletionSource<BrowserCommandResult>? pending;

    public Task<BrowserCommandResult> IssueAsync(string command, TimeSpan timeout, CancellationToken cancellationToken)
    {
        TaskCompletionSource<BrowserCommandResult> completion;
        lock (gate)
        {
            latest = new BrowserCommand(++sequence, command);
            completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
            pending?.TrySetResult(new(sequence - 1, false, "superseded", "", 0, "superseded"));
            pending = completion;
        }
        return WaitAsync(completion.Task, timeout, cancellationToken);
    }

    public BrowserCommand? ReadAfter(long lastSequence)
    {
        lock (gate) return latest is { } command && command.Sequence > lastSequence ? command : null;
    }

    public void Publish(BrowserCommandResult result)
    {
        lock (gate)
        {
            if (latest?.Sequence != result.Sequence) return;
            pending?.TrySetResult(result);
            pending = null;
        }
    }

    private static async Task<BrowserCommandResult> WaitAsync(Task<BrowserCommandResult> task, TimeSpan timeout, CancellationToken token)
    {
        try { return await task.WaitAsync(timeout, token); }
        catch (TimeoutException) { return new(0, false, "timeout", "", 0, "extension-timeout"); }
    }
}
