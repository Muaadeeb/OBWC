using OBWC.Api.Options;

namespace OBWC.Api.Services;

public sealed class InvalidUploadException(string message) : Exception(message);

public readonly record struct StoredFile(string RelativePath, string ContentType, string OriginalFileName);

public sealed class FileStorage
{
    public static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".gif", ".webp"
    };

    public static readonly HashSet<string> DocumentExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".doc", ".docx", ".txt", ".rtf"
    };

    private readonly string _root;
    private readonly long _maxBytes;

    public FileStorage(IWebHostEnvironment environment, IOptions<UploadOptions> options)
    {
        var configured = string.IsNullOrWhiteSpace(options.Value.Path) ? "App_Data/uploads" : options.Value.Path;
        _root = Path.IsPathRooted(configured)
            ? configured
            : Path.Combine(environment.ContentRootPath, configured);
        Directory.CreateDirectory(_root);
        _maxBytes = options.Value.MaxUploadBytes <= 0 ? 10 * 1024 * 1024 : options.Value.MaxUploadBytes;
    }

    public async Task<StoredFile> SaveAsync(
        IFormFile file,
        string folder,
        IReadOnlySet<string> allowedExtensions,
        CancellationToken cancellationToken)
    {
        if (file.Length <= 0 || file.Length > _maxBytes)
            throw new InvalidUploadException($"Files must be between 1 byte and {_maxBytes / 1024 / 1024} MB.");

        var extension = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(extension) || !allowedExtensions.Contains(extension))
            throw new InvalidUploadException($"Allowed file types: {string.Join(", ", allowedExtensions.OrderBy(item => item))}.");

        var storedName = $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
        var directory = Path.Combine(_root, folder);
        Directory.CreateDirectory(directory);
        var fullPath = Path.Combine(directory, storedName);
        await using (var stream = File.Create(fullPath))
        {
            await file.CopyToAsync(stream, cancellationToken);
        }

        var contentType = string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType;
        return new StoredFile($"{folder}/{storedName}", contentType, Path.GetFileName(file.FileName));
    }

    public Stream Open(string relativePath) => File.OpenRead(Resolve(relativePath));

    public void Delete(string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
            return;
        var fullPath = Resolve(relativePath);
        if (File.Exists(fullPath))
            File.Delete(fullPath);
    }

    private string Resolve(string relativePath)
    {
        if (relativePath.Contains("..", StringComparison.Ordinal))
            throw new InvalidOperationException("Invalid upload path.");

        var combined = Path.GetFullPath(Path.Combine(_root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        var root = Path.GetFullPath(_root);
        var rootPrefix = root.EndsWith(Path.DirectorySeparatorChar) ? root : root + Path.DirectorySeparatorChar;
        if (!combined.StartsWith(rootPrefix, StringComparison.Ordinal))
            throw new InvalidOperationException("Invalid upload path.");
        return combined;
    }
}
