using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Coms.Domain.Common;

namespace Coms.Domain.Orders
{
    public interface IOrderRepository
    {
        /// <summary>The order with its lines and status history.</summary>
        Task<Order?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

        Task<Order?> GetByNumberAsync(string orderNumber, CancellationToken cancellationToken = default);

        Task<PagedResult<OrderSummary>> SearchAsync(OrderFilter filter, PagedRequest paging, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<OrderSummary>> GetRecentAsync(int count, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<OrderStatusChange>> GetHistoryAsync(int orderId, CancellationToken cancellationToken = default);

        /// <summary>Recent status changes across all orders, newest first, for the audit page.</summary>
        Task<PagedResult<OrderStatusChange>> GetRecentHistoryAsync(PagedRequest paging, CancellationToken cancellationToken = default);

        /// <summary>Inserts header and lines, assigns OrderNumber, writes the creation history row.</summary>
        Task InsertAsync(Order order, string userName, CancellationToken cancellationToken = default);

        /// <summary>Updates header and replaces lines. Returns false when the RowVersion is stale.</summary>
        Task<bool> UpdateAsync(Order order, string userName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Single-row status change with a history row, for Submit, Approve and Cancel.
        /// Returns false when the RowVersion is stale or the order is no longer in <paramref name="from"/>.
        /// </summary>
        Task<bool> ChangeStatusAsync(int orderId, OrderStatus from, OrderStatus to, byte[] rowVersion, string userName, string? comment, CancellationToken cancellationToken = default);

        /// <summary>Runs dbo.usp_Order_Fulfil: stock movement plus status change in one transaction.</summary>
        Task<Result> FulfilAsync(int orderId, byte[] rowVersion, string userName, string? comment, bool allowNegativeStock, CancellationToken cancellationToken = default);
    }
}
