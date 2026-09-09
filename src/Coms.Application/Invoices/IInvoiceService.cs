using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Coms.Domain.Common;
using Coms.Domain.Invoices;

namespace Coms.Application.Invoices
{
    public interface IInvoiceService
    {
        Task<Invoice?> GetAsync(int id, CancellationToken cancellationToken = default);

        Task<Invoice?> GetByNumberAsync(string invoiceNumber, CancellationToken cancellationToken = default);

        Task<Invoice?> GetLiveForOrderAsync(int orderId, CancellationToken cancellationToken = default);

        Task<PagedResult<InvoiceSummary>> SearchAsync(InvoiceFilter filter, PagedRequest paging, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<InvoiceSummary>> GetForCustomerAsync(int customerId, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<Payment>> GetPaymentsForCustomerAsync(int customerId, CancellationToken cancellationToken = default);

        Task<Result<Invoice>> CreateFromOrderAsync(int orderId, byte[] orderRowVersion, DateTime? issuedDate, CancellationToken cancellationToken = default);

        Task<Result<Invoice>> ApplyPaymentAsync(Payment payment, byte[] invoiceRowVersion, CancellationToken cancellationToken = default);

        Task<Result<Invoice>> VoidAsync(int invoiceId, byte[] invoiceRowVersion, string? comment, CancellationToken cancellationToken = default);
    }
}
