using System.Collections.Generic;
using Coms.Domain.Common;

namespace Coms.Domain.Products
{
    public class Product : AuditedEntity
    {
        public const string DefaultUnitOfMeasure = "EA";

        /// <summary>Stored upper-case; unique.</summary>
        public string Sku { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        public string Category { get; set; } = string.Empty;

        public string UnitOfMeasure { get; set; } = DefaultUnitOfMeasure;

        public decimal UnitPrice { get; set; }

        public decimal CostPrice { get; set; }

        public decimal QuantityOnHand { get; set; }

        public decimal ReorderLevel { get; set; }

        public bool IsActive { get; set; } = true;

        public bool IsLowStock => IsActive && QuantityOnHand <= ReorderLevel;

        public decimal UnitMargin => Money.Round(UnitPrice - CostPrice);

        public void Normalize()
        {
            Sku = Sku.Trim().ToUpperInvariant();
            Name = Name.Trim();
            Category = Category.Trim();
            UnitOfMeasure = UnitOfMeasure.Trim().ToUpperInvariant();
            Description = string.IsNullOrWhiteSpace(Description) ? null : Description!.Trim();
        }

        public IReadOnlyList<ValidationError> Validate()
        {
            var errors = new List<ValidationError>();

            if (string.IsNullOrWhiteSpace(Sku))
            {
                errors.Add(new ValidationError(nameof(Sku), "SKU is required."));
            }
            else if (Sku.Length > 40)
            {
                errors.Add(new ValidationError(nameof(Sku), "SKU must be 40 characters or fewer."));
            }
            else if (Sku.Contains(" "))
            {
                errors.Add(new ValidationError(nameof(Sku), "SKU cannot contain spaces."));
            }

            if (string.IsNullOrWhiteSpace(Name))
            {
                errors.Add(new ValidationError(nameof(Name), "Name is required."));
            }
            else if (Name.Length > 200)
            {
                errors.Add(new ValidationError(nameof(Name), "Name must be 200 characters or fewer."));
            }

            if (Description != null && Description.Length > 1000)
            {
                errors.Add(new ValidationError(nameof(Description), "Description must be 1000 characters or fewer."));
            }

            if (string.IsNullOrWhiteSpace(Category))
            {
                errors.Add(new ValidationError(nameof(Category), "Category is required."));
            }
            else if (Category.Length > 60)
            {
                errors.Add(new ValidationError(nameof(Category), "Category must be 60 characters or fewer."));
            }

            if (string.IsNullOrWhiteSpace(UnitOfMeasure))
            {
                errors.Add(new ValidationError(nameof(UnitOfMeasure), "Unit of measure is required."));
            }
            else if (UnitOfMeasure.Length > 10)
            {
                errors.Add(new ValidationError(nameof(UnitOfMeasure), "Unit of measure must be 10 characters or fewer."));
            }

            if (UnitPrice < 0)
            {
                errors.Add(new ValidationError(nameof(UnitPrice), "Unit price cannot be negative."));
            }

            if (CostPrice < 0)
            {
                errors.Add(new ValidationError(nameof(CostPrice), "Cost price cannot be negative."));
            }

            if (ReorderLevel < 0)
            {
                errors.Add(new ValidationError(nameof(ReorderLevel), "Reorder level cannot be negative."));
            }

            return errors;
        }
    }
}
