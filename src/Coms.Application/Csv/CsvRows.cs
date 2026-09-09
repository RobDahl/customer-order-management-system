namespace Coms.Application.Csv
{
    /* Flat, all-string row shapes. The same shape is written by export and
       read by import, so a file exported from one system can be imported
       into another. Keeping the fields as text lets the importer report a
       precise message per bad value instead of failing on the first one. */

    public sealed class CustomerCsvRow
    {
        public string? CustomerNumber { get; set; }
        public string? Name { get; set; }
        public string? ContactName { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? BillingLine1 { get; set; }
        public string? BillingLine2 { get; set; }
        public string? BillingCity { get; set; }
        public string? BillingRegion { get; set; }
        public string? BillingPostalCode { get; set; }
        public string? BillingCountry { get; set; }
        public string? ShippingLine1 { get; set; }
        public string? ShippingLine2 { get; set; }
        public string? ShippingCity { get; set; }
        public string? ShippingRegion { get; set; }
        public string? ShippingPostalCode { get; set; }
        public string? ShippingCountry { get; set; }
        public string? PaymentTermsDays { get; set; }
        public string? CreditLimit { get; set; }
        public string? Status { get; set; }
        public string? Notes { get; set; }

        public static readonly string[] Headers =
        {
            nameof(CustomerNumber), nameof(Name), nameof(ContactName), nameof(Email), nameof(Phone),
            nameof(BillingLine1), nameof(BillingLine2), nameof(BillingCity), nameof(BillingRegion), nameof(BillingPostalCode), nameof(BillingCountry),
            nameof(ShippingLine1), nameof(ShippingLine2), nameof(ShippingCity), nameof(ShippingRegion), nameof(ShippingPostalCode), nameof(ShippingCountry),
            nameof(PaymentTermsDays), nameof(CreditLimit), nameof(Status), nameof(Notes)
        };

        public static readonly string[] RequiredHeaders =
        {
            nameof(Name), nameof(BillingLine1), nameof(BillingCity), nameof(BillingCountry)
        };
    }

    public sealed class ProductCsvRow
    {
        public string? Sku { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
        public string? Category { get; set; }
        public string? UnitOfMeasure { get; set; }
        public string? UnitPrice { get; set; }
        public string? CostPrice { get; set; }
        public string? QuantityOnHand { get; set; }
        public string? ReorderLevel { get; set; }
        public string? IsActive { get; set; }

        public static readonly string[] Headers =
        {
            nameof(Sku), nameof(Name), nameof(Description), nameof(Category), nameof(UnitOfMeasure),
            nameof(UnitPrice), nameof(CostPrice), nameof(QuantityOnHand), nameof(ReorderLevel), nameof(IsActive)
        };

        public static readonly string[] RequiredHeaders =
        {
            nameof(Sku), nameof(Name), nameof(Category), nameof(UnitPrice)
        };
    }

    public sealed class OrderCsvRow
    {
        public string? OrderNumber { get; set; }
        public string? Status { get; set; }
        public string? OrderDate { get; set; }
        public string? RequiredDate { get; set; }
        public string? CustomerNumber { get; set; }
        public string? CustomerName { get; set; }
        public string? CustomerReference { get; set; }
        public string? Lines { get; set; }
        public string? Subtotal { get; set; }
        public string? TaxAmount { get; set; }
        public string? Total { get; set; }
        public string? InvoiceNumber { get; set; }
        public string? InvoiceStatus { get; set; }
    }

    public sealed class OrderLineCsvRow
    {
        public string? OrderNumber { get; set; }
        public string? Status { get; set; }
        public string? OrderDate { get; set; }
        public string? CustomerNumber { get; set; }
        public string? CustomerName { get; set; }
        public string? LineNumber { get; set; }
        public string? Sku { get; set; }
        public string? Description { get; set; }
        public string? Quantity { get; set; }
        public string? UnitPrice { get; set; }
        public string? DiscountPercent { get; set; }
        public string? LineTotal { get; set; }
    }

    public sealed class InvoiceCsvRow
    {
        public string? InvoiceNumber { get; set; }
        public string? Status { get; set; }
        public string? OrderNumber { get; set; }
        public string? CustomerNumber { get; set; }
        public string? CustomerName { get; set; }
        public string? IssuedDate { get; set; }
        public string? DueDate { get; set; }
        public string? Total { get; set; }
        public string? AmountPaid { get; set; }
        public string? Balance { get; set; }
        public string? DaysOverdue { get; set; }
    }
}
