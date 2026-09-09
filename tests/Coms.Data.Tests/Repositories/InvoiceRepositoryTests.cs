using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Coms.Data.Connections;
using Coms.Data.Repositories;
using Coms.Domain.Common;
using Coms.Domain.Invoices;
using Coms.Domain.Orders;
using Xunit;

namespace Coms.Data.Tests.Repositories
{
    [Collection(DatabaseCollection.Name)]
    public class InvoiceRepositoryTests
    {
        private readonly DatabaseFixture _db;

        public InvoiceRepositoryTests(DatabaseFixture db)
        {
            _db = db;
        }

        [Fact]
        public async Task GetById_PaidInvoice_LoadsPayments()
        {
            if (!_db.IsAvailable) { return; }

            int id = await _db.ScalarAsync<int>("SELECT MIN(Id) FROM dbo.Invoices WHERE Status = 'PartiallyPaid'");

            using (DbSession session = _db.OpenSession())
            {
                Invoice? invoice = await new InvoiceRepository(session).GetByIdAsync(id);

                Assert.NotNull(invoice);
                Assert.Equal(InvoiceStatus.PartiallyPaid, invoice!.Status);
                Assert.NotEmpty(invoice.Payments);
                Assert.Equal(invoice.Total - invoice.AmountPaid, invoice.Balance);
                Assert.True(invoice.IsOpen);
                Assert.All(invoice.Payments, p => Assert.Equal(id, p.InvoiceId));
            }
        }

        [Fact]
        public async Task GetLiveByOrderId_ReturnsInvoiceForInvoicedOrder()
        {
            if (!_db.IsAvailable) { return; }

            int orderId = await _db.ScalarAsync<int>("SELECT MIN(Id) FROM dbo.Orders WHERE Status = 'Invoiced'");

            using (DbSession session = _db.OpenSession())
            {
                Invoice? invoice = await new InvoiceRepository(session).GetLiveByOrderIdAsync(orderId);

                Assert.NotNull(invoice);
                Assert.Equal(orderId, invoice!.OrderId);
                Assert.NotEqual(InvoiceStatus.Void, invoice.Status);
            }
        }

        [Fact]
        public async Task Search_OverdueOnly_ReturnsOpenOverdueRows()
        {
            if (!_db.IsAvailable) { return; }

            using (DbSession session = _db.OpenSession())
            {
                var filter = new InvoiceFilter { OverdueOnly = true };
                var paging = new PagedRequest { PageSize = 25, SortBy = "due" };

                PagedResult<InvoiceSummary> page = await new InvoiceRepository(session).SearchAsync(filter, paging);

                Assert.True(page.TotalCount > 0);
                Assert.All(page.Items, i => Assert.True(i.DaysOverdue > 0));
                Assert.All(page.Items, i => Assert.True(i.Status == InvoiceStatus.Open || i.Status == InvoiceStatus.PartiallyPaid));
                Assert.All(page.Items, i => Assert.Equal(i.Total - i.AmountPaid, i.Balance));
            }
        }

        [Fact]
        public async Task Search_BySearchText_MatchesCustomerName()
        {
            if (!_db.IsAvailable) { return; }

            using (DbSession session = _db.OpenSession())
            {
                var filter = new InvoiceFilter { Search = "Harbor" };

                PagedResult<InvoiceSummary> page = await new InvoiceRepository(session).SearchAsync(filter, PagedRequest.FirstPage(10));

                Assert.True(page.TotalCount > 0);
                Assert.All(page.Items, i => Assert.StartsWith("Harbor", i.CustomerName));
            }
        }

        [Fact]
        public async Task GetForCustomer_IsNewestFirst()
        {
            if (!_db.IsAvailable) { return; }

            using (DbSession session = _db.OpenSession())
            {
                IReadOnlyList<InvoiceSummary> rows = await new InvoiceRepository(session).GetForCustomerAsync(1);

                Assert.NotEmpty(rows);
                Assert.All(rows, r => Assert.Equal(1, r.CustomerId));
                for (int i = 1; i < rows.Count; i++)
                {
                    Assert.True(rows[i - 1].IssuedDate >= rows[i].IssuedDate);
                }
            }
        }

