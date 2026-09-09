using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Coms.Data.Connections;
using Coms.Domain.Common;
using Coms.Domain.Customers;
using Dapper;

namespace Coms.Data.Repositories
{
    public sealed class CustomerRepository : RepositoryBase, ICustomerRepository
    {
        /* The two address blocks are aliased to the Address property names so
           Dapper can multi-map them; splitOn: "Line1,Line1" marks where each
           block starts. */
        private const string SelectColumns = @"
            c.Id, c.CustomerNumber, c.Name, c.ContactName, c.Email, c.Phone,
            c.PaymentTermsDays, c.CreditLimit, c.Status, c.Notes,
            c.CreatedAtUtc, c.CreatedBy, c.UpdatedAtUtc, c.UpdatedBy, c.RowVersion,
            c.BillingLine1 AS Line1, c.BillingLine2 AS Line2, c.BillingCity AS City,
            c.BillingRegion AS Region, c.BillingPostalCode AS PostalCode, c.BillingCountry AS Country,
            c.ShippingLine1 AS Line1, c.ShippingLine2 AS Line2, c.ShippingCity AS City,
            c.ShippingRegion AS Region, c.ShippingPostalCode AS PostalCode, c.ShippingCountry AS Country";

        private const string SplitOn = "Line1,Line1";

        private static readonly IReadOnlyDictionary<string, string> SortColumns = SortMap(
            ("number", "c.CustomerNumber"),
            ("name", "c.Name"),
            ("contact", "c.ContactName"),
            ("city", "c.BillingCity"),
            ("terms", "c.PaymentTermsDays"),
            ("status", "c.Status"),
            ("updated", "c.UpdatedAtUtc"));

        public CustomerRepository(IDbSession session)
            : base(session)
        {
        }

        public async Task<Customer?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            return await QueryOneAsync("WHERE c.Id = @Id", new { Id = id }, cancellationToken).ConfigureAwait(false);
        }

        public async Task<Customer?> GetByNumberAsync(string customerNumber, CancellationToken cancellationToken = default)
        {
            return await QueryOneAsync("WHERE c.CustomerNumber = @Number", new { Number = customerNumber }, cancellationToken).ConfigureAwait(false);
        }

        public async Task<PagedResult<Customer>> SearchAsync(CustomerFilter filter, PagedRequest paging, CancellationToken cancellationToken = default)
        {
            var where = new List<string>();
            var parameters = new DynamicParameters();

            string? search = StartsWith(filter.Search);
            if (search != null)
            {
                where.Add("(c.CustomerNumber LIKE @Search OR c.Name LIKE @Search OR c.Email LIKE @Search)");
                parameters.Add("@Search", search);
            }

            if (filter.Status.HasValue)
            {
                where.Add("c.Status = @Status");
                parameters.Add("@Status", filter.Status.Value.ToString());
            }

            string fromAndWhere = "FROM dbo.Customers c" + (where.Count == 0 ? string.Empty : " WHERE " + string.Join(" AND ", where));
            string orderBy = ResolveSort(paging, SortColumns, "c.Name ASC, c.Id", "c.Id");

            string sql =
                "SELECT " + SelectColumns + " " + fromAndWhere +
                " ORDER BY " + orderBy +
                " OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;" +
                "SELECT COUNT(*) " + fromAndWhere + ";";

            parameters.Add("@Offset", paging.Offset);
            parameters.Add("@PageSize", paging.PageSize);

            IDbConnection connection = await ConnectionAsync(cancellationToken).ConfigureAwait(false);

            using (SqlMapper.GridReader grid = await connection.QueryMultipleAsync(Text(sql, parameters, cancellationToken)).ConfigureAwait(false))
            {
                List<Customer> items = grid.Read<Customer, Address, Address, Customer>(Map, SplitOn).ToList();
                int total = await grid.ReadSingleAsync<int>().ConfigureAwait(false);
                return new PagedResult<Customer>(items, total, paging.Page, paging.PageSize);
            }
        }

        public async Task InsertAsync(Customer customer, string userName, CancellationToken cancellationToken = default)
        {
            const string sql = @"
                DECLARE @number VARCHAR(12);
                EXEC dbo.usp_NextCustomerNumber @CustomerNumber = @number OUTPUT;

                INSERT dbo.Customers
                    (CustomerNumber, Name, ContactName, Email, Phone,
                     BillingLine1, BillingLine2, BillingCity, BillingRegion, BillingPostalCode, BillingCountry,
                     ShippingLine1, ShippingLine2, ShippingCity, ShippingRegion, ShippingPostalCode, ShippingCountry,
                     PaymentTermsDays, CreditLimit, Status, Notes,
                     CreatedAtUtc, CreatedBy, UpdatedAtUtc, UpdatedBy)
                VALUES
                    (@number, @Name, @ContactName, @Email, @Phone,
                     @BillingLine1, @BillingLine2, @BillingCity, @BillingRegion, @BillingPostalCode, @BillingCountry,
                     @ShippingLine1, @ShippingLine2, @ShippingCity, @ShippingRegion, @ShippingPostalCode, @ShippingCountry,
                     @PaymentTermsDays, @CreditLimit, @Status, @Notes,
                     SYSUTCDATETIME(), @UserName, SYSUTCDATETIME(), @UserName);

                SELECT Id, CustomerNumber, CreatedAtUtc, UpdatedAtUtc, RowVersion
                  FROM dbo.Customers
                 WHERE Id = SCOPE_IDENTITY();";

            IDbConnection connection = await ConnectionAsync(cancellationToken).ConfigureAwait(false);
            InsertedRow row = await connection.QuerySingleAsync<InsertedRow>(Text(sql, ToParameters(customer, userName), cancellationToken)).ConfigureAwait(false);

            customer.Id = row.Id;
            customer.CustomerNumber = row.CustomerNumber;
            customer.CreatedAtUtc = row.CreatedAtUtc;
            customer.CreatedBy = userName;
            customer.UpdatedAtUtc = row.UpdatedAtUtc;
            customer.UpdatedBy = userName;
            customer.RowVersion = row.RowVersion;
        }

