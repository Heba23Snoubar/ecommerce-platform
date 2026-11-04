using E.Domain.Common;

namespace E.Domain.Entities;

public class Category : BaseEntity
{
    public string Name { get; private set; } = null!;

    public string? Description { get; private set; }

    public Guid? ParentId { get; private set; }

    public string? ImageUrl { get; private set; }

    public Category? ParentCategory { get; private set; }

    private Category() { }

    public Category(
        string name,
        string? description = null,
        string? imageUrl = null,
        Guid? parentId = null)
    {
        Name = NormalizeAndValidateName(name);
        Description = NormalizeDescription(description);
        ImageUrl = NormalizeImageUrl(imageUrl);

        if (parentId == Id)
            throw new InvalidOperationException(
                "A category cannot be its own parent.");

        ParentId = parentId;
    }

    public void Rename(string name)
    {
        var normalizedName = NormalizeAndValidateName(name);

        if (Name == normalizedName)
            return;

        Name = normalizedName;
        UpdatedAt = DateTime.UtcNow;
    }

    public void ChangeDescription(string? description)
    {
        var normalizedDescription = NormalizeDescription(description);

        if (Description == normalizedDescription)
            return;

        Description = normalizedDescription;
        UpdatedAt = DateTime.UtcNow;
    }

    public void ChangeImage(string? imageUrl)
    {
        var normalizedImageUrl = NormalizeImageUrl(imageUrl);

        if (ImageUrl == normalizedImageUrl)
            return;

        ImageUrl = normalizedImageUrl;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MoveToParent(Guid? parentId)
    {
        if (parentId == Id)
            throw new InvalidOperationException(
                "A category cannot be its own parent.");

        if (ParentId == parentId)
            return;

        ParentId = parentId;
        UpdatedAt = DateTime.UtcNow;
    }

    private static string NormalizeAndValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException(
                "Category name is required.", nameof(name));

        name = name.Trim();

        if (name.Length > 100)
            throw new ArgumentException(
                "Category name cannot exceed 100 characters.", nameof(name));

        return name;
    }

    private static string? NormalizeDescription(string? description)
    {
        description = description?.Trim();

        if (description?.Length > 1000)
            throw new ArgumentException(
                "Description cannot exceed 1000 characters.", nameof(description));

        return string.IsNullOrEmpty(description) ? null : description;
    }

    private static string? NormalizeImageUrl(string? imageUrl)
    {
        imageUrl = imageUrl?.Trim();

        if (imageUrl?.Length > 500)
            throw new ArgumentException(
                "Image URL cannot exceed 500 characters.", nameof(imageUrl));

        return string.IsNullOrEmpty(imageUrl) ? null : imageUrl;
    }
}