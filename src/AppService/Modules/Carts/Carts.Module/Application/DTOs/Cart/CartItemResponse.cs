using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Products.Module.Application.DTOs.Cart
{

    public record CartItemResponse(
    Guid Id,
    Guid ProductId,
    int Quantity
);
}
