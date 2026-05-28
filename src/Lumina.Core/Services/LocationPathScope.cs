using Lumina.Core.Models;

namespace Lumina.Core.Services;

public sealed class LocationPathScope
{
    public LocationPathScope(string rootPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);

        RootPath = NormalizePath(rootPath);
    }

    public string RootPath { get; }

    public bool ContainsPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        try
        {
            return ContainsNormalizedPath(NormalizePath(path));
        }
        catch (Exception)
        {
            return false;
        }
    }

    public string NormalizeContainedPath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var normalizedPath = NormalizePath(path);
        if (!ContainsNormalizedPath(normalizedPath))
        {
            throw new UnauthorizedAccessException(
                $"Path is outside the current location root: {normalizedPath}");
        }

        return normalizedPath;
    }

    public bool TryNormalizeContainedPath(
        string path,
        out string normalizedPath)
    {
        normalizedPath = string.Empty;
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        try
        {
            normalizedPath = NormalizeContainedPath(path);
            return true;
        }
        catch (Exception)
        {
            normalizedPath = string.Empty;
            return false;
        }
    }

    public bool TryGetParentPath(
        string path,
        out string parentPath)
    {
        parentPath = string.Empty;
        if (!TryNormalizeContainedPath(path, out var normalizedPath) ||
            IsSameNormalizedPath(normalizedPath, RootPath))
        {
            return false;
        }

        var parent = Directory.GetParent(normalizedPath);
        if (parent is null ||
            !TryNormalizeContainedPath(parent.FullName, out parentPath))
        {
            parentPath = string.Empty;
            return false;
        }

        return true;
    }

    public string GetRelativePath(string path)
    {
        var normalizedPath = NormalizeContainedPath(path);
        var relativePath = Path.GetRelativePath(RootPath, normalizedPath);

        return string.IsNullOrWhiteSpace(relativePath) ? "." : relativePath;
    }

    public string GetDisplayPath(
        string path,
        string rootName)
    {
        var displayRootName = ResolveRootDisplayName(rootName);
        var relativePath = GetRelativePath(path);

        return relativePath == "."
            ? displayRootName
            : Path.Combine(displayRootName, relativePath);
    }

    public IReadOnlyList<LocationPathSegment> GetBreadcrumbs(
        string path,
        string rootName)
    {
        var normalizedPath = NormalizeContainedPath(path);
        var items = new List<LocationPathSegment>
        {
            new(ResolveRootDisplayName(rootName), RootPath),
        };

        var relativePath = Path.GetRelativePath(RootPath, normalizedPath);
        if (string.IsNullOrWhiteSpace(relativePath) || relativePath == ".")
        {
            return items;
        }

        var currentPath = RootPath;
        foreach (var segment in relativePath.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries))
        {
            currentPath = Path.Combine(currentPath, segment);
            items.Add(new LocationPathSegment(segment, currentPath));
        }

        return items;
    }

    private string ResolveRootDisplayName(string rootName)
    {
        var trimmedName = rootName.Trim();
        if (!string.IsNullOrWhiteSpace(trimmedName))
        {
            return trimmedName;
        }

        var directoryName = Path.GetFileName(Path.TrimEndingDirectorySeparator(RootPath));
        if (!string.IsNullOrWhiteSpace(directoryName))
        {
            return directoryName;
        }

        return RootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private bool ContainsNormalizedPath(string normalizedPath)
    {
        if (IsSameNormalizedPath(normalizedPath, RootPath))
        {
            return true;
        }

        return EnsureTrailingDirectorySeparator(TrimEndingDirectorySeparator(normalizedPath))
            .StartsWith(
                EnsureTrailingDirectorySeparator(TrimEndingDirectorySeparator(RootPath)),
                StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSameNormalizedPath(
        string left,
        string right)
    {
        return string.Equals(
            TrimEndingDirectorySeparator(left),
            TrimEndingDirectorySeparator(right),
            StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizePath(string path)
    {
        return Path.GetFullPath(path.Trim());
    }

    private static string TrimEndingDirectorySeparator(string path)
    {
        return Path.TrimEndingDirectorySeparator(path);
    }

    private static string EnsureTrailingDirectorySeparator(string path)
    {
        return Path.EndsInDirectorySeparator(path)
            ? path
            : path + Path.DirectorySeparatorChar;
    }
}
