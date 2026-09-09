using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Coms.Domain.Common;
using Coms.Domain.Orders;

namespace Coms.Application.Orders
{
    public interface IOrderService
    {
        Task<Order?> GetAsync(int id, CancellationToken cancellationToken = default);

        Task<Order?> GetByNumberAsync(string orderNumber, CancellationToken cancellationToken = default);

        Task<PagedResult<OrderSummary>> SearchAsync(OrderFilter filter, PagedRequest paging, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<OrderSummary>> GetRecentAsync(int count, CancellationToken cancellationToken = default);

        Task<PagedResult<OrderStatusChange>> GetRecentHistoryAsync(PagedRequest paging, CancellationToken cancellationToken = default);

        /// <summary>Builds and saves a Draft order from user input.</summary>
        Task<Result<Order>> CreateDraftAsync(OrderInput input, CancellationToken cancellationToken = default);

        /// <summary>Replaces header and lines of a Draft or Submitted order.</summary>
        Task<Result<Order>> UpdateAsync(int orderId, byte[] rowVersion, OrderInput input, CancellationToken cancellationToken = default);

        /// <summary>Recomputes what the totals would be for the given input, without saving. Used by live totals on screen.</summary>
        Task<Result<Order>> PreviewAsync(OrderInput input, CancellationToken cancellationToken = default);

        Task<Result<Order>> SubmitAsync(int orderId, byte[] rowVersion, string? comment, CancellationToken cancellationToken = default);

        Task<Result<Order>> ApproveAsync(int orderId, byte[] rowVersion, string? comment, CancellationToken cancellationToken = default);

        Task<Result<Order>> FulfilAsync(int orderId, byte[] rowVersion, string? comment, CancellationToken cancellationToken = default);

        Task<Result<Order>> CancelAsync(int orderId, byte[] rowVersion, string? reason, CancellationToken cancellationToken = default);
    }
}
