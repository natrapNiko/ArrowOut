using System.Text.RegularExpressions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace ArrowOut.Services.Storage;

// Simple file storage. It's the local disk for now, but could be swapped for Azure Blob Storage.
public interface IFileStorage
{
    Task<IReadOnlyList<string>> ListAsync(string container, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(string container, string fileName, CancellationToken cancellationToken = default);

    Task<string?> ReadTextAsync(string container, string fileName, CancellationToken cancellationToken = default);

    Task WriteTextAsync(string container, string fileName, string content, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(string container, string fileName, CancellationToken cancellationToken = default);
}

public sealed class FileStorageOptions
{
    public const string SectionName = "FileStorage";

    // Relative paths start from the content root. Don't put this inside wwwroot.
    public string RootPath { get; set; } = "App_Data/storage";
}

public sealed partial class LocalFileStorage : IFileStorage
{
    private readonly string _root;

    public LocalFileStorage(IOptions<FileStorageOptions> options, IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(environment);

        var configured = options.Value.RootPath;
        _root = Path.GetFullPath(Path.IsPathRooted(configured)
            ? configured
            : Path.Combine(environment.ContentRootPath, configured));
    }

    internal LocalFileStorage(string absoluteRoot)
    {
        _root = Path.GetFullPath(absoluteRoot);
    }

    public Task<IReadOnlyList<string>> ListAsync(string container, CancellationToken cancellationToken = default)
    {
        var directory = ResolveContainer(container);
        IReadOnlyList<string> files = Directory.Exists(directory)
            ? Directory.EnumerateFiles(directory).Select(Path.GetFileName).OfType<string>().Order(StringComparer.Ordinal).ToList()
            : [];

        return Task.FromResult(files);
    }

    public Task<bool> ExistsAsync(string container, string fileName, CancellationToken cancellationToken = default) =>
        Task.FromResult(File.Exists(ResolveFile(container, fileName)));

    public async Task<string?> ReadTextAsync(string container, string fileName, CancellationToken cancellationToken = default)
    {
        var path = ResolveFile(container, fileName);
        return File.Exists(path) ? await File.ReadAllTextAsync(path, cancellationToken) : null;
    }

    public async Task WriteTextAsync(string container, string fileName, string content, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        var path = ResolveFile(container, fileName);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        // Write to a temp file first and then move it, so nobody ever reads a half-written file.
        var temp = path + ".tmp";
        await File.WriteAllTextAsync(temp, content, cancellationToken);
        File.Move(temp, path, overwrite: true);
    }

    public Task<bool> DeleteAsync(string container, string fileName, CancellationToken cancellationToken = default)
    {
        var path = ResolveFile(container, fileName);
        if (!File.Exists(path))
        {
            return Task.FromResult(false);
        }

        File.Delete(path);
        return Task.FromResult(true);
    }

    private string ResolveContainer(string container)
    {
        EnsureSafeName(container, nameof(container));
        return EnsureWithinRoot(Path.Combine(_root, container));
    }

    private string ResolveFile(string container, string fileName)
    {
        EnsureSafeName(fileName, nameof(fileName));
        return EnsureWithinRoot(Path.Combine(ResolveContainer(container), fileName));
    }

    // Only safe characters, so no "..", slashes, drive letters or control characters.
    private static void EnsureSafeName(string name, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(name) || !SafeNameRegex().IsMatch(name) || name.Contains("..", StringComparison.Ordinal))
        {
            throw new ArgumentException($"'{name}' is not a permitted storage name.", parameterName);
        }
    }

    private string EnsureWithinRoot(string path)
    {
        var full = Path.GetFullPath(path);
        var rootWithSeparator = _root.EndsWith(Path.DirectorySeparatorChar) ? _root : _root + Path.DirectorySeparatorChar;

        return full.StartsWith(rootWithSeparator, StringComparison.Ordinal)
            ? full
            : throw new UnauthorizedAccessException("Path escapes the storage root.");
    }

    [GeneratedRegex("^[a-z0-9][a-z0-9_.-]{0,80}$", RegexOptions.CultureInvariant)]
    private static partial Regex SafeNameRegex();
}
