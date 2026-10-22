namespace Joyridr.Common;

public interface IFileStorage
{
    /// <summary>Saves the file and returns its public path, e.g. "/uploads/cars/123/abc.jpg".</summary>
    Task<string> SaveAsync(string folder, IFormFile file, CancellationToken ct);
    Task DeleteAsync(string publicPath, CancellationToken ct);
}

/// <summary>
/// Stores uploads on local disk under wwwroot/uploads, served as static files.
/// Fine for development; swap for S3/Azure Blob/Cloudflare R2 in production.
/// </summary>
public class LocalFileStorage(IWebHostEnvironment env) : IFileStorage
{
    public static readonly string[] AllowedImageTypes = ["image/jpeg", "image/png", "image/heic", "image/webp"];
    public const long MaxImageBytes = 10 * 1024 * 1024;

    private string Root => Path.Combine(env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot"), "uploads");

    public async Task<string> SaveAsync(string folder, IFormFile file, CancellationToken ct)
    {
        var extension = file.ContentType switch
        {
            "image/jpeg" => ".jpg",
            "image/png" => ".png",
            "image/heic" => ".heic",
            "image/webp" => ".webp",
            _ => throw ApiException.BadRequest("Only JPEG, PNG, HEIC and WebP images are allowed.", "invalid_file_type"),
        };
        if (file.Length is 0 or > MaxImageBytes)
            throw ApiException.BadRequest("Images must be between 1 byte and 10 MB.", "invalid_file_size");

        var directory = Path.Combine(Root, folder);
        Directory.CreateDirectory(directory);
        var fileName = $"{Guid.CreateVersion7()}{extension}";

        await using (var stream = File.Create(Path.Combine(directory, fileName)))
            await file.CopyToAsync(stream, ct);

        return $"/uploads/{folder.Replace('\\', '/')}/{fileName}";
    }

    public Task DeleteAsync(string publicPath, CancellationToken ct)
    {
        if (!publicPath.StartsWith("/uploads/", StringComparison.Ordinal) || publicPath.Contains(".."))
            return Task.CompletedTask;

        var fullPath = Path.Combine(Root, publicPath["/uploads/".Length..]);
        if (File.Exists(fullPath))
            File.Delete(fullPath);
        return Task.CompletedTask;
    }
}
