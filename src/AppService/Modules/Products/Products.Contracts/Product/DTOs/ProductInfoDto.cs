using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Products.Contract.Product.DTOs
{

    public record ProductInfoDto(
    Guid Id,
    string Name,
    string? Description,
    decimal Price,
    Guid CategoryId,
    bool IsAvailable,
    IReadOnlyCollection<ProductImageDto> Images
);
}
