using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Coms.Data.Connections;
using Coms.Domain.Reports;
using Dapper;

namespace Coms.Data.Repositories
{
    public sealed class ReportRepository : RepositoryBase, IReportRepository
    {
        public ReportRepository(IDbSession session)
            : base(session)
        {
        }

        public async Task<DashboardCounts> GetDashboardCountsAsync(CancellationToken cancellationToken = default)
        {
            IDbConnection connection = await ConnectionAsync(cancellationToken).ConfigureAwait(false);
            return await connection.QuerySingleAsync<DashboardCounts>(Procedure("rpt.usp_DashboardCounts", null, cancellationToken)).ConfigureAwait(false);
        }

        public async Task<IReadOnlyList<SalesByMonthRow>> GetSalesByMonthAsync(DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default)
        {
            var parameters = new DynamicParameters();
            parameters.Add("@FromDate", fromDate.Date, DbType.Date);
            parameters.Add("@ToDate", toDate.Date, DbType.Date);

            IDbConnection connection = await ConnectionAsync(cancellationToken).ConfigureAwait(false);
            return (await connection.QueryAsync<SalesByMonthRow>(Procedure("rpt.usp_SalesByMonth", parameters, cancellationToken)).ConfigureAwait(false)).ToList();
        }

        public async Task<IReadOnlyList<TopCustomerRow>> GetTopCustomersAsync(DateTime fromDate, DateTime toDate, int top, CancellationToken cancellationToken = default)
        {
            var parameters = new DynamicParameters();
            parameters.Add("@FromDate", fromDate.Date, DbType.Date);
            parameters.Add("@ToDate", toDate.Date, DbType.Date);
            parameters.Add("@Top", top);

            IDbConnection connection = await ConnectionAsync(cancellationToken).ConfigureAwait(false);
            return (await connection.QueryAsync<TopCustomerRow>(Procedure("rpt.usp_TopCustomers", parameters, cancellationToken)).ConfigureAwait(false)).ToList();
        }

        public async Task<IReadOnlyList<ArAgingRow>> GetArAgingAsync(DateTime? asOfDate, CancellationToken cancellationToken = default)
        {
            var parameters = new DynamicParameters();
            parameters.Add("@AsOfDate", asOfDate?.Date, DbType.Date);

            IDbConnection connection = await ConnectionAsync(cancellationToken).ConfigureAwait(false);
            return (await connection.QueryAsync<ArAgingRow>(Procedure("rpt.usp_ArAging", parameters, cancellationToken)).ConfigureAwait(false)).ToList();
        }

        public async Task<IReadOnlyList<ProductSalesRow>> GetProductSalesAsync(DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default)
        {
            var parameters = new DynamicParameters();
            parameters.Add("@FromDate", fromDate.Date, DbType.Date);
            parameters.Add("@ToDate", toDate.Date, DbType.Date);

            IDbConnection connection = await ConnectionAsync(cancellationToken).ConfigureAwait(false);
            return (await connection.QueryAsync<ProductSalesRow>(Procedure("rpt.usp_ProductSales", parameters, cancellationToken)).ConfigureAwait(false)).ToList();
        }

        public async Task<IReadOnlyList<LowStockRow>> GetLowStockAsync(CancellationToken cancellationToken = default)
        {
            const string sql = @"
                SELECT ProductId, Sku, Name, Category, UnitOfMeasure, QuantityOnHand, ReorderLevel, PendingDemand, ProjectedOnHand, Shortfall
                  FROM rpt.vw_LowStock
                 ORDER BY Shortfall DESC, Sku;";

            IDbConnection connection = await ConnectionAsync(cancellationToken).ConfigureAwait(false);
            return (await connection.QueryAsync<LowStockRow>(Text(sql, null, cancellationToken)).ConfigureAwait(false)).ToList();
        }

        public async Task<IReadOnlyList<InvoiceAgingRow>> GetInvoiceAgingAsync(int? customerId, CancellationToken cancellationToken = default)
        {
            const string sql = @"
                SELECT InvoiceId, InvoiceNumber, CustomerId, CustomerNumber, CustomerName, OrderId, OrderNumber,
                       IssuedDate, DueDate, Total, AmountPaid, Balance, Status, DaysOverdue, AgeBucket
                  FROM rpt.vw_InvoiceAging
                 WHERE @CustomerId IS NULL OR CustomerId = @CustomerId
                 ORDER BY DaysOverdue DESC, DueDate, InvoiceNumber;";

            IDbConnection connection = await ConnectionAsync(cancellationToken).ConfigureAwait(false);
            return (await connection.QueryAsync<InvoiceAgingRow>(Text(sql, new { CustomerId = customerId }, cancellationToken)).ConfigureAwait(false)).ToList();
        }
    }
}
