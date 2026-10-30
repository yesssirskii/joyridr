namespace Joyridr.Models;

public class CarPhoto
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid CarId { get; set; }
    public Car Car { get; set; } = null!;

    /// <summary>Path relative to the API base URL, e.g. "/uploads/cars/{carId}/{file}.jpg".</summary>
    public string Url { get; set; } = null!;
    public int SortOrder { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
