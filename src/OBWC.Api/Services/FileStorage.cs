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
        ".pdf"
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

        await using var input = file.OpenReadStream();
        using var buffer = new MemoryStream();
        await input.CopyToAsync(buffer, cancellationToken);
        if (buffer.Length <= 0 || buffer.Length > _maxBytes)
            throw new InvalidUploadException($"Files must be between 1 byte and {_maxBytes / 1024 / 1024} MB.");

        var bytes = buffer.ToArray();
        var contentType = RequireContentType(extension, bytes);
        var storedName = $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
        var directory = Path.Combine(_root, SanitizeFolder(folder));
        Directory.CreateDirectory(directory);
        var fullPath = Path.Combine(directory, storedName);
        await File.WriteAllBytesAsync(fullPath, bytes, cancellationToken);
        return new StoredFile($"{SanitizeFolder(folder)}/{storedName}", contentType, Path.GetFileName(file.FileName));
    }

    private static string SanitizeFolder(string folder)
    {
        if (string.IsNullOrWhiteSpace(folder) || folder.Contains("..", StringComparison.Ordinal) || folder.Contains('/', StringComparison.Ordinal) || folder.Contains('\\', StringComparison.Ordinal))
            throw new InvalidOperationException("Invalid upload folder.");
        return folder;
    }

    private static string RequireContentType(string extension, ReadOnlySpan<byte> bytes)
    {
        var ext = extension.ToLowerInvariant();
        var matches = ext switch
        {
            ".jpg" or ".jpeg" => bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF,
            ".png" => bytes.Length >= 8 && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47,
            ".gif" => bytes.Length >= 6 && bytes[0] == (byte)'G' && bytes[1] == (byte)'I' && bytes[2] == (byte)'F',
            ".webp" => bytes.Length >= 12
                && bytes[0] == (byte)'R' && bytes[1] == (byte)'I' && bytes[2] == (byte)'F' && bytes[3] == (byte)'F'
                && bytes[8] == (byte)'W' && bytes[9] == (byte)'E' && bytes[10] == (byte)'B' && bytes[11] == (byte)'P',
            ".pdf" => bytes.Length >= 5 && bytes[0] == (byte)'%' && bytes[1] == (byte)'P' && bytes[2] == (byte)'D' && bytes[3] == (byte)'F',
            _ => false
        };
        if (!matches)
            throw new InvalidUploadException("The file contents do not match the file type.");

        return ext switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".pdf" => "application/pdf",
            _ => "application/octet-stream"
        };
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
