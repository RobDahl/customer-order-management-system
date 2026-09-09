using System;

namespace Coms.Domain.Orders
{
    /// <summary>One row of dbo.OrderStatusHistory.</summary>
    public sealed class OrderStatusChange
    {
        public int Id { get; set; }

        public int OrderId { get; set; }

        /// <summary>Null for the row that records creation.</summary>
        public OrderStatus? FromStatus { get; set; }

        public OrderStatus ToStatus { get; set; }

        public DateTime ChangedAtUtc { get; set; }

        public string ChangedBy { get; set; } = string.Empty;

        public string? Comment { get; set; }
    }
}
