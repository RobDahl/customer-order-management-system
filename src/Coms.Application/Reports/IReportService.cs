using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Coms.Domain.Common;
using Coms.Domain.Reports;

namespace Coms.Application.Reports
{
    public interface IReportService
    {
        Task<DashboardCounts> GetDashboardCountsAsync(CancellationToken cancellationToken = default);

        Task<Result<IReadOnlyList<SalesByMonthRow>>> GetSalesByMonthAsync(ReportPeriod period, CancellationToken cancellationToken = default);

        Task<Result<IReadOnlyList<TopCustomerRow>>> GetTopCustomersAsync(ReportPeriod period, int top, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<ArAgingRow>> GetArAgingAsync(DateTime? asOfDate, CancellationToken cancellationToken = default);

        Task<Result<IReadOnlyList<ProductSalesRow>>> GetProductSalesAsync(ReportPeriod period, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<LowStockRow>> GetLowStockAsync(CancellationToken cancellationToken = default);

        Task<IReadOnlyList<InvoiceAgingRow>> GetInvoiceAgingAsync(int? customerId, CancellationToken cancellationToken = default);
    }
}
