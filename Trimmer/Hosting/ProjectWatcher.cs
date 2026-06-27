namespace Trimmer.Hosting;

/// <summary>
/// Watches a project directory and invokes a callback (debounced) whenever a relevant
/// source or asset file changes, is created, deleted or renamed.
/// </summary>
public sealed class ProjectWatcher : IDisposable
{
    private readonly FileSystemWatcher _watcher;
    private readonly Action _onChanged;
    private readonly TimeSpan _debounce;
    private readonly Lock _gate = new();
    private Timer? _timer;

    public ProjectWatcher(string root, Action onChanged, TimeSpan? debounce = null)
    {
        _onChanged = onChanged;
        _debounce = debounce ?? TimeSpan.FromMilliseconds(150);
        _watcher = new FileSystemWatcher(root)
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.LastWrite
                | NotifyFilters.FileName
                | NotifyFilters.DirectoryName
                | NotifyFilters.Size
        };

        _watcher.Changed += OnEvent;
        _watcher.Created += OnEvent;
        _watcher.Deleted += OnEvent;
        _watcher.Renamed += OnEvent;
    }

    public void Start() => _watcher.EnableRaisingEvents = true;

    private void OnEvent(object sender, FileSystemEventArgs e)
    {
        if (ShouldIgnore(e.FullPath))
        {
            return;
        }

        lock (_gate)
        {
            _timer?.Dispose();
            _timer = new Timer(_ => _onChanged(), null, _debounce, Timeout.InfiniteTimeSpan);
        }
    }

    private static bool ShouldIgnore(string path)
    {
        var segments = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return segments.Any(s =>
            s.Equals("bin", StringComparison.OrdinalIgnoreCase)
            || s.Equals("obj", StringComparison.OrdinalIgnoreCase)
            || s.Equals(".git", StringComparison.OrdinalIgnoreCase)
            || s.Equals(".trimmer", StringComparison.OrdinalIgnoreCase));
    }

    public void Dispose()
    {
        _watcher.Dispose();
        lock (_gate)
        {
            _timer?.Dispose();
        }
    }
}