        public async Task<bool> UpdateAsync(Customer customer, string userName, CancellationToken cancellationToken = default)
        {
            const string sql = @"
                UPDATE dbo.Customers
                   SET Name = @Name, ContactName = @ContactName, Email = @Email, Phone = @Phone,
                       BillingLine1 = @BillingLine1, BillingLine2 = @BillingLine2, BillingCity = @BillingCity,
                       BillingRegion = @BillingRegion, BillingPostalCode = @BillingPostalCode, BillingCountry = @BillingCountry,
                       ShippingLine1 = @ShippingLine1, ShippingLine2 = @ShippingLine2, ShippingCity = @ShippingCity,
                       ShippingRegion = @ShippingRegion, ShippingPostalCode = @ShippingPostalCode, ShippingCountry = @ShippingCountry,
                       PaymentTermsDays = @PaymentTermsDays, CreditLimit = @CreditLimit, Status = @Status, Notes = @Notes,
                       UpdatedAtUtc = SYSUTCDATETIME(), UpdatedBy = @UserName
                OUTPUT INSERTED.UpdatedAtUtc, INSERTED.RowVersion
                 WHERE Id = @Id AND RowVersion = @RowVersion;";

            IDbConnection connection = await ConnectionAsync(cancellationToken).ConfigureAwait(false);
            UpdatedRow? row = await connection.QuerySingleOrDefaultAsync<UpdatedRow>(Text(sql, ToParameters(customer, userName), cancellationToken)).ConfigureAwait(false);

            if (row == null)
            {
                return false;
            }

            customer.UpdatedAtUtc = row.UpdatedAtUtc;
            customer.UpdatedBy = userName;
            customer.RowVersion = row.RowVersion;
            return true;
        }

        public async Task<CustomerBalance?> GetBalanceAsync(int customerId, CancellationToken cancellationToken = default)
        {
            const string sql = @"
                SELECT CustomerId, CustomerNumber, Name, Status, PaymentTermsDays, CreditLimit,
                       OpenInvoiceCount, OutstandingBalance, OverdueBalance, OldestDueDate,
                       CommittedOrderCount, CommittedTotal, Exposure, CreditAvailable
                  FROM rpt.vw_CustomerBalance
                 WHERE CustomerId = @Id;";

            IDbConnection connection = await ConnectionAsync(cancellationToken).ConfigureAwait(false);
            return await connection.QuerySingleOrDefaultAsync<CustomerBalance>(Text(sql, new { Id = customerId }, cancellationToken)).ConfigureAwait(false);
        }

        private async Task<Customer?> QueryOneAsync(string where, object parameters, CancellationToken cancellationToken)
        {
            string sql = "SELECT " + SelectColumns + " FROM dbo.Customers c " + where + ";";

            IDbConnection connection = await ConnectionAsync(cancellationToken).ConfigureAwait(false);
            IEnumerable<Customer> rows = await connection.QueryAsync<Customer, Address, Address, Customer>(
                Text(sql, parameters, cancellationToken), Map, SplitOn).ConfigureAwait(false);

            return rows.FirstOrDefault();
        }

        private static Customer Map(Customer customer, Address billing, Address? shipping)
        {
            customer.BillingAddress = billing;
            customer.ShippingAddress = shipping == null || shipping.IsEmpty ? null : shipping;
            return customer;
        }

        private static object ToParameters(Customer c, string userName)
        {
            Address? ship = c.ShippingAddress;

            return new
            {
                c.Id,
                c.RowVersion,
                c.Name,
                c.ContactName,
                c.Email,
                c.Phone,
                BillingLine1 = c.BillingAddress.Line1,
                BillingLine2 = c.BillingAddress.Line2,
                BillingCity = c.BillingAddress.City,
                BillingRegion = c.BillingAddress.Region,
                BillingPostalCode = c.BillingAddress.PostalCode,
                BillingCountry = c.BillingAddress.Country,
                ShippingLine1 = ship?.Line1,
                ShippingLine2 = ship?.Line2,
                ShippingCity = ship?.City,
                ShippingRegion = ship?.Region,
                ShippingPostalCode = ship?.PostalCode,
                ShippingCountry = ship?.Country,
                c.PaymentTermsDays,
                c.CreditLimit,
                Status = c.Status.ToString(),
                c.Notes,
                UserName = userName
            };
        }

        private sealed class InsertedRow
        {
            public int Id { get; set; }
            public string CustomerNumber { get; set; } = string.Empty;
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
