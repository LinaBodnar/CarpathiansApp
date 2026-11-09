using Carpathians.Domain.Exceptions;

namespace Carpathians.Domain.Entities;

public class GalleryImage
{
    public long Id { get; private set; }
    public long PointId { get; private set; }
    public string FileName { get; private set; } = string.Empty;
    public string Url { get; private set; } = string.Empty;
    public string? Caption { get; private set; }
    public int SortOrder { get; private set; }
    public DateTime UploadedAt { get; private set; }

    private GalleryImage() { }

    internal GalleryImage(string fileName, string url, string? caption, int sortOrder)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            throw new DomainException("File name is required");
        if (string.IsNullOrWhiteSpace(url))
            throw new DomainException("Image URL is required");
        if (sortOrder < 0)
            throw new DomainException("Sort order cannot be negative");

        FileName = fileName.Trim();
        Url = url.Trim();
        Caption = caption?.Trim();
        SortOrder = sortOrder;
    }

    public void Update(string? caption, int sortOrder)
    {
        if (sortOrder < 0)
            throw new DomainException("Sort order cannot be negative");

        Caption = caption?.Trim();
        SortOrder = sortOrder;
    }
}
