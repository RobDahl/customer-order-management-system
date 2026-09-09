using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Coms.Domain.Invoices
{
    /// <summary>Read-only: payments are written by dbo.usp_Invoice_ApplyPayment.</summary>
    public interface IPaymentRepository
    {
        Task<Payment?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<Payment>> GetForInvoiceAsync(int invoiceId, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<Payment>> GetForCustomerAsync(int customerId, CancellationToken cancellationToken = default);
    }
}
