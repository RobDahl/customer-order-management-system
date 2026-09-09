using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Coms.Domain.Reports
{
    /// <summary>Read-only access to the rpt schema.</summary>
    public interface IReportRepository
    {
        Task<DashboardCounts> GetDashboardCountsAsync(CancellationToken cancellationToken = default);

        Task<IReadOnlyList<SalesByMonthRow>> GetSalesByMonthAsync(DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<TopCustomerRow>> GetTopCustomersAsync(DateTime fromDate, DateTime toDate, int top, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<ArAgingRow>> GetArAgingAsync(DateTime? asOfDate, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<ProductSalesRow>> GetProductSalesAsync(DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<LowStockRow>> GetLowStockAsync(CancellationToken cancellationToken = default);

        Task<IReadOnlyList<InvoiceAgingRow>> GetInvoiceAgingAsync(int? customerId, CancellationToken cancellationToken = default);
    }
}
