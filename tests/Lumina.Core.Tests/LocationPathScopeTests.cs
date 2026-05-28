using Lumina.Core.Services;

namespace Lumina.Core.Tests;

public sealed class LocationPathScopeTests : IDisposable
{
    private readonly string _temporaryDirectory = CreateTemporaryDirectory();

    [Fact]
    public void NormalizeContainedPath_AllowsRootAndDescendants()
    {
        var scope = new LocationPathScope(_temporaryDirectory);
        var childPath = Directory.CreateDirectory(Path.Combine(_temporaryDirectory, "Child")).FullName;

        Assert.Equal(Path.GetFullPath(_temporaryDirectory), scope.NormalizeContainedPath(_temporaryDirectory));
        Assert.Equal(childPath, scope.NormalizeContainedPath(Path.Combine(_temporaryDirectory, ".", "Child")));
    }

    [Fact]
    public void NormalizeContainedPath_RejectsParentAndPrefixSibling()
    {
        var parentDirectory = Directory.GetParent(_temporaryDirectory)?.FullName
            ?? throw new InvalidOperationException("Temporary directory has no parent.");
        var siblingPrefixPath = _temporaryDirectory + "-Sibling";
        var scope = new LocationPathScope(_temporaryDirectory);

        Assert.Throws<UnauthorizedAccessException>(() => scope.NormalizeContainedPath(parentDirectory));
        Assert.Throws<UnauthorizedAccessException>(() => scope.NormalizeContainedPath(siblingPrefixPath));
    }

    [Fact]
    public void TryGetParentPath_StopsAtLocationRoot()
    {
        var childPath = Directory.CreateDirectory(Path.Combine(_temporaryDirectory, "Child")).FullName;
        var nestedPath = Directory.CreateDirectory(Path.Combine(childPath, "Nested")).FullName;
        var scope = new LocationPathScope(_temporaryDirectory);

        Assert.True(scope.TryGetParentPath(nestedPath, out var parentPath));
        Assert.Equal(childPath, parentPath);
        Assert.False(scope.TryGetParentPath(_temporaryDirectory, out _));
    }

    [Fact]
    public void GetBreadcrumbs_StartsAtLocationRootName()
    {
        var childPath = Directory.CreateDirectory(Path.Combine(_temporaryDirectory, "Child")).FullName;
        var nestedPath = Directory.CreateDirectory(Path.Combine(childPath, "Nested")).FullName;
        var scope = new LocationPathScope(_temporaryDirectory);

        var breadcrumbs = scope.GetBreadcrumbs(nestedPath, "Projects");

        Assert.Equal(["Projects", "Child", "Nested"], breadcrumbs.Select(item => item.Name));
        Assert.Equal(
            [_temporaryDirectory, childPath, nestedPath],
            breadcrumbs.Select(item => item.Path));
    }

    [Fact]
    public void GetDisplayPath_UsesLocationRootAsTopLevel()
    {
        var nestedPath = Directory.CreateDirectory(Path.Combine(_temporaryDirectory, "Child", "Nested")).FullName;
        var scope = new LocationPathScope(_temporaryDirectory);

        Assert.Equal("Projects", scope.GetDisplayPath(_temporaryDirectory, "Projects"));
        Assert.Equal(
            Path.Combine("Projects", "Child", "Nested"),
            scope.GetDisplayPath(nestedPath, "Projects"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_temporaryDirectory))
        {
            Directory.Delete(_temporaryDirectory, recursive: true);
        }
    }

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"Lumina-LocationPathScope-Tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);

        return path;
    }
}
