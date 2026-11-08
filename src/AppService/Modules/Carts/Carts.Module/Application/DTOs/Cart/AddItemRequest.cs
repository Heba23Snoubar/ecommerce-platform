using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Products.Module.Application.DTOs.Cart
{
    public record AddItemRequest(
     [Required] Guid ProductId,
     [Range(1, 1000, ErrorMessage = "Quantity must be greater than zero.")] int Quantity
 );
}
