using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Coms.Data.Connections;
using Coms.Domain.Common;
using Coms.Domain.Invoices;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Coms.Data.Repositories
{
    public sealed class InvoiceRepository : RepositoryBase, IInvoiceRepository
    {
        private const string InvoiceColumns = @"
            i.Id, i.InvoiceNumber, i.OrderId, i.CustomerId, i.IssuedDate, i.DueDate,
            i.Subtotal, i.TaxAmount, i.Total, i.AmountPaid, i.Status,
            i.CreatedAtUtc, i.CreatedBy, i.UpdatedAtUtc, i.UpdatedBy, i.RowVersion";

        private const string PaymentColumns = @"
            p.Id, p.InvoiceId, p.PaidDate, p.Amount, p.Method, p.Reference, p.Notes,
            p.CreatedAtUtc, p.CreatedBy, p.UpdatedAtUtc, p.UpdatedBy, p.RowVersion";

        private const string SummaryColumns = @"
            i.Id AS InvoiceId, i.InvoiceNumber, i.OrderId, o.OrderNumber,
            i.CustomerId, c.CustomerNumber, c.Name AS CustomerName,
            i.IssuedDate, i.DueDate, i.Total, i.AmountPaid, i.Total - i.AmountPaid AS Balance, i.Status,
            CASE WHEN i.Status IN ('Open', 'PartiallyPaid') AND i.DueDate < @Today
                 THEN DATEDIFF(DAY, i.DueDate, @Today) ELSE 0 END AS DaysOverdue";

        private const string SummaryFrom = @"
            FROM dbo.Invoices i
            JOIN dbo.Customers c ON c.Id = i.CustomerId
            JOIN dbo.Orders o ON o.Id = i.OrderId";

        private static readonly IReadOnlyDictionary<string, string> SortColumns = SortMap(
            ("number", "i.InvoiceNumber"),
            ("order", "o.OrderNumber"),
            ("customer", "c.Name"),
            ("issued", "i.IssuedDate"),
            ("due", "i.DueDate"),
            ("total", "i.Total"),
            ("paid", "i.AmountPaid"),
            ("balance", "(i.Total - i.AmountPaid)"),
            ("status", "i.Status"));

        public InvoiceRepository(IDbSession session)
            : base(session)
        {
        }

        public Task<Invoice?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            return QueryOneAsync("WHERE i.Id = @Id", new { Id = id }, cancellationToken);
        }

        public Task<Invoice?> GetByNumberAsync(string invoiceNumber, CancellationToken cancellationToken = default)
        {
            return QueryOneAsync("WHERE i.InvoiceNumber = @Number", new { Number = invoiceNumber.Trim() }, cancellationToken);
        }

        public Task<Invoice?> GetLiveByOrderIdAsync(int orderId, CancellationToken cancellationToken = default)
        {
            return QueryOneAsync("WHERE i.OrderId = @OrderId AND i.Status <> 'Void'", new { OrderId = orderId }, cancellationToken);
        }

        public Task<PagedResult<InvoiceSummary>> SearchAsync(InvoiceFilter filter, PagedRequest paging, CancellationToken cancellationToken = default)
        {
            var where = new List<string>();
            var parameters = new DynamicParameters();
            parameters.Add("@Today", DateTime.UtcNow.Date, DbType.Date);

            string? search = StartsWith(filter.Search);
            if (search != null)
            {
                where.Add("(i.InvoiceNumber LIKE @Search OR o.OrderNumber LIKE @Search OR c.CustomerNumber LIKE @Search OR c.Name LIKE @Search)");
                parameters.Add("@Search", search);
            }

            if (filter.Status.HasValue)
            {
                where.Add("i.Status = @Status");
                parameters.Add("@Status", filter.Status.Value.ToString());
            }

            if (filter.CustomerId.HasValue)
            {
                where.Add("i.CustomerId = @CustomerId");
                parameters.Add("@CustomerId", filter.CustomerId.Value);
            }

            if (filter.OverdueOnly)
            {
                where.Add("i.Status IN ('Open', 'PartiallyPaid') AND i.DueDate < @Today");
            }

            if (filter.FromDate.HasValue)
            {
                where.Add("i.IssuedDate >= @FromDate");
                parameters.Add("@FromDate", filter.FromDate.Value.Date, DbType.Date);
            }

            if (filter.ToDate.HasValue)
            {
                where.Add("i.IssuedDate <= @ToDate");
                parameters.Add("@ToDate", filter.ToDate.Value.Date, DbType.Date);
            }

            string fromAndWhere = SummaryFrom + (where.Count == 0 ? string.Empty : " WHERE " + string.Join(" AND ", where));
            string orderBy = ResolveSort(paging, SortColumns, "i.IssuedDate DESC, i.Id DESC", "i.Id DESC");

            return QueryPagedAsync<InvoiceSummary>(SummaryColumns, fromAndWhere, orderBy, parameters, paging, cancellationToken);
        }

        public async Task<IReadOnlyList<InvoiceSummary>> GetForCustomerAsync(int customerId, CancellationToken cancellationToken = default)
        {
            string sql = "SELECT " + SummaryColumns + " " + SummaryFrom + " WHERE i.CustomerId = @CustomerId ORDER BY i.IssuedDate DESC, i.Id DESC;";

            IDbConnection connection = await ConnectionAsync(cancellationToken).ConfigureAwait(false);
            return (await connection.QueryAsync<InvoiceSummary>(
                Text(sql, new { CustomerId = customerId, Today = DateTime.UtcNow.Date }, cancellationToken)).ConfigureAwait(false)).ToList();
        }

        public async Task<Result<int>> CreateFromOrderAsync(int orderId, byte[] orderRowVersion, string userName, DateTime? issuedDate, CancellationToken cancellationToken = default)
        {
            var parameters = new DynamicParameters();
            parameters.Add("@OrderId", orderId);
            parameters.Add("@CreatedBy", userName);
            parameters.Add("@IssuedDate", issuedDate?.Date, DbType.Date);
            parameters.Add("@RowVersion", orderRowVersion, DbType.Binary, size: 8);
            parameters.Add("@InvoiceId", dbType: DbType.Int32, direction: ParameterDirection.Output);

            try
            {
                IDbConnection connection = await ConnectionAsync(cancellationToken).ConfigureAwait(false);
                await connection.ExecuteAsync(Procedure("dbo.usp_Invoice_CreateFromOrder", parameters, cancellationToken)).ConfigureAwait(false);
                return Result<int>.Success(parameters.Get<int>("@InvoiceId"));
            }
            catch (SqlException ex) when (SqlErrorMapper.IsBusinessError(ex))
            {
                return SqlErrorMapper.ToResult<int>(ex);
            }
        }

        public async Task<Result<int>> ApplyPaymentAsync(Payment payment, byte[] invoiceRowVersion, string userName, CancellationToken cancellationToken = default)
        {
            var parameters = new DynamicParameters();
            parameters.Add("@InvoiceId", payment.InvoiceId);
            parameters.Add("@Amount", payment.Amount, DbType.Decimal, precision: 18, scale: 2);
            parameters.Add("@PaidDate", payment.PaidDate.Date, DbType.Date);
            parameters.Add("@Method", payment.Method.ToString());
            parameters.Add("@CreatedBy", userName);
            parameters.Add("@Reference", payment.Reference);
            parameters.Add("@Notes", payment.Notes);
            parameters.Add("@RowVersion", invoiceRowVersion, DbType.Binary, size: 8);
            parameters.Add("@PaymentId", dbType: DbType.Int32, direction: ParameterDirection.Output);

            try
            {
                IDbConnection connection = await ConnectionAsync(cancellationToken).ConfigureAwait(false);
                await connection.ExecuteAsync(Procedure("dbo.usp_Invoice_ApplyPayment", parameters, cancellationToken)).ConfigureAwait(false);

                int paymentId = parameters.Get<int>("@PaymentId");
                payment.Id = paymentId;
                payment.CreatedBy = userName;
                payment.UpdatedBy = userName;
                return Result<int>.Success(paymentId);
            }
            catch (SqlException ex) when (SqlErrorMapper.IsBusinessError(ex))
            {
                return SqlErrorMapper.ToResult<int>(ex);
            }
        }

        public async Task<Result> VoidAsync(int invoiceId, byte[] invoiceRowVersion, string userName, string? comment, CancellationToken cancellationToken = default)
        {
            var parameters = new DynamicParameters();
            parameters.Add("@InvoiceId", invoiceId);
            parameters.Add("@ChangedBy", userName);
            parameters.Add("@Comment", comment);
            parameters.Add("@RowVersion", invoiceRowVersion, DbType.Binary, size: 8);

            try
            {
                IDbConnection connection = await ConnectionAsync(cancellationToken).ConfigureAwait(false);
                await connection.ExecuteAsync(Procedure("dbo.usp_Invoice_Void", parameters, cancellationToken)).ConfigureAwait(false);
                return Result.Success();
            }
            catch (SqlException ex) when (SqlErrorMapper.IsBusinessError(ex))
            {
                return SqlErrorMapper.ToResult(ex);
            }
        }

        private async Task<Invoice?> QueryOneAsync(string where, object parameters, CancellationToken cancellationToken)
        {
            string sql =
                "SELECT " + InvoiceColumns + " FROM dbo.Invoices i " + where + ";" +
                "SELECT " + PaymentColumns + " FROM dbo.Payments p WHERE p.InvoiceId IN (SELECT i.Id FROM dbo.Invoices i " + where + ") ORDER BY p.PaidDate, p.Id;";

            IDbConnection connection = await ConnectionAsync(cancellationToken).ConfigureAwait(false);

            using (SqlMapper.GridReader grid = await connection.QueryMultipleAsync(Text(sql, parameters, cancellationToken)).ConfigureAwait(false))
            {
                Invoice? invoice = (await grid.ReadAsync<Invoice>().ConfigureAwait(false)).FirstOrDefault();
                if (invoice == null)
                {
                    return null;
                }

                invoice.Payments = (await grid.ReadAsync<Payment>().ConfigureAwait(false)).ToList();
                return invoice;
            }
        }
    }
}
