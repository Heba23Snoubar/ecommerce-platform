using Products.Contract.Category.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Products.Contract.Category.Interfaces
{
    public interface ICategoryQueryService
    {
        Task<CategoryInfoDto?> GetByIdAsync(
            Guid categoryId,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyCollection<CategoryInfoDto>> GetByIdsAsync(
            IReadOnlyCollection<Guid> categoryIds,
            CancellationToken cancellationToken = default);
    }
}
