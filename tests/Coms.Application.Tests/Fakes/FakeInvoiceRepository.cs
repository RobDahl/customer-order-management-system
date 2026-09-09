using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Coms.Domain.Common;
using Coms.Domain.Invoices;
using Coms.Domain.Orders;

namespace Coms.Application.Tests.Fakes
{
    /// <summary>Mirrors the invoice procedures in memory.</summary>
    internal sealed class FakeInvoiceRepository : IInvoiceRepository, IPaymentRepository
    {
        private readonly FakeOrderRepository _orders;
        private readonly FakeCustomerRepository _customers;

        public FakeInvoiceRepository(FakeOrderRepository orders, FakeCustomerRepository customers)
        {
            _orders = orders;
            _customers = customers;
        }

        public List<Invoice> Invoices { get; } = new List<Invoice>();

        public Invoice Add(Invoice invoice)
        {
            invoice.Id = Invoices.Count == 0 ? 1 : Invoices.Max(i => i.Id) + 1;
            invoice.InvoiceNumber = "INV-" + invoice.IssuedDate.Year + "-" + invoice.Id.ToString("000000");
            invoice.RowVersion = Versions.Next();
            Invoices.Add(invoice);
            return invoice;
        }

        public Task<Invoice?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Invoices.FirstOrDefault(i => i.Id == id));
        }

        public Task<Invoice?> GetByNumberAsync(string invoiceNumber, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Invoices.FirstOrDefault(i => i.InvoiceNumber == invoiceNumber));
        }

        public Task<Invoice?> GetLiveByOrderIdAsync(int orderId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Invoices.FirstOrDefault(i => i.OrderId == orderId && i.Status != InvoiceStatus.Void));
        }

        public Task<PagedResult<InvoiceSummary>> SearchAsync(InvoiceFilter filter, PagedRequest paging, CancellationToken cancellationToken = default)
        {
            IEnumerable<Invoice> query = Invoices;
            if (filter.Status.HasValue)
            {
                query = query.Where(i => i.Status == filter.Status.Value);
            }

            if (filter.CustomerId.HasValue)
            {
                query = query.Where(i => i.CustomerId == filter.CustomerId.Value);
            }

            List<InvoiceSummary> all = query.Select(ToSummary).ToList();
            List<InvoiceSummary> page = all.Skip(paging.Offset).Take(paging.PageSize).ToList();
            return Task.FromResult(new PagedResult<InvoiceSummary>(page, all.Count, paging.Page, paging.PageSize));
        }

        public Task<IReadOnlyList<InvoiceSummary>> GetForCustomerAsync(int customerId, CancellationToken cancellationToken = default)
        {
            IReadOnlyList<InvoiceSummary> rows = Invoices.Where(i => i.CustomerId == customerId).Select(ToSummary).ToList();
            return Task.FromResult(rows);
        }

        public Task<Result<int>> CreateFromOrderAsync(int orderId, byte[] orderRowVersion, string userName, DateTime? issuedDate, CancellationToken cancellationToken = default)
        {
            Order? order = _orders.Orders.FirstOrDefault(o => o.Id == orderId);
            if (order == null)
            {
                return Task.FromResult(Result<int>.Failure(ErrorCode.NotFound, "Order not found."));
            }

            if (!Versions.Match(order.RowVersion, orderRowVersion))
            {
                return Task.FromResult(Result<int>.Failure(ErrorCode.Conflict, "Modified by another user."));
            }

            if (order.Status != OrderStatus.Fulfilled)
            {
                return Task.FromResult(Result<int>.Failure(ErrorCode.InvalidStatus, "Order cannot be invoiced from status '" + order.Status + "'."));
            }

            if (Invoices.Any(i => i.OrderId == orderId && i.Status != InvoiceStatus.Void))
            {
                return Task.FromResult(Result<int>.Failure(ErrorCode.InvoiceExists, "A live invoice already exists for this order."));
            }

            int terms = _customers.Customers.First(c => c.Id == order.CustomerId).PaymentTermsDays;
            DateTime issued = (issuedDate ?? DateTime.UtcNow).Date;

            Invoice invoice = Add(new Invoice
            {
                OrderId = orderId,
                CustomerId = order.CustomerId,
                IssuedDate = issued,
                DueDate = issued.AddDays(terms),
                Subtotal = order.Subtotal,
                TaxAmount = order.TaxAmount,
                Total = order.Total,
                Status = InvoiceStatus.Open,
                CreatedBy = userName
            });

            order.Status = OrderStatus.Invoiced;
            order.RowVersion = Versions.Next();
            order.History.Add(new OrderStatusChange { OrderId = orderId, FromStatus = OrderStatus.Fulfilled, ToStatus = OrderStatus.Invoiced, ChangedAtUtc = DateTime.UtcNow, ChangedBy = userName });

            return Task.FromResult(Result<int>.Success(invoice.Id));
        }

        public Task<Result<int>> ApplyPaymentAsync(Payment payment, byte[] invoiceRowVersion, string userName, CancellationToken cancellationToken = default)
        {
            Invoice? invoice = Invoices.FirstOrDefault(i => i.Id == payment.InvoiceId);
            if (invoice == null)
            {
                return Task.FromResult(Result<int>.Failure(ErrorCode.NotFound, "Invoice not found."));
            }

            if (!Versions.Match(invoice.RowVersion, invoiceRowVersion))
            {
                return Task.FromResult(Result<int>.Failure(ErrorCode.Conflict, "Modified by another user."));
            }

            if (!invoice.IsOpen)
            {
                return Task.FromResult(Result<int>.Failure(ErrorCode.InvoiceNotOpen, "Invoice cannot accept payments."));
            }

            if (invoice.AmountPaid + payment.Amount > invoice.Total)
            {
                return Task.FromResult(Result<int>.Failure(ErrorCode.PaymentExceedsBalance, "Payment exceeds the outstanding balance."));
            }

            payment.Id = invoice.Payments.Count + 1;
            payment.CreatedBy = userName;
            payment.RowVersion = Versions.Next();
            invoice.Payments.Add(payment);
            invoice.AmountPaid += payment.Amount;
            invoice.Status = invoice.AmountPaid == invoice.Total ? InvoiceStatus.Paid : InvoiceStatus.PartiallyPaid;
            invoice.RowVersion = Versions.Next();
            return Task.FromResult(Result<int>.Success(payment.Id));
        }

        public Task<Result> VoidAsync(int invoiceId, byte[] invoiceRowVersion, string userName, string? comment, CancellationToken cancellationToken = default)
        {
            Invoice? invoice = Invoices.FirstOrDefault(i => i.Id == invoiceId);
            if (invoice == null)
            {
                return Task.FromResult(Result.Failure(ErrorCode.NotFound, "Invoice not found."));
            }

            if (!Versions.Match(invoice.RowVersion, invoiceRowVersion))
            {
                return Task.FromResult(Result.Failure(ErrorCode.Conflict, "Modified by another user."));
            }

            if (invoice.Status == InvoiceStatus.Void)
            {
                return Task.FromResult(Result.Failure(ErrorCode.InvoiceNotOpen, "Already void."));
            }

            if (invoice.Payments.Count > 0)
            {
                return Task.FromResult(Result.Failure(ErrorCode.InvoiceHasPayments, "Has payments."));
            }

            invoice.Status = InvoiceStatus.Void;
            invoice.RowVersion = Versions.Next();

            Order order = _orders.Orders.First(o => o.Id == invoice.OrderId);
            order.Status = OrderStatus.Fulfilled;
            order.RowVersion = Versions.Next();
            order.History.Add(new OrderStatusChange { OrderId = order.Id, FromStatus = OrderStatus.Invoiced, ToStatus = OrderStatus.Fulfilled, ChangedAtUtc = DateTime.UtcNow, ChangedBy = userName, Comment = "Invoice " + invoice.InvoiceNumber + " voided." });

            return Task.FromResult(Result.Success());
        }

        /* IPaymentRepository */

        Task<Payment?> IPaymentRepository.GetByIdAsync(int id, CancellationToken cancellationToken)
        {
            return Task.FromResult(Invoices.SelectMany(i => i.Payments).FirstOrDefault(p => p.Id == id));
        }

        public Task<IReadOnlyList<Payment>> GetForInvoiceAsync(int invoiceId, CancellationToken cancellationToken = default)
        {
            IReadOnlyList<Payment> rows = Invoices.Where(i => i.Id == invoiceId).SelectMany(i => i.Payments).ToList();
            return Task.FromResult(rows);
        }

        Task<IReadOnlyList<Payment>> IPaymentRepository.GetForCustomerAsync(int customerId, CancellationToken cancellationToken)
        {
            IReadOnlyList<Payment> rows = Invoices.Where(i => i.CustomerId == customerId).SelectMany(i => i.Payments).ToList();
            return Task.FromResult(rows);
        }

        private InvoiceSummary ToSummary(Invoice i)
        {
            Order? order = _orders.Orders.FirstOrDefault(o => o.Id == i.OrderId);
            var customer = _customers.Customers.FirstOrDefault(c => c.Id == i.CustomerId);
            return new InvoiceSummary
            {
                InvoiceId = i.Id,
                InvoiceNumber = i.InvoiceNumber,
                OrderId = i.OrderId,
                OrderNumber = order?.OrderNumber ?? string.Empty,
                CustomerId = i.CustomerId,
                CustomerNumber = customer?.CustomerNumber ?? string.Empty,
                CustomerName = customer?.Name ?? string.Empty,
                IssuedDate = i.IssuedDate,
                DueDate = i.DueDate,
                Total = i.Total,
                AmountPaid = i.AmountPaid,
                Balance = i.Balance,
                Status = i.Status,
                DaysOverdue = i.DaysOverdue(DateTime.UtcNow)
            };
        }
    }
}
