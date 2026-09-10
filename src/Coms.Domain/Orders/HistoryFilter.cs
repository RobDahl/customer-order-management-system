using System;

namespace Coms.Domain.Orders
{
    /// <summary>Filters for the cross-order status history (audit) list.</summary>
    public sealed class HistoryFilter
    {
        /// <summary>Matches the start of the order number.</summary>
        public string? OrderNumber { get; set; }

        public OrderStatus? ToStatus { get; set; }

        /// <summary>Matches the start of the user name.</summary>
        public string? ChangedBy { get; set; }

        public DateTime? FromDate { get; set; }

        public DateTime? ToDate { get; set; }
    }
}
