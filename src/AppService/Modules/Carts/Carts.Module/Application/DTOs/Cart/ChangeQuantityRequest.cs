using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Products.Module.Application.DTOs.Cart
{
     
    public record ChangeQuantityRequest(
        [Range(1, 1000, ErrorMessage = "Amount must be greater than zero.")] int Amount
    );
}
