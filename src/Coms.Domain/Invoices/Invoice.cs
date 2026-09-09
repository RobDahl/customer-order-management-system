using System;
using System.Collections.Generic;
using Coms.Domain.Common;

namespace Coms.Domain.Invoices
{
    /// <summary>
    /// Created only by dbo.usp_Invoice_CreateFromOrder and changed only by the
    /// payment and void procedures, so this class has no Validate method.
    /// </summary>
    public class Invoice : AuditedEntity
    {
        public string InvoiceNumber { get; set; } = string.Empty;

        public int OrderId { get; set; }

        public int CustomerId { get; set; }

        public DateTime IssuedDate { get; set; }

        public DateTime DueDate { get; set; }

        public decimal Subtotal { get; set; }

        public decimal TaxAmount { get; set; }

        public decimal Total { get; set; }

        public decimal AmountPaid { get; set; }

        public InvoiceStatus Status { get; set; }

        public List<Payment> Payments { get; set; } = new List<Payment>();

        public decimal Balance => Total - AmountPaid;

        public bool IsOpen => Status == InvoiceStatus.Open || Status == InvoiceStatus.PartiallyPaid;

        public bool IsOverdue(DateTime today)
        {
            return IsOpen && DueDate.Date < today.Date;
        }

        public int DaysOverdue(DateTime today)
        {
            return IsOverdue(today) ? (today.Date - DueDate.Date).Days : 0;
        }
    }
}