        [Fact]
        public async Task CreateFromOrder_FulfilledOrder_CreatesOpenInvoiceAndMarksOrder()
        {
            if (!_db.IsAvailable) { return; }

            int orderId = await _db.ScalarAsync<int>(@"
                SELECT MIN(o.Id) FROM dbo.Orders o
                 WHERE o.Status = 'Fulfilled'
                   AND NOT EXISTS (SELECT 1 FROM dbo.Invoices i WHERE i.OrderId = o.Id AND i.Status <> 'Void')");

            using (DbSession session = await _db.BeginSessionAsync())
            {
                var invoices = new InvoiceRepository(session);
                var orders = new OrderRepository(session);
                Order order = (await orders.GetByIdAsync(orderId))!;

                Result<int> result = await invoices.CreateFromOrderAsync(order.Id, order.RowVersion, "tester", new DateTime(2026, 9, 9));

                Assert.True(result.IsSuccess, result.Message);

                Invoice? invoice = await invoices.GetByIdAsync(result.Value);
                Assert.NotNull(invoice);
                Assert.StartsWith("INV-2026-", invoice!.InvoiceNumber);
                Assert.Equal(InvoiceStatus.Open, invoice.Status);
                Assert.Equal(order.Total, invoice.Total);
                Assert.Equal(new DateTime(2026, 9, 9), invoice.IssuedDate);
                Assert.True(invoice.DueDate >= invoice.IssuedDate);
                Assert.Equal(OrderStatus.Invoiced, (await orders.GetByIdAsync(orderId))!.Status);
            }
        }

        [Fact]
        public async Task CreateFromOrder_ApprovedOrder_ReturnsInvalidStatus()
        {
            if (!_db.IsAvailable) { return; }

            int orderId = await _db.ScalarAsync<int>("SELECT MIN(Id) FROM dbo.Orders WHERE Status = 'Approved'");
            byte[] rowVersion = await _db.ScalarAsync<byte[]>("SELECT RowVersion FROM dbo.Orders WHERE Id = @Id", new { Id = orderId });

            using (DbSession session = _db.OpenSession())
            {
                Result<int> result = await new InvoiceRepository(session).CreateFromOrderAsync(orderId, rowVersion, "tester", null);

                Assert.Equal(ErrorCode.InvalidStatus, result.Code);
                Assert.Throws<InvalidOperationException>(() => result.Value);
            }
        }

        [Fact]
        public async Task ApplyPayment_Partial_UpdatesInvoice()
        {
            if (!_db.IsAvailable) { return; }

            int invoiceId = await _db.ScalarAsync<int>("SELECT MIN(Id) FROM dbo.Invoices WHERE Status = 'Open'");

            using (DbSession session = await _db.BeginSessionAsync())
            {
                var repository = new InvoiceRepository(session);
                Invoice invoice = (await repository.GetByIdAsync(invoiceId))!;
                decimal half = Money.Round(invoice.Total / 2);

                var payment = new Payment
                {
                    InvoiceId = invoiceId,
                    Amount = half,
                    PaidDate = new DateTime(2026, 9, 9),
                    Method = PaymentMethod.BankTransfer,
                    Reference = "TEST-1"
                };

                Result<int> result = await repository.ApplyPaymentAsync(payment, invoice.RowVersion, "tester");

                Assert.True(result.IsSuccess, result.Message);
                Assert.Equal(result.Value, payment.Id);

                Invoice? reloaded = await repository.GetByIdAsync(invoiceId);
                Assert.Equal(InvoiceStatus.PartiallyPaid, reloaded!.Status);
                Assert.Equal(half, reloaded.AmountPaid);
                Assert.Single(reloaded.Payments);
                Assert.Equal(PaymentMethod.BankTransfer, reloaded.Payments[0].Method);
                Assert.Equal("TEST-1", reloaded.Payments[0].Reference);
            }
        }

        [Fact]
        public async Task ApplyPayment_MoreThanBalance_ReturnsPaymentExceedsBalance()
        {
            if (!_db.IsAvailable) { return; }

            int invoiceId = await _db.ScalarAsync<int>("SELECT MIN(Id) FROM dbo.Invoices WHERE Status = 'Open'");
            decimal total = await _db.ScalarAsync<decimal>("SELECT Total FROM dbo.Invoices WHERE Id = @Id", new { Id = invoiceId });
            byte[] rowVersion = await _db.ScalarAsync<byte[]>("SELECT RowVersion FROM dbo.Invoices WHERE Id = @Id", new { Id = invoiceId });

            using (DbSession session = _db.OpenSession())
            {
                var payment = new Payment { InvoiceId = invoiceId, Amount = total + 0.01m, PaidDate = DateTime.UtcNow.Date, Method = PaymentMethod.Cash };

                Result<int> result = await new InvoiceRepository(session).ApplyPaymentAsync(payment, rowVersion, "tester");

                Assert.Equal(ErrorCode.PaymentExceedsBalance, result.Code);
            }
        }

        [Fact]
        public async Task ApplyPayment_PaidInvoice_ReturnsInvoiceNotOpen()
        {
            if (!_db.IsAvailable) { return; }

            int invoiceId = await _db.ScalarAsync<int>("SELECT MIN(Id) FROM dbo.Invoices WHERE Status = 'Paid'");
            byte[] rowVersion = await _db.ScalarAsync<byte[]>("SELECT RowVersion FROM dbo.Invoices WHERE Id = @Id", new { Id = invoiceId });

            using (DbSession session = _db.OpenSession())
            {
                var payment = new Payment { InvoiceId = invoiceId, Amount = 1m, PaidDate = DateTime.UtcNow.Date, Method = PaymentMethod.Cash };

                Result<int> result = await new InvoiceRepository(session).ApplyPaymentAsync(payment, rowVersion, "tester");

                Assert.Equal(ErrorCode.InvoiceNotOpen, result.Code);
            }
        }

        [Fact]
        public async Task Void_UnpaidInvoice_VoidsAndReturnsOrderToFulfilled()
        {
            if (!_db.IsAvailable) { return; }

            int invoiceId = await _db.ScalarAsync<int>("SELECT MIN(Id) FROM dbo.Invoices WHERE Status = 'Open' AND AmountPaid = 0");

            using (DbSession session = await _db.BeginSessionAsync())
            {
                var invoices = new InvoiceRepository(session);
                Invoice invoice = (await invoices.GetByIdAsync(invoiceId))!;

                Result result = await invoices.VoidAsync(invoiceId, invoice.RowVersion, "tester", "duplicate");

                Assert.True(result.IsSuccess, result.Message);
                Assert.Equal(InvoiceStatus.Void, (await invoices.GetByIdAsync(invoiceId))!.Status);
                Assert.Null(await invoices.GetLiveByOrderIdAsync(invoice.OrderId));

                Order? order = await new OrderRepository(session).GetByIdAsync(invoice.OrderId);
                Assert.Equal(OrderStatus.Fulfilled, order!.Status);
                Assert.Contains(order.History, h => h.ToStatus == OrderStatus.Fulfilled && h.Comment != null && h.Comment.Contains("voided"));
            }
        }

        [Fact]
        public async Task Void_InvoiceWithPayments_ReturnsInvoiceHasPayments()
        {
            if (!_db.IsAvailable) { return; }

            int invoiceId = await _db.ScalarAsync<int>("SELECT MIN(Id) FROM dbo.Invoices WHERE Status = 'PartiallyPaid'");
            byte[] rowVersion = await _db.ScalarAsync<byte[]>("SELECT RowVersion FROM dbo.Invoices WHERE Id = @Id", new { Id = invoiceId });

            using (DbSession session = _db.OpenSession())
            {
                Result result = await new InvoiceRepository(session).VoidAsync(invoiceId, rowVersion, "tester", null);

                Assert.Equal(ErrorCode.InvoiceHasPayments, result.Code);
            }
        }

        [Fact]
        public async Task PaymentRepository_GetForCustomer_ReturnsCustomerPayments()
        {
            if (!_db.IsAvailable) { return; }

            using (DbSession session = _db.OpenSession())
            {
                IReadOnlyList<Payment> payments = await new PaymentRepository(session).GetForCustomerAsync(1);

                Assert.NotEmpty(payments);
                Assert.All(payments, p => Assert.True(p.Amount > 0));
            }
        }
    }
}
