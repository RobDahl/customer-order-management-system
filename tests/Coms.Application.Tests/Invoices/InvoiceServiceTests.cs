using System;
using System.Threading.Tasks;
using Coms.Application.Tests.Fakes;
using Coms.Domain.Common;
using Coms.Domain.Invoices;
using Coms.Domain.Orders;
using Xunit;

namespace Coms.Application.Tests.Invoices
{
    public class InvoiceServiceTests
    {
        private readonly TestWorld _world = new TestWorld();

        [Fact]
        public async Task CreateFromOrder_Fulfilled_CreatesOpenInvoiceWithTerms()
        {
            Order order = _world.SeedOrder(OrderStatus.Fulfilled);

            Result<Invoice> result = await _world.InvoiceService.CreateFromOrderAsync(order.Id, order.RowVersion, new DateTime(2026, 9, 10));

            Assert.True(result.IsSuccess, result.Message);
            Invoice invoice = result.Value;
            Assert.Equal(InvoiceStatus.Open, invoice.Status);
            Assert.Equal(order.Total, invoice.Total);
            Assert.Equal(new DateTime(2026, 10, 10), invoice.DueDate);   // 30-day terms
            Assert.Equal(OrderStatus.Invoiced, order.Status);
        }

        [Fact]
        public async Task CreateFromOrder_Approved_ReturnsInvalidStatus()
        {
            Order order = _world.SeedOrder(OrderStatus.Approved);

            Result<Invoice> result = await _world.InvoiceService.CreateFromOrderAsync(order.Id, order.RowVersion, null);

            Assert.Equal(ErrorCode.InvalidStatus, result.Code);
            Assert.Empty(_world.Invoices.Invoices);
        }

        [Fact]
        public async Task CreateFromOrder_IssueDateBeforeOrderDate_IsRejected()
        {
            Order order = _world.SeedOrder(OrderStatus.Fulfilled);

            Result<Invoice> result = await _world.InvoiceService.CreateFromOrderAsync(order.Id, order.RowVersion, order.OrderDate.AddDays(-1));

            Assert.Equal(ErrorCode.Validation, result.Code);
        }

        [Fact]
        public async Task CreateFromOrder_Twice_ReturnsInvoiceExists()
        {
            Order order = _world.SeedOrder(OrderStatus.Fulfilled);
            await _world.InvoiceService.CreateFromOrderAsync(order.Id, order.RowVersion, null);
            order.Status = OrderStatus.Fulfilled;   // simulate a stale client that still sees it as fulfilled

            Result<Invoice> result = await _world.InvoiceService.CreateFromOrderAsync(order.Id, order.RowVersion, null);

            Assert.Equal(ErrorCode.InvoiceExists, result.Code);
        }

        [Fact]
        public async Task ApplyPayment_Partial_ThenFull_MovesThroughStatuses()
        {
            Invoice invoice = await OpenInvoiceAsync();

            Result<Invoice> partial = await _world.InvoiceService.ApplyPaymentAsync(
                new Payment { InvoiceId = invoice.Id, Amount = 13m, PaidDate = new DateTime(2026, 9, 12), Method = PaymentMethod.Cheque, Reference = " CHK-1 " },
                invoice.RowVersion);

            Assert.True(partial.IsSuccess, partial.Message);
            Assert.Equal(InvoiceStatus.PartiallyPaid, partial.Value.Status);
            Assert.Equal(20m, partial.Value.Balance);
            Assert.Equal("CHK-1", partial.Value.Payments[0].Reference);

            Result<Invoice> full = await _world.InvoiceService.ApplyPaymentAsync(
                new Payment { InvoiceId = invoice.Id, Amount = 20m, PaidDate = new DateTime(2026, 9, 20), Method = PaymentMethod.Card },
                partial.Value.RowVersion);

            Assert.True(full.IsSuccess, full.Message);
            Assert.Equal(InvoiceStatus.Paid, full.Value.Status);
            Assert.Equal(0m, full.Value.Balance);
        }

