using System;

namespace Coms.Domain.Customers
{
    /// <summary>What a customer owes and how much room they have left. Read from rpt.vw_CustomerBalance.</summary>
    public sealed class CustomerBalance
    {
        public int CustomerId { get; set; }

        public string CustomerNumber { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public CustomerStatus Status { get; set; }

        public int PaymentTermsDays { get; set; }

        public decimal? CreditLimit { get; set; }

        public int OpenInvoiceCount { get; set; }

        public decimal OutstandingBalance { get; set; }

        public decimal OverdueBalance { get; set; }

        public DateTime? OldestDueDate { get; set; }

        /// <summary>Approved or fulfilled orders not yet invoiced.</summary>
        public int CommittedOrderCount { get; set; }

        public decimal CommittedTotal { get; set; }

        /// <summary>Outstanding plus committed.</summary>
        public decimal Exposure { get; set; }

        /// <summary>Null when the customer has no credit limit.</summary>
        public decimal? CreditAvailable { get; set; }

        /// <summary>True when an additional <paramref name="amount"/> would exceed the credit limit.</summary>
        public bool WouldExceedLimit(decimal amount)
        {
            return CreditAvailable.HasValue && amount > CreditAvailable.Value;
        }
    }
}
