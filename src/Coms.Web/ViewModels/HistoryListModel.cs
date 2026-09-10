using System;
using Coms.Domain.Common;
using Coms.Domain.Orders;

namespace Coms.Web.ViewModels
{
    public sealed class HistoryListModel : ListQuery
    {
        public string? OrderNumber { get; set; }

        public OrderStatus? ToStatus { get; set; }

        public string? ChangedBy { get; set; }

        public DateTime? From { get; set; }

        public DateTime? To { get; set; }

        public PagedResult<OrderStatusChange> Results { get; set; } = PagedResult<OrderStatusChange>.Empty(new PagedRequest());

        public HistoryFilter ToFilter()
        {
            return new HistoryFilter { OrderNumber = OrderNumber, ToStatus = ToStatus, ChangedBy = ChangedBy, FromDate = From, ToDate = To };
        }
    }
}
