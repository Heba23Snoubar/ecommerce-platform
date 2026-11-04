using Products.Contract.Product.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Products.Contract.Product.Interfaces
{

     
    public interface IProductQueryService
    {
        Task<ProductInfoDto?> GetByIdAsync(
            Guid productId,
            CancellationToken cancellationToken = default
        );

        Task<IReadOnlyCollection<ProductInfoDto>> GetByIdsAsync(
            IReadOnlyCollection<Guid> productIds,
            CancellationToken cancellationToken = default
        );
    }
}
