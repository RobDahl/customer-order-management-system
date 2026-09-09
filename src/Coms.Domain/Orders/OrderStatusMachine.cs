using System;
using System.Collections.Generic;

namespace Coms.Domain.Orders
{
    /// <summary>
    /// The one place that knows which order status changes are allowed.
    ///
    ///   Draft -> Submitted -> Approved -> Fulfilled -> Invoiced
    ///     |          |            |
    ///     +----------+------------+--------> Cancelled
    /// </summary>
    public static class OrderStatusMachine
    {
        private static readonly Dictionary<OrderStatus, OrderStatus[]> Transitions = new Dictionary<OrderStatus, OrderStatus[]>
        {
            { OrderStatus.Draft,     new[] { OrderStatus.Submitted, OrderStatus.Cancelled } },
            { OrderStatus.Submitted, new[] { OrderStatus.Approved, OrderStatus.Cancelled } },
            { OrderStatus.Approved,  new[] { OrderStatus.Fulfilled, OrderStatus.Cancelled } },
            { OrderStatus.Fulfilled, new[] { OrderStatus.Invoiced } },
            { OrderStatus.Invoiced,  Array.Empty<OrderStatus>() },
            { OrderStatus.Cancelled, Array.Empty<OrderStatus>() }
        };

        public static bool CanTransition(OrderStatus from, OrderStatus to)
        {
            return Array.IndexOf(Transitions[from], to) >= 0;
        }

        public static IReadOnlyList<OrderStatus> AllowedTransitions(OrderStatus from)
        {
            return Transitions[from];
        }

        /// <summary>Nothing can happen to an order in a terminal status.</summary>
        public static bool IsTerminal(OrderStatus status)
        {
            return Transitions[status].Length == 0;
        }

        /// <summary>Lines and header details may only change before approval.</summary>
        public static bool AllowsEditing(OrderStatus status)
        {
            return status == OrderStatus.Draft || status == OrderStatus.Submitted;
        }

        /// <summary>Statuses that count as a sale for reporting.</summary>
        public static bool IsSale(OrderStatus status)
        {
            return status == OrderStatus.Fulfilled || status == OrderStatus.Invoiced;
        }
    }
}
