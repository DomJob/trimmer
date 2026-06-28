namespace Trimmer.Hosting;

/// <summary>
/// Watches a project directory and invokes a callback (debounced) whenever a relevant
/// source or asset file changes, is created, deleted or renamed.
/// </summary>
public sealed class ProjectWatcher : IDisposable
{
    private readonly FileSystemWatcher _watcher;
    private readonly Action<IReadOnlyCollection<string>> _onChanged;
    private readonly TimeSpan _debounce;
    private readonly Lock _gate = new();
    private readonly HashSet<string> _pending = new(StringComparer.OrdinalIgnoreCase);
    private Timer? _timer;

    public ProjectWatcher(string root, Action<IReadOnlyCollection<string>> onChanged, TimeSpan? debounce = null)
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
        _watcher.Renamed += OnRenamed;
    }

    public void Start() => _watcher.EnableRaisingEvents = true;

    private void OnRenamed(object sender, RenamedEventArgs e)
    {
        Track(e.OldFullPath);
        Track(e.FullPath);
    }

    private void OnEvent(object sender, FileSystemEventArgs e) => Track(e.FullPath);

    private void Track(string fullPath)
    {
        if (ShouldIgnore(fullPath))
        {
            return;
        }

        lock (_gate)
        {
            _pending.Add(fullPath);
            _timer?.Dispose();
            _timer = new Timer(_ => Flush(), null, _debounce, Timeout.InfiniteTimeSpan);
        }
    }

    private void Flush()
    {
        string[] changed;
        lock (_gate)
        {
            changed = [.. _pending];
            _pending.Clear();
        }

        if (changed.Length > 0)
        {
            _onChanged(changed);
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
