using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Coms.Domain.Common;

namespace Coms.Domain.Invoices
{
    public interface IInvoiceRepository
    {
        /// <summary>The invoice with its payments.</summary>
        Task<Invoice?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

        Task<Invoice?> GetByNumberAsync(string invoiceNumber, CancellationToken cancellationToken = default);

        /// <summary>The live (non-void) invoice for an order, if any.</summary>
        Task<Invoice?> GetLiveByOrderIdAsync(int orderId, CancellationToken cancellationToken = default);

        Task<PagedResult<InvoiceSummary>> SearchAsync(InvoiceFilter filter, PagedRequest paging, CancellationToken cancellationToken = default);

        /// <summary>All invoices for a customer, newest first, for the statement page.</summary>
        Task<IReadOnlyList<InvoiceSummary>> GetForCustomerAsync(int customerId, CancellationToken cancellationToken = default);

        /// <summary>Runs dbo.usp_Invoice_CreateFromOrder and returns the new invoice id.</summary>
        Task<Result<int>> CreateFromOrderAsync(int orderId, byte[] orderRowVersion, string userName, DateTime? issuedDate, CancellationToken cancellationToken = default);

        /// <summary>Runs dbo.usp_Invoice_ApplyPayment and returns the new payment id.</summary>
        Task<Result<int>> ApplyPaymentAsync(Payment payment, byte[] invoiceRowVersion, string userName, CancellationToken cancellationToken = default);

        /// <summary>Runs dbo.usp_Invoice_Void.</summary>
        Task<Result> VoidAsync(int invoiceId, byte[] invoiceRowVersion, string userName, string? comment, CancellationToken cancellationToken = default);
    }
}
