namespace Trimmer.Hosting;

/// <summary>
/// Coordinates browser live-reload. Renderers inject the client script into HTML responses;
/// the browser opens an SSE connection (tagged with the page it is showing) that this service
/// pushes to whenever a change relevant to that specific page lands on disk.
/// </summary>
public sealed class LiveReloadService
{
    public const string Endpoint = "/__trimmer/livereload";

    /// <summary>Query-string key the browser uses to announce which page it is viewing.</summary>
    public const string PageQueryKey = "page";

    private readonly Lock _gate = new();
    private readonly List<Connection> _connections = [];

    /// <summary>
    /// Registers a browser connection for the page identified by <paramref name="pageKey"/>.
    /// Dispose the returned token when the connection ends.
    /// </summary>
    public ConnectionToken Connect(string? pageKey)
    {
        var connection = new Connection(pageKey ?? string.Empty);
        lock (_gate)
        {
            _connections.Add(connection);
        }

        return new ConnectionToken(this, connection);
    }

    /// <summary>
    /// Signals every connection whose page is affected by the change, as decided by
    /// <paramref name="affectsPage"/> (invoked with each connection's page key).
    /// </summary>
    public void NotifyChange(Func<string, bool> affectsPage)
    {
        Connection[] snapshot;
        lock (_gate)
        {
            snapshot = [.. _connections];
        }

        foreach (var connection in snapshot)
        {
            if (affectsPage(connection.PageKey))
            {
                connection.Trigger();
            }
        }
    }

    /// <summary>Injects the live-reload script, tagged with the page key, into an HTML document.</summary>
    public string InjectInto(string html, string? pageKey = null)
    {
        var script = BuildClientScript(pageKey);
        const string bodyClose = "</body>";
        var index = html.LastIndexOf(bodyClose, StringComparison.OrdinalIgnoreCase);
        return index >= 0
            ? html[..index] + script + html[index..]
            : html + script;
    }

    private static string BuildClientScript(string? pageKey)
    {
        // The page key is appended (URL-encoded) so the server can tell which page each
        // SSE connection belongs to and only reload the ones that actually changed.
        var url = pageKey is { Length: > 0 }
            ? $"{Endpoint}?{PageQueryKey}=' + encodeURIComponent('{JsEscape(pageKey)}') + '"
            : Endpoint;

        return $$"""
                 <script>
                 (function () {
                     if (!window.EventSource) { return; }
                     var source = new EventSource('{{url}}');
                     source.onmessage = function () { window.location.reload(); };
                 })();
                 </script>
                 """;
    }

    private static string JsEscape(string value) =>
        value.Replace("\\", "\\\\").Replace("'", "\\'");

    private void Remove(Connection connection)
    {
        lock (_gate)
        {
            _connections.Remove(connection);
        }
    }

    internal sealed class Connection(string pageKey)
    {
        private TaskCompletionSource _signal = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public string PageKey { get; } = pageKey;

        public Task WaitForChangeAsync(CancellationToken cancellationToken) =>
            Volatile.Read(ref _signal).Task.WaitAsync(cancellationToken);

        public void Trigger()
        {
            var previous = Interlocked.Exchange(
                ref _signal,
                new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously));
            previous.TrySetResult();
        }
    }

    /// <summary>Handle to a registered browser connection; dispose to unregister.</summary>
    public sealed class ConnectionToken : IDisposable
    {
        private readonly LiveReloadService _service;
        private readonly Connection _connection;

        internal ConnectionToken(LiveReloadService service, Connection connection)
        {
            _service = service;
            _connection = connection;
        }

        /// <summary>Resolves when a change relevant to this connection's page occurs.</summary>
        public Task WaitForChangeAsync(CancellationToken cancellationToken) =>
            _connection.WaitForChangeAsync(cancellationToken);

        public void Dispose() => _service.Remove(_connection);
    }
}
