using Carts.Contract.Cart.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Carts.Contract.Cart.Interfaces
{
    public interface ICartQueryService
    {
          
            Task<CartIntegrationDto?> GetCartByUserIdAsync(Guid userId, CancellationToken ct = default);

            Task ClearCartAsync(Guid userId, CancellationToken ct = default);
         
    }
}
