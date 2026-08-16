using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Interop;

namespace RespawnSwitch.TestWindowHost;

public partial class TestWindow : Window
{
    private readonly HostOptions options;

    public TestWindow(HostOptions options)
    {
        this.options = options;
        InitializeComponent();
        ContentRendered += (_, _) =>
        {
            ApplyMode();
            WriteReadyFile();
        };
    }

    private void ApplyMode()
    {
        switch (options.Mode.ToLowerInvariant())
        {
            case "hidden": Hide(); break;
            case "minimized": WindowState = WindowState.Minimized; break;
            case "maximized": WindowState = WindowState.Maximized; break;
            case "topmost":
            case "topmost-peer": Topmost = true; break;
            case "recreate": WatchForRecreate(); break;
            case "hung-uia": WatchForHungProviderRelease(); break;
        }
    }

    private void WriteReadyFile()
    {
        var source = (HwndSource?)PresentationSource.FromVisual(this);
        if (source is null) return;
        Directory.CreateDirectory(Path.GetDirectoryName(options.ReadyFile)!);
        File.WriteAllText(options.ReadyFile, JsonSerializer.Serialize(new { Pid = Environment.ProcessId, Hwnd = source.Handle.ToInt64(), Class = GetType().FullName, Bounds = new { Left, Top, Width, Height }, Mode = options.Mode, Ready = true }) + Environment.NewLine);
    }

    private void WatchForRecreate()
    {
        if (string.IsNullOrWhiteSpace(options.RecreateEvent)) return;
        _ = Task.Run(() =>
        {
            using var signal = new EventWaitHandle(false, EventResetMode.ManualReset, options.RecreateEvent);
            signal.WaitOne();
            Dispatcher.Invoke(() => { var replacement = new TestWindow(options); Application.Current.MainWindow = replacement; replacement.Show(); Close(); });
        });
    }

    private void WatchForHungProviderRelease()
    {
        if (string.IsNullOrWhiteSpace(options.HungEvent)) return;
        _ = Task.Run(() => { using var release = new EventWaitHandle(false, EventResetMode.ManualReset, options.HungEvent); release.WaitOne(); });
    }
}
