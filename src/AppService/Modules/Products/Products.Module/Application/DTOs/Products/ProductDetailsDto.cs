using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Catalog.Module.Application.DTOs.Products
{
    public record ProductDetailsDto(
        Guid Id,
        string Name,
        string? Description,
        decimal Price,
        Guid CategoryId,
        bool IsAvailable,
        IReadOnlyCollection<ProductImageDto> Images,
        DateTime CreatedAt,
        DateTime? UpdatedAt);
}
