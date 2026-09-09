using System.Collections.Generic;
using Coms.Domain.Common;
using Coms.Domain.Products;

namespace Coms.Domain.Orders
{
    public class OrderLine
    {
        public int Id { get; set; }

        public int OrderId { get; set; }

        public int LineNumber { get; set; }

        public int ProductId { get; set; }

        /// <summary>Snapshot of the product SKU when the line was added.</summary>
        public string Sku { get; set; } = string.Empty;

        /// <summary>Snapshot of the product name; editable per line.</summary>
        public string Description { get; set; } = string.Empty;

        public decimal Quantity { get; set; }

        public decimal UnitPrice { get; set; }

        /// <summary>Snapshot of the product cost, for margin reporting.</summary>
        public decimal UnitCost { get; set; }

        public decimal DiscountPercent { get; set; }

        /// <summary>Set by <see cref="Recalculate"/>; never entered by hand.</summary>
        public decimal LineTotal { get; set; }

        public static OrderLine FromProduct(ProductLookup product, decimal quantity)
        {
            return new OrderLine
            {
                ProductId = product.Id,
                Sku = product.Sku,
                Description = product.Name,
                Quantity = quantity,
                UnitPrice = product.UnitPrice,
                UnitCost = product.CostPrice
            };
        }

        public void Recalculate()
        {
            LineTotal = Money.Round(Quantity * UnitPrice * (1 - DiscountPercent / 100m));
        }

        public void Validate(string prefix, ICollection<ValidationError> errors)
        {
            if (ProductId <= 0)
            {
                errors.Add(new ValidationError(prefix + nameof(ProductId), "A product is required."));
            }

            if (string.IsNullOrWhiteSpace(Description))
            {
                errors.Add(new ValidationError(prefix + nameof(Description), "Description is required."));
            }
            else if (Description.Length > 200)
            {
                errors.Add(new ValidationError(prefix + nameof(Description), "Description must be 200 characters or fewer."));
            }

            if (Quantity <= 0)
            {
                errors.Add(new ValidationError(prefix + nameof(Quantity), "Quantity must be greater than zero."));
            }

            if (UnitPrice < 0)
            {
                errors.Add(new ValidationError(prefix + nameof(UnitPrice), "Unit price cannot be negative."));
            }

            if (UnitCost < 0)
            {
                errors.Add(new ValidationError(prefix + nameof(UnitCost), "Unit cost cannot be negative."));
            }

            if (DiscountPercent < 0 || DiscountPercent > 100)
            {
                errors.Add(new ValidationError(prefix + nameof(DiscountPercent), "Discount must be between 0 and 100 percent."));
            }
        }
    }
}
