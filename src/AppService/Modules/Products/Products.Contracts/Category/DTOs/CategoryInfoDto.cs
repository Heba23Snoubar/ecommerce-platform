using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Products.Contract.Category.DTOs
{


    public record CategoryInfoDto(
        Guid Id,
        string Name,
        string? Description,
        Guid? ParentId,
        string? ImageUrl
    );
}
