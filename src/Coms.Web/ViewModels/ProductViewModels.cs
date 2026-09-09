using System.Collections.Generic;
using Coms.Domain.Common;
using Coms.Domain.Products;

namespace Coms.Web.ViewModels
{
    public sealed class ProductListModel : ListQuery
    {
        public string? Search { get; set; }

        public string? Category { get; set; }

        /// <summary>"active", "inactive" or empty for all.</summary>
        public string? Active { get; set; } = "active";

        public bool LowStock { get; set; }

        public IReadOnlyList<string> Categories { get; set; } = new List<string>();

        public PagedResult<Product> Results { get; set; } = PagedResult<Product>.Empty(new PagedRequest());

        public ProductFilter ToFilter()
        {
            return new ProductFilter
            {
                Search = Search,
                Category = Category,
                IsActive = Active == "active" ? true : Active == "inactive" ? false : (bool?)null,
                LowStockOnly = LowStock
            };
        }
    }

    public sealed class ProductFormModel
    {
        public Product Product { get; set; } = new Product();

        public IReadOnlyList<string> Categories { get; set; } = new List<string>();
    }

    public sealed class StockAdjustmentModel
    {
        public int Id { get; set; }

        public string Sku { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string UnitOfMeasure { get; set; } = string.Empty;

        public decimal CurrentQuantity { get; set; }

        public decimal NewQuantity { get; set; }

        public string Reason { get; set; } = string.Empty;

        public string? RowVersion { get; set; }
    }

    public sealed class SetActiveModel
    {
        public int Id { get; set; }

        public bool IsActive { get; set; }

        public string? RowVersion { get; set; }
    }
}
