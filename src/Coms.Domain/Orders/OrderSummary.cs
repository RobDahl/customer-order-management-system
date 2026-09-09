using System;
using Coms.Domain.Invoices;

namespace Coms.Domain.Orders
{
    /// <summary>One row of rpt.vw_OrderSummary: what an order list shows.</summary>
    public sealed class OrderSummary
    {
        public int OrderId { get; set; }

        public string OrderNumber { get; set; } = string.Empty;

        public OrderStatus Status { get; set; }

        public DateTime OrderDate { get; set; }

        public DateTime? RequiredDate { get; set; }

        public string? CustomerReference { get; set; }

        public int CustomerId { get; set; }

        public string CustomerNumber { get; set; } = string.Empty;

        public string CustomerName { get; set; } = string.Empty;

        public decimal Subtotal { get; set; }

        public decimal TaxRate { get; set; }

        public decimal TaxAmount { get; set; }

        public decimal Total { get; set; }

        public int LineCount { get; set; }

        public int? InvoiceId { get; set; }

        public string? InvoiceNumber { get; set; }

        public InvoiceStatus? InvoiceStatus { get; set; }

        public DateTime UpdatedAtUtc { get; set; }

        public string UpdatedBy { get; set; } = string.Empty;
    }
}
