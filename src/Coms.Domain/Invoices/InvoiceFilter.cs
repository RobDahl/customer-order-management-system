using System;

namespace Coms.Domain.Invoices
{
    public sealed class InvoiceFilter
    {
        /// <summary>Matches the start of the invoice number, order number, customer number or customer name.</summary>
        public string? Search { get; set; }

        public InvoiceStatus? Status { get; set; }

        public int? CustomerId { get; set; }

        public bool OverdueOnly { get; set; }

        public DateTime? FromDate { get; set; }

        public DateTime? ToDate { get; set; }
    }
}
