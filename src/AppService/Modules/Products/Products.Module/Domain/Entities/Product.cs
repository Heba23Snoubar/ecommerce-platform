
using E.Domain.Common;
using E.Domain.Entities;
using System.ComponentModel.Design;
using System.Text.Json;
using UUIDNext;

namespace E.Domain.Entites
{

    public class Product :BaseEntity
    {
         
        public string Name { get; private set; } = null!;

        public string? Description { get; private set; }

        public decimal Price { get; private set; }

        public Guid CategoryId { get; private set; }

        public Category Category { get; private set; } = null!;
         
        public bool IsAvailable { get; private set; }  

         

        private List<ProductImage> _images = new();

        public IReadOnlyCollection<ProductImage> Images => _images.AsReadOnly();
         
         
         

        private Product()
        {
        }

        public Product(
            string name,
            decimal price,
            Guid categoryId,
            string? description = null
            )
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Product name is required.");

            if (price <= 0)
                throw new ArgumentException("Price must be greater than zero.");

            Name = name;
            Price = price;
            CategoryId = categoryId;
            Description = description;
            IsAvailable = true;
        }

        public void ChangePrice(decimal newPrice)
        {
            if (newPrice <= 0)
                throw new ArgumentOutOfRangeException(
                    nameof(newPrice),
                    "Product price must be greater than zero.");

            Price = newPrice;
            UpdatedAt = DateTime.UtcNow;
        }
        public void Disable()
        {
            IsAvailable = false;
            UpdatedAt = DateTime.UtcNow;
        }

        public void Enable()
        {
            IsAvailable = true;
            UpdatedAt = DateTime.UtcNow;
        }

        public void AddImage(string imageUrl, bool isMain = false)
        {
            if (string.IsNullOrWhiteSpace(imageUrl))
                throw new ArgumentException("Image URL is required.");


            if (!_images.Any())
            {
                isMain = true;
            }
            else if (isMain)
            {
                foreach (var img in _images)
                    img.RemoveAsMain();
            }

             

            var image = new ProductImage(imageUrl, isMain);
          
            
            _images.Add(image);
            UpdatedAt = DateTime.UtcNow;
        }

        public void SetMainImage(Guid imageId)
        {
            var image = Images.FirstOrDefault(i => i.Id == imageId);

            if(image == null)
                throw new ArgumentException("Image not found.");

            foreach (var item in Images)
                item.RemoveAsMain();

            image.SetAsMain();
            UpdatedAt = DateTime.UtcNow;
        }



        public void RemoveImage(Guid imageId)
        {
            var image = Images.FirstOrDefault(i => i.Id == imageId);

            if (image == null)
                throw new ArgumentException("Image not found.");

            _images.Remove(image);

            UpdatedAt = DateTime.UtcNow;
        }
    public void Rename(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException(
                    "Product name is required.",
                    nameof(name));

            Name = name.Trim();
            UpdatedAt = DateTime.UtcNow;
        }

        public void ChangeDescription(string? description)
        {
            Description = description?.Trim();
            UpdatedAt = DateTime.UtcNow;
        }
        public void ChangeCategory(Guid categoryId)
        {
            if (categoryId == Guid.Empty)
                throw new ArgumentException(
                    "Category ID is required.",
                    nameof(categoryId));

            CategoryId = categoryId;
            UpdatedAt = DateTime.UtcNow;
        }
        
    }
    }