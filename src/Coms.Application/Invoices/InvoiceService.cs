using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Coms.Domain.Common;
using Coms.Domain.Invoices;
using Coms.Domain.Orders;
using Microsoft.Extensions.Logging;

namespace Coms.Application.Invoices
{
    public sealed class InvoiceService : IInvoiceService
    {
        private readonly IInvoiceRepository _invoices;
        private readonly IPaymentRepository _payments;
        private readonly IOrderRepository _orders;
        private readonly ICurrentUser _currentUser;
        private readonly ILogger<InvoiceService> _logger;

        public InvoiceService(
            IInvoiceRepository invoices,
            IPaymentRepository payments,
            IOrderRepository orders,
            ICurrentUser currentUser,
            ILogger<InvoiceService> logger)
        {
            _invoices = invoices ?? throw new ArgumentNullException(nameof(invoices));
            _payments = payments ?? throw new ArgumentNullException(nameof(payments));
            _orders = orders ?? throw new ArgumentNullException(nameof(orders));
            _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task<Invoice?> GetAsync(int id, CancellationToken cancellationToken = default)
        {
            return _invoices.GetByIdAsync(id, cancellationToken);
        }

        public Task<Invoice?> GetByNumberAsync(string invoiceNumber, CancellationToken cancellationToken = default)
        {
            return _invoices.GetByNumberAsync(invoiceNumber, cancellationToken);
        }

        public Task<Invoice?> GetLiveForOrderAsync(int orderId, CancellationToken cancellationToken = default)
        {
            return _invoices.GetLiveByOrderIdAsync(orderId, cancellationToken);
        }

        public Task<PagedResult<InvoiceSummary>> SearchAsync(InvoiceFilter filter, PagedRequest paging, CancellationToken cancellationToken = default)
        {
            return _invoices.SearchAsync(filter, paging, cancellationToken);
        }

        public Task<IReadOnlyList<InvoiceSummary>> GetForCustomerAsync(int customerId, CancellationToken cancellationToken = default)
        {
            return _invoices.GetForCustomerAsync(customerId, cancellationToken);
        }

        public Task<IReadOnlyList<Payment>> GetPaymentsForCustomerAsync(int customerId, CancellationToken cancellationToken = default)
        {
            return _payments.GetForCustomerAsync(customerId, cancellationToken);
        }

        public async Task<Result<Invoice>> CreateFromOrderAsync(int orderId, byte[] orderRowVersion, DateTime? issuedDate, CancellationToken cancellationToken = default)
        {
            Order? order = await _orders.GetByIdAsync(orderId, cancellationToken).ConfigureAwait(false);
            if (order == null)
            {
                return Result<Invoice>.NotFound("Order");
            }

            if (!RowVersions.Match(order.RowVersion, orderRowVersion))
            {
                return Result<Invoice>.Conflict();
            }

            if (order.Status != OrderStatus.Fulfilled)
            {
                return Result<Invoice>.Failure(ErrorCode.InvalidStatus, "Order " + order.OrderNumber + " is " + order.Status + "; only fulfilled orders can be invoiced.");
            }

            if (issuedDate.HasValue && issuedDate.Value.Date < order.OrderDate.Date)
            {
                return Result<Invoice>.Invalid("IssuedDate", "Issue date cannot be before the order date.");
            }

            Result<int> created = await _invoices.CreateFromOrderAsync(order.Id, order.RowVersion, _currentUser.UserName, issuedDate, cancellationToken).ConfigureAwait(false);
            if (created.IsFailure)
            {
                return Result<Invoice>.From(created);
            }

            Invoice? invoice = await _invoices.GetByIdAsync(created.Value, cancellationToken).ConfigureAwait(false);
            if (invoice == null)
            {
                return Result<Invoice>.NotFound("Invoice");
            }

            _logger.LogInformation("Invoice {InvoiceNumber} issued for order {OrderNumber} by {User}", invoice.InvoiceNumber, order.OrderNumber, _currentUser.UserName);
            return Result<Invoice>.Success(invoice);
        }

        public async Task<Result<Invoice>> ApplyPaymentAsync(Payment payment, byte[] invoiceRowVersion, CancellationToken cancellationToken = default)
        {
            if (payment == null)
            {
                throw new ArgumentNullException(nameof(payment));
            }

            payment.Normalize();
            IReadOnlyList<ValidationError> errors = payment.Validate();
            if (errors.Count > 0)
            {
                return Result<Invoice>.Invalid(errors);
            }

            Invoice? invoice = await _invoices.GetByIdAsync(payment.InvoiceId, cancellationToken).ConfigureAwait(false);
            if (invoice == null)
            {
                return Result<Invoice>.NotFound("Invoice");
            }

            if (!RowVersions.Match(invoice.RowVersion, invoiceRowVersion))
            {
                return Result<Invoice>.Conflict();
            }

            if (!invoice.IsOpen)
            {
                return Result<Invoice>.Failure(ErrorCode.InvoiceNotOpen, "Invoice " + invoice.InvoiceNumber + " is " + invoice.Status + " and cannot accept payments.");
            }

            if (payment.Amount > invoice.Balance)
            {
                return Result<Invoice>.Failure(ErrorCode.PaymentExceedsBalance, "Payment exceeds the outstanding balance of " + invoice.Balance.ToString("N2", System.Globalization.CultureInfo.InvariantCulture) + ".");
            }

            if (payment.PaidDate.Date < invoice.IssuedDate.Date)
            {
                return Result<Invoice>.Invalid(nameof(Payment.PaidDate), "Paid date cannot be before the invoice issue date.");
            }

            Result<int> applied = await _invoices.ApplyPaymentAsync(payment, invoice.RowVersion, _currentUser.UserName, cancellationToken).ConfigureAwait(false);
            if (applied.IsFailure)
            {
                return Result<Invoice>.From(applied);
            }

            _logger.LogInformation("Payment of {Amount} applied to {InvoiceNumber} by {User}", payment.Amount, invoice.InvoiceNumber, _currentUser.UserName);
            return await ReloadAsync(invoice.Id, cancellationToken).ConfigureAwait(false);
        }

        public async Task<Result<Invoice>> VoidAsync(int invoiceId, byte[] invoiceRowVersion, string? comment, CancellationToken cancellationToken = default)
        {
            Invoice? invoice = await _invoices.GetByIdAsync(invoiceId, cancellationToken).ConfigureAwait(false);
            if (invoice == null)
            {
                return Result<Invoice>.NotFound("Invoice");
            }

            if (!RowVersions.Match(invoice.RowVersion, invoiceRowVersion))
            {
                return Result<Invoice>.Conflict();
            }

            if (invoice.Status == InvoiceStatus.Void)
            {
                return Result<Invoice>.Failure(ErrorCode.InvoiceNotOpen, "Invoice " + invoice.InvoiceNumber + " is already void.");
            }

            if (invoice.AmountPaid > 0 || invoice.Payments.Count > 0)
            {
                return Result<Invoice>.Failure(ErrorCode.InvoiceHasPayments, "Invoice " + invoice.InvoiceNumber + " has payments recorded and cannot be voided.");
            }

            Result voided = await _invoices.VoidAsync(invoice.Id, invoice.RowVersion, _currentUser.UserName, string.IsNullOrWhiteSpace(comment) ? null : comment!.Trim(), cancellationToken).ConfigureAwait(false);
            if (voided.IsFailure)
            {
                return Result<Invoice>.From(voided);
            }

            _logger.LogInformation("Invoice {InvoiceNumber} voided by {User}", invoice.InvoiceNumber, _currentUser.UserName);
            return await ReloadAsync(invoice.Id, cancellationToken).ConfigureAwait(false);
        }

        private async Task<Result<Invoice>> ReloadAsync(int invoiceId, CancellationToken cancellationToken)
        {
            Invoice? invoice = await _invoices.GetByIdAsync(invoiceId, cancellationToken).ConfigureAwait(false);
            return invoice == null ? Result<Invoice>.NotFound("Invoice") : Result<Invoice>.Success(invoice);
        }
    }
}
