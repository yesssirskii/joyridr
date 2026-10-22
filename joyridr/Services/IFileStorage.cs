namespace Joyridr.Services;

public interface IFileStorage
{
    /// <summary>Saves the file and returns its public path, e.g. "/uploads/cars/123/abc.jpg".</summary>
    Task<string> SaveAsync(string folder, IFormFile file, CancellationToken ct);
    Task DeleteAsync(string publicPath, CancellationToken ct);
}