        [Fact]
        public async Task ApplyPayment_MoreThanBalance_ReturnsPaymentExceedsBalance()
        {
            Invoice invoice = await OpenInvoiceAsync();

            Result<Invoice> result = await _world.InvoiceService.ApplyPaymentAsync(
                new Payment { InvoiceId = invoice.Id, Amount = 33.01m, PaidDate = new DateTime(2026, 9, 12), Method = PaymentMethod.Cash },
                invoice.RowVersion);

            Assert.Equal(ErrorCode.PaymentExceedsBalance, result.Code);
            Assert.Empty(invoice.Payments);
        }

        [Fact]
        public async Task ApplyPayment_BeforeIssueDate_IsRejected()
        {
            Invoice invoice = await OpenInvoiceAsync();

            Result<Invoice> result = await _world.InvoiceService.ApplyPaymentAsync(
                new Payment { InvoiceId = invoice.Id, Amount = 1m, PaidDate = invoice.IssuedDate.AddDays(-1), Method = PaymentMethod.Cash },
                invoice.RowVersion);

            Assert.Equal(ErrorCode.Validation, result.Code);
            Assert.Contains(result.Errors, e => e.Field == "PaidDate");
        }

        [Fact]
        public async Task ApplyPayment_ZeroAmount_IsRejected()
        {
            Invoice invoice = await OpenInvoiceAsync();

            Result<Invoice> result = await _world.InvoiceService.ApplyPaymentAsync(
                new Payment { InvoiceId = invoice.Id, Amount = 0m, PaidDate = invoice.IssuedDate, Method = PaymentMethod.Cash },
                invoice.RowVersion);

            Assert.Equal(ErrorCode.Validation, result.Code);
        }

        [Fact]
        public async Task ApplyPayment_StaleVersion_ReturnsConflict()
        {
            Invoice invoice = await OpenInvoiceAsync();

            Result<Invoice> result = await _world.InvoiceService.ApplyPaymentAsync(
                new Payment { InvoiceId = invoice.Id, Amount = 1m, PaidDate = invoice.IssuedDate, Method = PaymentMethod.Cash },
                new byte[] { 7, 7, 7, 7, 7, 7, 7, 7 });

            Assert.Equal(ErrorCode.Conflict, result.Code);
        }

        [Fact]
        public async Task Void_Unpaid_VoidsAndReturnsOrderToFulfilled()
        {
            Invoice invoice = await OpenInvoiceAsync();

            Result<Invoice> result = await _world.InvoiceService.VoidAsync(invoice.Id, invoice.RowVersion, "wrong customer");

            Assert.True(result.IsSuccess, result.Message);
            Assert.Equal(InvoiceStatus.Void, result.Value.Status);
            Assert.Equal(OrderStatus.Fulfilled, _world.Orders.Orders[0].Status);
            Assert.Null(await _world.InvoiceService.GetLiveForOrderAsync(invoice.OrderId));
        }

        [Fact]
        public async Task Void_WithPayments_ReturnsInvoiceHasPayments()
        {
            Invoice invoice = await OpenInvoiceAsync();
            await _world.InvoiceService.ApplyPaymentAsync(
                new Payment { InvoiceId = invoice.Id, Amount = 5m, PaidDate = invoice.IssuedDate, Method = PaymentMethod.Cash },
                invoice.RowVersion);

            Result<Invoice> result = await _world.InvoiceService.VoidAsync(invoice.Id, invoice.RowVersion, null);

            Assert.Equal(ErrorCode.InvoiceHasPayments, result.Code);
        }

        [Fact]
        public async Task Void_AlreadyVoid_ReturnsInvoiceNotOpen()
        {
            Invoice invoice = await OpenInvoiceAsync();
            await _world.InvoiceService.VoidAsync(invoice.Id, invoice.RowVersion, null);

            Result<Invoice> result = await _world.InvoiceService.VoidAsync(invoice.Id, invoice.RowVersion, null);

            Assert.Equal(ErrorCode.InvoiceNotOpen, result.Code);
        }

        private async Task<Invoice> OpenInvoiceAsync()
        {
            Order order = _world.SeedOrder(OrderStatus.Fulfilled);   // total 33
            Result<Invoice> created = await _world.InvoiceService.CreateFromOrderAsync(order.Id, order.RowVersion, new DateTime(2026, 9, 10));
            return created.Value;
        }
    }
}
