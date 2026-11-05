using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Products.Module.Application.DTOs.Categories
{
     
    public record UpdateCategoryRequest(
        string Name,
        string? Description,
        string? ImageUrl,
        Guid? ParentId
    );
}
