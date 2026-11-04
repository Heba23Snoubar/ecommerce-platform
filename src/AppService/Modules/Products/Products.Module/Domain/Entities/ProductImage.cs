using E.Domain.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace E.Domain.Entites
{
    public class ProductImage : BaseEntity
    {
        public string ImageUrl { get; private set; } = null!;

        public bool IsMain { get; private set; }

        public Guid ProductId { get; private set; }

        public Product Product { get; private set; } = null!;

        private ProductImage() { }



        public ProductImage(string imageUrl, bool isMain = false)
        {
            if (string.IsNullOrWhiteSpace(imageUrl))
                throw new ArgumentException(
                    "Image URL is required.",
                    nameof(imageUrl));

            ImageUrl = imageUrl.Trim();
            IsMain = isMain;
        }

        public void ChangeImage(string imageUrl)
        {
            if (string.IsNullOrWhiteSpace(imageUrl))
                throw new ArgumentException(
                    "Image URL is required.",
                    nameof(imageUrl));

            ImageUrl = imageUrl.Trim();
            UpdatedAt = DateTime.UtcNow;
        }

        public void  SetAsMain()
        {
            IsMain = true;
            UpdatedAt = DateTime.UtcNow;
        }

        public void RemoveAsMain()
        {
            IsMain = false;
            UpdatedAt = DateTime.UtcNow;
        }
    }

}