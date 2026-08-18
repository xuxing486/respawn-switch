using System.Net;
using System.Text;
using System.Text.Json;
using System.IO;

namespace RespawnSwitch.App.Browser;

public sealed class BrowserBridgeServer : IAsyncDisposable
{
    private readonly BrowserBridgeState state;
    private readonly string statusPath;
    private readonly HttpListener listener = new();
    private readonly CancellationTokenSource shutdown = new();
    private Task? loop;

    public BrowserBridgeServer(BrowserBridgeState state, string statusPath)
    {
        this.state = state;
        this.statusPath = statusPath;
        listener.Prefixes.Add("http://127.0.0.1:17653/respawnswitch/");
    }

    public void Start()
    {
        if (loop is not null) return;
        listener.Start();
        loop = Task.Run(RunAsync);
    }

    private async Task RunAsync()
    {
        while (!shutdown.IsCancellationRequested)
        {
            HttpListenerContext context;
            try { context = await listener.GetContextAsync().WaitAsync(shutdown.Token); }
            catch (OperationCanceledException) { break; }
            catch (HttpListenerException) when (shutdown.IsCancellationRequested) { break; }
            _ = HandleAsync(context);
        }
    }

    private async Task HandleAsync(HttpListenerContext context)
    {
        try
        {
            AddCors(context.Response);
            if (context.Request.HttpMethod == "OPTIONS") { context.Response.StatusCode = 204; return; }
            var path = context.Request.Url?.AbsolutePath;
            if (context.Request.HttpMethod == "GET" && path?.EndsWith("/command", StringComparison.Ordinal) == true)
            {
                _ = long.TryParse(context.Request.QueryString["after"], out var after);
                await WriteJsonAsync(context.Response, state.ReadAfter(after));
                return;
            }
            if (context.Request.HttpMethod == "POST" && path?.EndsWith("/status", StringComparison.Ordinal) == true)
            {
                var result = await JsonSerializer.DeserializeAsync<BrowserCommandResult>(context.Request.InputStream, JsonOptions);
                if (result is null) { context.Response.StatusCode = 400; return; }
                state.Publish(result);
                Directory.CreateDirectory(Path.GetDirectoryName(statusPath)!);
                var status = new { ready = result.Ok, result.Browser, result.TabCount, result.State, result.ErrorCode, updatedAtUtc = DateTimeOffset.UtcNow };
                await File.WriteAllTextAsync(statusPath, JsonSerializer.Serialize(status, JsonOptions));
                await WriteJsonAsync(context.Response, new { ok = true });
                return;
            }
            context.Response.StatusCode = 404;
        }
        catch { context.Response.StatusCode = 500; }
        finally { context.Response.Close(); }
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static void AddCors(HttpListenerResponse response)
    {
        response.Headers["Access-Control-Allow-Origin"] = "*";
        response.Headers["Access-Control-Allow-Headers"] = "content-type";
        response.Headers["Access-Control-Allow-Methods"] = "GET, POST, OPTIONS";
    }
    private static async Task WriteJsonAsync(HttpListenerResponse response, object? value)
    {
        response.ContentType = "application/json";
        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value, JsonOptions));
        response.ContentLength64 = bytes.Length;
        await response.OutputStream.WriteAsync(bytes);
    }

    public async ValueTask DisposeAsync()
    {
        shutdown.Cancel();
        listener.Close();
        if (loop is not null) try { await loop; } catch (OperationCanceledException) { }
        shutdown.Dispose();
    }
}
