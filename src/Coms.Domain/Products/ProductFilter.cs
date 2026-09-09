namespace Coms.Domain.Products
{
    public sealed class ProductFilter
    {
        /// <summary>Matches the start of the SKU or name.</summary>
        public string? Search { get; set; }

        public string? Category { get; set; }

        /// <summary>Null = all, true = active only, false = inactive only.</summary>
        public bool? IsActive { get; set; }

        public bool LowStockOnly { get; set; }
    }
}
