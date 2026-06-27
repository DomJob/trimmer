using Trimmer.Packages;

namespace Trimmer.Tests;

/// <summary>A resolver that never returns any assemblies, for offline/fast tests.</summary>
public sealed class FakePackageResolver : IPackageResolver
{
    public Task<IReadOnlyList<string>> ResolveAsync(
        IReadOnlyList<PackageReference> packages,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<string>>([]);
}
