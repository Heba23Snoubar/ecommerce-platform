using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Carts.Contract.Cart.DTOs
{
     
    public record CartItemIntegrationDto(
        Guid ProductId,
        int Quantity
    );
}
