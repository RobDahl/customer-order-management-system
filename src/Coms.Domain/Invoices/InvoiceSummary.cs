using System;

namespace Coms.Domain.Invoices
{
    /// <summary>What an invoice list shows.</summary>
    public sealed class InvoiceSummary
    {
        public int InvoiceId { get; set; }

        public string InvoiceNumber { get; set; } = string.Empty;

        public int OrderId { get; set; }

        public string OrderNumber { get; set; } = string.Empty;

        public int CustomerId { get; set; }

        public string CustomerNumber { get; set; } = string.Empty;

        public string CustomerName { get; set; } = string.Empty;

        public DateTime IssuedDate { get; set; }

        public DateTime DueDate { get; set; }

        public decimal Total { get; set; }

        public decimal AmountPaid { get; set; }

        public decimal Balance { get; set; }

        public InvoiceStatus Status { get; set; }

        public int DaysOverdue { get; set; }
    }
}
