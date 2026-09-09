using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Coms.Domain.Common;
using Coms.Domain.Reports;

namespace Coms.Application.Reports
{
    public sealed class ReportService : IReportService
    {
        public const int DefaultTop = 10;
        public const int MaxTop = 100;

        private readonly IReportRepository _reports;

        public ReportService(IReportRepository reports)
        {
            _reports = reports ?? throw new ArgumentNullException(nameof(reports));
        }

        public Task<DashboardCounts> GetDashboardCountsAsync(CancellationToken cancellationToken = default)
        {
            return _reports.GetDashboardCountsAsync(cancellationToken);
        }

        public async Task<Result<IReadOnlyList<SalesByMonthRow>>> GetSalesByMonthAsync(ReportPeriod period, CancellationToken cancellationToken = default)
        {
            IReadOnlyList<ValidationError> errors = period.Validate();
            if (errors.Count > 0)
            {
                return Result<IReadOnlyList<SalesByMonthRow>>.Invalid(errors);
            }

            IReadOnlyList<SalesByMonthRow> rows = await _reports.GetSalesByMonthAsync(period.FromDate, period.ToDate, cancellationToken).ConfigureAwait(false);
            return Result<IReadOnlyList<SalesByMonthRow>>.Success(rows);
        }

        public async Task<Result<IReadOnlyList<TopCustomerRow>>> GetTopCustomersAsync(ReportPeriod period, int top, CancellationToken cancellationToken = default)
        {
            IReadOnlyList<ValidationError> errors = period.Validate();
            if (errors.Count > 0)
            {
                return Result<IReadOnlyList<TopCustomerRow>>.Invalid(errors);
            }

            int limited = top < 1 ? DefaultTop : Math.Min(top, MaxTop);
            IReadOnlyList<TopCustomerRow> rows = await _reports.GetTopCustomersAsync(period.FromDate, period.ToDate, limited, cancellationToken).ConfigureAwait(false);
            return Result<IReadOnlyList<TopCustomerRow>>.Success(rows);
        }

        public Task<IReadOnlyList<ArAgingRow>> GetArAgingAsync(DateTime? asOfDate, CancellationToken cancellationToken = default)
        {
            return _reports.GetArAgingAsync(asOfDate, cancellationToken);
        }

        public async Task<Result<IReadOnlyList<ProductSalesRow>>> GetProductSalesAsync(ReportPeriod period, CancellationToken cancellationToken = default)
        {
            IReadOnlyList<ValidationError> errors = period.Validate();
            if (errors.Count > 0)
            {
                return Result<IReadOnlyList<ProductSalesRow>>.Invalid(errors);
            }

            IReadOnlyList<ProductSalesRow> rows = await _reports.GetProductSalesAsync(period.FromDate, period.ToDate, cancellationToken).ConfigureAwait(false);
            return Result<IReadOnlyList<ProductSalesRow>>.Success(rows);
        }

        public Task<IReadOnlyList<LowStockRow>> GetLowStockAsync(CancellationToken cancellationToken = default)
        {
            return _reports.GetLowStockAsync(cancellationToken);
        }

        public Task<IReadOnlyList<InvoiceAgingRow>> GetInvoiceAgingAsync(int? customerId, CancellationToken cancellationToken = default)
        {
            return _reports.GetInvoiceAgingAsync(customerId, cancellationToken);
        }
    }
}
