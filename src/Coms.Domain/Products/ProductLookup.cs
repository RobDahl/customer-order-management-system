namespace Coms.Domain.Products
{
    /// <summary>The few columns an order-entry picker needs.</summary>
    public sealed class ProductLookup
    {
        public int Id { get; set; }

        public string Sku { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string UnitOfMeasure { get; set; } = string.Empty;

        public decimal UnitPrice { get; set; }

        public decimal CostPrice { get; set; }

        public decimal QuantityOnHand { get; set; }

        public bool IsActive { get; set; }
    }
}
