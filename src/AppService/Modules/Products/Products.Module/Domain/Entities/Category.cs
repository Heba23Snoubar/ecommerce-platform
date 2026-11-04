using E.Domain.Common;
using E.Domain.Entites;

namespace E.Domain.Entities
{
     
    public class Category : BaseEntity
    {
         
        public string Name { get; private set; } = null!;
        
        public string? Description { get; private set; }

        public Guid? ParentId { get; private set; }

        public string? ImageUrl { get; private set; }

        public Category? ParentCategory { get; private set; }
          
        private Category()
        {
        }

        public Category(
          string name,
          string? description = null,
          string? imageUrl = null,
          Guid? parentId = null)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException(
                    "Category name is required.",
                    nameof(name));

            Name = name.Trim();
            Description = description?.Trim();
            ImageUrl = imageUrl?.Trim();
            ParentId = parentId;
        }


        public void ChangeDescription(string? description)
        {
          
            Description = description?.Trim();
            UpdatedAt = DateTime.UtcNow;
        }

        public void Rename(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException(
                    "Category name is required.",
                    nameof(name));

            Name = name.Trim();
            UpdatedAt = DateTime.UtcNow;
        }

        public void ChangeImage(string? imageUrl)
        {
            ImageUrl = imageUrl?.Trim();
            UpdatedAt = DateTime.UtcNow;
        }

        public void MoveToParent(Guid? parentId)
        {
            if (parentId == Id)
                throw new InvalidOperationException(
                    "A category cannot be its own parent.");

            ParentId = parentId;
            UpdatedAt = DateTime.UtcNow;
        }

    }


}