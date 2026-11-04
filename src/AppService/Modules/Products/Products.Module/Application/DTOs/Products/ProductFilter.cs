using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Catalog.Module.Application.DTOs.Products
{
   
    public record ProductFilter
    {
        public string? SearchTerm { get; init; }

        public Guid? CategoryId { get; init; }

        public decimal? MinPrice { get; init; }

        public decimal? MaxPrice { get; init; }

        public bool? IsAvailable { get; init; }

        public string? SortBy { get; init; }

        public bool SortDescending { get; init; }

        public int PageNumber { get; init; } = 1;

        public int PageSize { get; init; } = 10;
    }
}
