using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Coms.Domain.Reports;

namespace Coms.Application.Tests.Fakes
{
    internal sealed class FakeReportRepository : IReportRepository
    {
        public List<(DateTime From, DateTime To, int Top)> TopCustomerCalls { get; } = new List<(DateTime, DateTime, int)>();

        public Task<DashboardCounts> GetDashboardCountsAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new DashboardCounts { ActiveCustomers = 3, AsOfDate = DateTime.UtcNow.Date });
        }

        public Task<IReadOnlyList<SalesByMonthRow>> GetSalesByMonthAsync(DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default)
        {
            IReadOnlyList<SalesByMonthRow> rows = new[] { new SalesByMonthRow { MonthStart = fromDate, MonthLabel = fromDate.ToString("yyyy-MM") } };
            return Task.FromResult(rows);
        }

        public Task<IReadOnlyList<TopCustomerRow>> GetTopCustomersAsync(DateTime fromDate, DateTime toDate, int top, CancellationToken cancellationToken = default)
        {
            TopCustomerCalls.Add((fromDate, toDate, top));
            IReadOnlyList<TopCustomerRow> rows = Array.Empty<TopCustomerRow>();
            return Task.FromResult(rows);
        }

        public Task<IReadOnlyList<ArAgingRow>> GetArAgingAsync(DateTime? asOfDate, CancellationToken cancellationToken = default)
        {
            IReadOnlyList<ArAgingRow> rows = Array.Empty<ArAgingRow>();
            return Task.FromResult(rows);
        }

        public Task<IReadOnlyList<ProductSalesRow>> GetProductSalesAsync(DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default)
        {
            IReadOnlyList<ProductSalesRow> rows = Array.Empty<ProductSalesRow>();
            return Task.FromResult(rows);
        }

        public Task<IReadOnlyList<LowStockRow>> GetLowStockAsync(CancellationToken cancellationToken = default)
        {
            IReadOnlyList<LowStockRow> rows = Array.Empty<LowStockRow>();
            return Task.FromResult(rows);
        }

        public Task<IReadOnlyList<InvoiceAgingRow>> GetInvoiceAgingAsync(int? customerId, CancellationToken cancellationToken = default)
        {
            IReadOnlyList<InvoiceAgingRow> rows = Array.Empty<InvoiceAgingRow>();
            return Task.FromResult(rows);
        }
    }
}
