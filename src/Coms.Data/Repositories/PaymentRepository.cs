using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Coms.Data.Connections;
using Coms.Domain.Invoices;
using Dapper;

namespace Coms.Data.Repositories
{
    public sealed class PaymentRepository : RepositoryBase, IPaymentRepository
    {
        private const string Columns = @"
            p.Id, p.InvoiceId, p.PaidDate, p.Amount, p.Method, p.Reference, p.Notes,
            p.CreatedAtUtc, p.CreatedBy, p.UpdatedAtUtc, p.UpdatedBy, p.RowVersion";

        public PaymentRepository(IDbSession session)
            : base(session)
        {
        }

        public async Task<Payment?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            IDbConnection connection = await ConnectionAsync(cancellationToken).ConfigureAwait(false);
            return await connection.QuerySingleOrDefaultAsync<Payment>(
                Text("SELECT " + Columns + " FROM dbo.Payments p WHERE p.Id = @Id;", new { Id = id }, cancellationToken)).ConfigureAwait(false);
        }

        public async Task<IReadOnlyList<Payment>> GetForInvoiceAsync(int invoiceId, CancellationToken cancellationToken = default)
        {
            IDbConnection connection = await ConnectionAsync(cancellationToken).ConfigureAwait(false);
            return (await connection.QueryAsync<Payment>(
                Text("SELECT " + Columns + " FROM dbo.Payments p WHERE p.InvoiceId = @InvoiceId ORDER BY p.PaidDate, p.Id;", new { InvoiceId = invoiceId }, cancellationToken)).ConfigureAwait(false)).ToList();
        }

        public async Task<IReadOnlyList<Payment>> GetForCustomerAsync(int customerId, CancellationToken cancellationToken = default)
        {
            string sql = "SELECT " + Columns + @"
                  FROM dbo.Payments p
                  JOIN dbo.Invoices i ON i.Id = p.InvoiceId
                 WHERE i.CustomerId = @CustomerId
                 ORDER BY p.PaidDate DESC, p.Id DESC;";

            IDbConnection connection = await ConnectionAsync(cancellationToken).ConfigureAwait(false);
            return (await connection.QueryAsync<Payment>(Text(sql, new { CustomerId = customerId }, cancellationToken)).ConfigureAwait(false)).ToList();
        }
    }
}
