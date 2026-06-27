namespace Trimmer.Hosting;

/// <summary>
/// Coordinates browser live-reload. Renderers inject <see cref="ClientScript"/> into HTML
/// responses; the browser opens an SSE connection that this service pushes to whenever the
/// project changes on disk.
/// </summary>
public sealed class LiveReloadService
{
    public const string Endpoint = "/__trimmer/livereload";

    private volatile TaskCompletionSource _signal = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public string ClientScript { get; } =
        $$"""
        <script>
        (function () {
            if (!window.EventSource) { return; }
            var source = new EventSource('{{Endpoint}}');
            source.onmessage = function () { window.location.reload(); };
        })();
        </script>
        """;

    /// <summary>Resolves the next time the project changes.</summary>
    public Task WaitForChangeAsync(CancellationToken cancellationToken) =>
        _signal.Task.WaitAsync(cancellationToken);

    /// <summary>Signals all connected browsers to reload.</summary>
    public void NotifyChange()
    {
        var previous = Interlocked.Exchange(
            ref _signal,
            new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously));
        previous.TrySetResult();
    }

    /// <summary>Injects the live-reload script into an HTML document.</summary>
    public string InjectInto(string html)
    {
        const string bodyClose = "</body>";
        var index = html.LastIndexOf(bodyClose, StringComparison.OrdinalIgnoreCase);
        return index >= 0
            ? html[..index] + ClientScript + html[index..]
            : html + ClientScript;
    }
}
