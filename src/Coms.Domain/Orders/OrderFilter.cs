using System;

namespace Coms.Domain.Orders
{
    public sealed class OrderFilter
    {
        /// <summary>Matches the start of the order number, customer number, customer name or customer reference.</summary>
        public string? Search { get; set; }

        public OrderStatus? Status { get; set; }

        public int? CustomerId { get; set; }

        public DateTime? FromDate { get; set; }

        public DateTime? ToDate { get; set; }
    }
}
