using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Coms.Data.Connections;
using Coms.Domain.Common;
using Coms.Domain.Orders;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Coms.Data.Repositories
{
    public sealed class OrderRepository : RepositoryBase, IOrderRepository
    {
        private const string HeaderColumns = @"
            o.Id, o.OrderNumber, o.CustomerId, o.Status, o.OrderDate, o.RequiredDate, o.CustomerReference,
            o.Subtotal, o.TaxRate, o.TaxAmount, o.Total, o.Notes,
            o.CreatedAtUtc, o.CreatedBy, o.UpdatedAtUtc, o.UpdatedBy, o.RowVersion,
            o.ShipToLine1 AS Line1, o.ShipToLine2 AS Line2, o.ShipToCity AS City,
            o.ShipToRegion AS Region, o.ShipToPostalCode AS PostalCode, o.ShipToCountry AS Country";

        private const string LineColumns = @"
            l.Id, l.OrderId, l.LineNumber, l.ProductId, l.Sku, l.Description,
            l.Quantity, l.UnitPrice, l.UnitCost, l.DiscountPercent, l.LineTotal";

        private const string HistoryColumns = @"
            h.Id, h.OrderId, h.FromStatus, h.ToStatus, h.ChangedAtUtc, h.ChangedBy, h.Comment";

        private const string SummaryColumns = @"
            s.OrderId, s.OrderNumber, s.Status, s.OrderDate, s.RequiredDate, s.CustomerReference,
            s.CustomerId, s.CustomerNumber, s.CustomerName,
            s.Subtotal, s.TaxRate, s.TaxAmount, s.Total, s.LineCount,
            s.InvoiceId, s.InvoiceNumber, s.InvoiceStatus, s.UpdatedAtUtc, s.UpdatedBy";

        private static readonly IReadOnlyDictionary<string, string> SortColumns = SortMap(
            ("number", "s.OrderNumber"),
            ("date", "s.OrderDate"),
            ("required", "s.RequiredDate"),
            ("customer", "s.CustomerName"),
            ("status", "s.Status"),
            ("total", "s.Total"),
            ("updated", "s.UpdatedAtUtc"));

        public OrderRepository(IDbSession session)
            : base(session)
        {
        }

        public async Task<Order?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            string sql =
                "SELECT " + HeaderColumns + " FROM dbo.Orders o WHERE o.Id = @Id;" +
                "SELECT " + LineColumns + " FROM dbo.OrderLines l WHERE l.OrderId = @Id ORDER BY l.LineNumber;" +
                "SELECT " + HistoryColumns + " FROM dbo.OrderStatusHistory h WHERE h.OrderId = @Id ORDER BY h.ChangedAtUtc, h.Id;";

            IDbConnection connection = await ConnectionAsync(cancellationToken).ConfigureAwait(false);

            using (SqlMapper.GridReader grid = await connection.QueryMultipleAsync(Text(sql, new { Id = id }, cancellationToken)).ConfigureAwait(false))
            {
                Order? order = grid.Read<Order, Address, Order>(MapHeader, "Line1").FirstOrDefault();
                if (order == null)
                {
                    return null;
                }

                order.Lines = (await grid.ReadAsync<OrderLine>().ConfigureAwait(false)).ToList();
                order.History = (await grid.ReadAsync<OrderStatusChange>().ConfigureAwait(false)).ToList();
                return order;
            }
        }

        public async Task<Order?> GetByNumberAsync(string orderNumber, CancellationToken cancellationToken = default)
        {
            IDbConnection connection = await ConnectionAsync(cancellationToken).ConfigureAwait(false);
            int? id = await connection.QuerySingleOrDefaultAsync<int?>(
                Text("SELECT Id FROM dbo.Orders WHERE OrderNumber = @Number;", new { Number = orderNumber.Trim() }, cancellationToken)).ConfigureAwait(false);

            return id.HasValue ? await GetByIdAsync(id.Value, cancellationToken).ConfigureAwait(false) : null;
        }

        public Task<PagedResult<OrderSummary>> SearchAsync(OrderFilter filter, PagedRequest paging, CancellationToken cancellationToken = default)
        {
            var where = new List<string>();
            var parameters = new DynamicParameters();

            string? search = StartsWith(filter.Search);
            if (search != null)
            {
                where.Add("(s.OrderNumber LIKE @Search OR s.CustomerNumber LIKE @Search OR s.CustomerName LIKE @Search OR s.CustomerReference LIKE @Search)");
                parameters.Add("@Search", search);
            }

            if (filter.Status.HasValue)
            {
                where.Add("s.Status = @Status");
                parameters.Add("@Status", filter.Status.Value.ToString());
            }

            if (filter.CustomerId.HasValue)
            {
                where.Add("s.CustomerId = @CustomerId");
                parameters.Add("@CustomerId", filter.CustomerId.Value);
            }

            if (filter.FromDate.HasValue)
            {
                where.Add("s.OrderDate >= @FromDate");
                parameters.Add("@FromDate", filter.FromDate.Value.Date, DbType.Date);
            }

            if (filter.ToDate.HasValue)
            {
                where.Add("s.OrderDate <= @ToDate");
                parameters.Add("@ToDate", filter.ToDate.Value.Date, DbType.Date);
            }

            string fromAndWhere = "FROM rpt.vw_OrderSummary s" + (where.Count == 0 ? string.Empty : " WHERE " + string.Join(" AND ", where));
            string orderBy = ResolveSort(paging, SortColumns, "s.OrderDate DESC, s.OrderId DESC", "s.OrderId DESC");

            return QueryPagedAsync<OrderSummary>(SummaryColumns, fromAndWhere, orderBy, parameters, paging, cancellationToken);
        }

        public async Task<IReadOnlyList<OrderSummary>> GetRecentAsync(int count, CancellationToken cancellationToken = default)
        {
            string sql = "SELECT TOP (@Count) " + SummaryColumns + " FROM rpt.vw_OrderSummary s ORDER BY s.OrderDate DESC, s.OrderId DESC;";

            IDbConnection connection = await ConnectionAsync(cancellationToken).ConfigureAwait(false);
            return (await connection.QueryAsync<OrderSummary>(Text(sql, new { Count = Math.Max(1, count) }, cancellationToken)).ConfigureAwait(false)).ToList();
        }

        public async Task<IReadOnlyList<OrderStatusChange>> GetHistoryAsync(int orderId, CancellationToken cancellationToken = default)
        {
            string sql = "SELECT " + HistoryColumns + " FROM dbo.OrderStatusHistory h WHERE h.OrderId = @Id ORDER BY h.ChangedAtUtc, h.Id;";

            IDbConnection connection = await ConnectionAsync(cancellationToken).ConfigureAwait(false);
            return (await connection.QueryAsync<OrderStatusChange>(Text(sql, new { Id = orderId }, cancellationToken)).ConfigureAwait(false)).ToList();
        }

        public Task<PagedResult<OrderStatusChange>> GetRecentHistoryAsync(PagedRequest paging, CancellationToken cancellationToken = default)
        {
            return QueryPagedAsync<OrderStatusChange>(
                HistoryColumns,
                "FROM dbo.OrderStatusHistory h",
                "h.ChangedAtUtc DESC, h.Id DESC",
                new DynamicParameters(),
                paging,
                cancellationToken);
        }

        public Task InsertAsync(Order order, string userName, CancellationToken cancellationToken = default)
        {
            const string headerSql = @"
                DECLARE @number VARCHAR(16);
                EXEC dbo.usp_NextOrderNumber @SeriesYear = @SeriesYear, @OrderNumber = @number OUTPUT;

                INSERT dbo.Orders
                    (OrderNumber, CustomerId, Status, OrderDate, RequiredDate, CustomerReference,
                     ShipToLine1, ShipToLine2, ShipToCity, ShipToRegion, ShipToPostalCode, ShipToCountry,
                     Subtotal, TaxRate, TaxAmount, Total, Notes,
                     CreatedAtUtc, CreatedBy, UpdatedAtUtc, UpdatedBy)
                VALUES
                    (@number, @CustomerId, @Status, @OrderDate, @RequiredDate, @CustomerReference,
                     @ShipToLine1, @ShipToLine2, @ShipToCity, @ShipToRegion, @ShipToPostalCode, @ShipToCountry,
                     @Subtotal, @TaxRate, @TaxAmount, @Total, @Notes,
                     SYSUTCDATETIME(), @UserName, SYSUTCDATETIME(), @UserName);

                DECLARE @newOrderId INT = SCOPE_IDENTITY();

                INSERT dbo.OrderStatusHistory (OrderId, FromStatus, ToStatus, ChangedAtUtc, ChangedBy, Comment)
                VALUES (@newOrderId, NULL, @Status, SYSUTCDATETIME(), @UserName, NULL);

                SELECT Id, OrderNumber, CreatedAtUtc, UpdatedAtUtc, RowVersion FROM dbo.Orders WHERE Id = @newOrderId;";

            return InTransactionAsync<object?>(async () =>
            {
                IDbConnection connection = await ConnectionAsync(cancellationToken).ConfigureAwait(false);

                DynamicParameters parameters = HeaderParameters(order, userName);
                parameters.Add("@SeriesYear", (short)order.OrderDate.Year, DbType.Int16);

                InsertedRow row = await connection.QuerySingleAsync<InsertedRow>(Text(headerSql, parameters, cancellationToken)).ConfigureAwait(false);

                order.Id = row.Id;
                order.OrderNumber = row.OrderNumber;
                order.CreatedAtUtc = row.CreatedAtUtc;
                order.CreatedBy = userName;
                order.UpdatedAtUtc = row.UpdatedAtUtc;
                order.UpdatedBy = userName;
                order.RowVersion = row.RowVersion;

                await InsertLinesAsync(connection, order, cancellationToken).ConfigureAwait(false);

                order.History = (await GetHistoryAsync(order.Id, cancellationToken).ConfigureAwait(false)).ToList();
                return null;
            }, cancellationToken);
        }

        public Task<bool> UpdateAsync(Order order, string userName, CancellationToken cancellationToken = default)
        {
            const string headerSql = @"
                UPDATE dbo.Orders
                   SET CustomerId = @CustomerId, OrderDate = @OrderDate, RequiredDate = @RequiredDate, CustomerReference = @CustomerReference,
                       ShipToLine1 = @ShipToLine1, ShipToLine2 = @ShipToLine2, ShipToCity = @ShipToCity,
                       ShipToRegion = @ShipToRegion, ShipToPostalCode = @ShipToPostalCode, ShipToCountry = @ShipToCountry,
                       Subtotal = @Subtotal, TaxRate = @TaxRate, TaxAmount = @TaxAmount, Total = @Total, Notes = @Notes,
                       UpdatedAtUtc = SYSUTCDATETIME(), UpdatedBy = @UserName
                OUTPUT INSERTED.UpdatedAtUtc, INSERTED.RowVersion
                 WHERE Id = @Id AND RowVersion = @RowVersion;";

            return InTransactionAsync(async () =>
            {
                IDbConnection connection = await ConnectionAsync(cancellationToken).ConfigureAwait(false);

                UpdatedRow? row = await connection.QuerySingleOrDefaultAsync<UpdatedRow>(
                    Text(headerSql, HeaderParameters(order, userName), cancellationToken)).ConfigureAwait(false);

                if (row == null)
                {
                    return false;
                }

                await connection.ExecuteAsync(Text("DELETE dbo.OrderLines WHERE OrderId = @Id;", new { order.Id }, cancellationToken)).ConfigureAwait(false);
                await InsertLinesAsync(connection, order, cancellationToken).ConfigureAwait(false);

                order.UpdatedAtUtc = row.UpdatedAtUtc;
                order.UpdatedBy = userName;
                order.RowVersion = row.RowVersion;
                return true;
            }, cancellationToken);
        }

        public Task<bool> ChangeStatusAsync(int orderId, OrderStatus from, OrderStatus to, byte[] rowVersion, string userName, string? comment, CancellationToken cancellationToken = default)
        {
            const string sql = @"
                DECLARE @rows INT;

                UPDATE dbo.Orders
                   SET Status = @To, UpdatedAtUtc = SYSUTCDATETIME(), UpdatedBy = @UserName
                 WHERE Id = @OrderId AND Status = @From AND RowVersion = @RowVersion;

                SET @rows = @@ROWCOUNT;

                IF @rows = 1
                    INSERT dbo.OrderStatusHistory (OrderId, FromStatus, ToStatus, ChangedAtUtc, ChangedBy, Comment)
                    VALUES (@OrderId, @From, @To, SYSUTCDATETIME(), @UserName, @Comment);

                SELECT @rows;";

            return InTransactionAsync(async () =>
            {
                IDbConnection connection = await ConnectionAsync(cancellationToken).ConfigureAwait(false);

                var parameters = new DynamicParameters();
                parameters.Add("@OrderId", orderId);
                parameters.Add("@From", from.ToString());
                parameters.Add("@To", to.ToString());
                parameters.Add("@RowVersion", rowVersion, DbType.Binary, size: 8);
                parameters.Add("@UserName", userName);
                parameters.Add("@Comment", comment);

                int rows = await connection.ExecuteScalarAsync<int>(Text(sql, parameters, cancellationToken)).ConfigureAwait(false);
                return rows == 1;
            }, cancellationToken);
        }

        public async Task<Result> FulfilAsync(int orderId, byte[] rowVersion, string userName, string? comment, bool allowNegativeStock, CancellationToken cancellationToken = default)
        {
            var parameters = new DynamicParameters();
            parameters.Add("@OrderId", orderId);
            parameters.Add("@ChangedBy", userName);
            parameters.Add("@Comment", comment);
            parameters.Add("@AllowNegativeStock", allowNegativeStock);
            parameters.Add("@RowVersion", rowVersion, DbType.Binary, size: 8);

            try
            {
                IDbConnection connection = await ConnectionAsync(cancellationToken).ConfigureAwait(false);
                await connection.ExecuteAsync(Procedure("dbo.usp_Order_Fulfil", parameters, cancellationToken)).ConfigureAwait(false);
                return Result.Success();
            }
            catch (SqlException ex) when (SqlErrorMapper.IsBusinessError(ex))
            {
                return SqlErrorMapper.ToResult(ex);
            }
        }

        private async Task InsertLinesAsync(IDbConnection connection, Order order, CancellationToken cancellationToken)
        {
            const string sql = @"
                INSERT dbo.OrderLines (OrderId, LineNumber, ProductId, Sku, Description, Quantity, UnitPrice, UnitCost, DiscountPercent, LineTotal)
                OUTPUT INSERTED.Id
                VALUES (@OrderId, @LineNumber, @ProductId, @Sku, @Description, @Quantity, @UnitPrice, @UnitCost, @DiscountPercent, @LineTotal);";

            foreach (OrderLine line in order.Lines)
            {
                line.OrderId = order.Id;
                line.Id = await connection.QuerySingleAsync<int>(Text(sql, line, cancellationToken)).ConfigureAwait(false);
            }
        }

        private static Order MapHeader(Order order, Address? shipTo)
        {
            order.ShipTo = shipTo == null || shipTo.IsEmpty ? null : shipTo;
            return order;
        }

        private static DynamicParameters HeaderParameters(Order o, string userName)
        {
            Address? ship = o.ShipTo;

            var p = new DynamicParameters();
            p.Add("@Id", o.Id);
            p.Add("@RowVersion", o.RowVersion, DbType.Binary, size: 8);
            p.Add("@CustomerId", o.CustomerId);
            p.Add("@Status", o.Status.ToString());
            p.Add("@OrderDate", o.OrderDate.Date, DbType.Date);
            p.Add("@RequiredDate", o.RequiredDate?.Date, DbType.Date);
            p.Add("@CustomerReference", o.CustomerReference);
            p.Add("@ShipToLine1", ship?.Line1);
            p.Add("@ShipToLine2", ship?.Line2);
            p.Add("@ShipToCity", ship?.City);
            p.Add("@ShipToRegion", ship?.Region);
            p.Add("@ShipToPostalCode", ship?.PostalCode);
            p.Add("@ShipToCountry", ship?.Country);
            p.Add("@Subtotal", o.Subtotal);
            p.Add("@TaxRate", o.TaxRate);
            p.Add("@TaxAmount", o.TaxAmount);
            p.Add("@Total", o.Total);
            p.Add("@Notes", o.Notes);
            p.Add("@UserName", userName);
            return p;
        }

        private sealed class InsertedRow
        {
            public int Id { get; set; }
            public string OrderNumber { get; set; } = string.Empty;
            public DateTime CreatedAtUtc { get; set; }
            public DateTime UpdatedAtUtc { get; set; }
            public byte[] RowVersion { get; set; } = Array.Empty<byte>();
        }

        private sealed class UpdatedRow
        {
            public DateTime UpdatedAtUtc { get; set; }
            public byte[] RowVersion { get; set; } = Array.Empty<byte>();
        }
    }
}
