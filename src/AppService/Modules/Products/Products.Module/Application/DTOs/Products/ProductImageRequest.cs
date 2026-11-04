using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Catalog.Module.Application.DTOs.Products
{

    public record ProductImageRequest(
        string ImageUrl,
        bool IsMain = false);
}
