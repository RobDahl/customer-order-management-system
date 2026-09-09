using System;
using System.Collections.Generic;
using Coms.Domain.Common;

namespace Coms.Application.Orders
{
    /// <summary>
    /// What a user types on the order entry screen. The service turns this
    /// into an <see cref="Domain.Orders.Order"/> by looking up products,
    /// snapshotting prices and computing totals.
    /// </summary>
    public sealed class OrderInput
    {
        public int CustomerId { get; set; }

        public DateTime OrderDate { get; set; } = DateTime.UtcNow.Date;

        public DateTime? RequiredDate { get; set; }

        public string? CustomerReference { get; set; }

        public string? Notes { get; set; }

        /// <summary>Null means "ship to the customer's effective shipping address".</summary>
        public Address? ShipTo { get; set; }

        public List<OrderLineInput> Lines { get; set; } = new List<OrderLineInput>();
    }

    public sealed class OrderLineInput
    {
        public int ProductId { get; set; }

        public decimal Quantity { get; set; }

        public decimal DiscountPercent { get; set; }

        /// <summary>Null means "use the product's current price".</summary>
        public decimal? UnitPrice { get; set; }

        /// <summary>Null means "use the product's name".</summary>
        public string? Description { get; set; }
    }
}
