using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Coms.Data.Connections;
using Coms.Data.Repositories;
using Coms.Domain.Common;
using Coms.Domain.Orders;
using Coms.Domain.Products;
using Xunit;

namespace Coms.Data.Tests.Repositories
{
    [Collection(DatabaseCollection.Name)]
    public class OrderRepositoryTests
    {
        private const string ApprovedOrderWithStockSql = @"
            SELECT TOP (1) o.Id
              FROM dbo.Orders o
             WHERE o.Status = 'Approved'
               AND NOT EXISTS
                   (SELECT 1
                      FROM dbo.OrderLines l
                      JOIN dbo.Products p ON p.Id = l.ProductId
                     WHERE l.OrderId = o.Id
                     GROUP BY l.ProductId, p.QuantityOnHand
                    HAVING SUM(l.Quantity) > p.QuantityOnHand)
             ORDER BY o.Id";

        private readonly DatabaseFixture _db;

        public OrderRepositoryTests(DatabaseFixture db)
        {
            _db = db;
        }

        [Fact]
        public async Task GetById_LoadsHeaderLinesAndHistory()
        {
            if (!_db.IsAvailable) { return; }

            using (DbSession session = _db.OpenSession())
            {
                Order? order = await new OrderRepository(session).GetByIdAsync(1);

                Assert.NotNull(order);
                Assert.StartsWith("ORD-", order!.OrderNumber);
                Assert.NotEmpty(order.Lines);
                Assert.NotEmpty(order.History);
                Assert.Null(order.History[0].FromStatus);
                Assert.Equal(OrderStatus.Draft, order.History[0].ToStatus);
                Assert.NotNull(order.ShipTo);
                Assert.Equal(order.Lines.Sum(l => l.LineTotal), order.Subtotal);
                Assert.Equal(order.Subtotal + order.TaxAmount, order.Total);
            }
        }

        [Fact]
        public async Task GetByNumber_FindsOrder()
        {
            if (!_db.IsAvailable) { return; }

            string number = await _db.ScalarAsync<string>("SELECT OrderNumber FROM dbo.Orders WHERE Id = 5");

            using (DbSession session = _db.OpenSession())
            {
                Order? order = await new OrderRepository(session).GetByNumberAsync(number);

                Assert.Equal(5, order!.Id);
            }
        }

        [Fact]
        public async Task Search_ByStatus_ReturnsOnlyThatStatus()
        {
            if (!_db.IsAvailable) { return; }

            using (DbSession session = _db.OpenSession())
            {
                var filter = new OrderFilter { Status = OrderStatus.Approved };

                PagedResult<OrderSummary> page = await new OrderRepository(session).SearchAsync(filter, PagedRequest.FirstPage(20));

                Assert.True(page.TotalCount > 0);
                Assert.All(page.Items, s => Assert.Equal(OrderStatus.Approved, s.Status));
                Assert.All(page.Items, s => Assert.True(s.LineCount > 0));
                Assert.All(page.Items, s => Assert.False(string.IsNullOrEmpty(s.CustomerName)));
            }
        }

        [Fact]
        public async Task Search_ByDateRangeAndCustomer_Filters()
        {
            if (!_db.IsAvailable) { return; }

            using (DbSession session = _db.OpenSession())
            {
                var filter = new OrderFilter
                {
                    CustomerId = 1,
                    FromDate = new DateTime(2026, 1, 1),
                    ToDate = new DateTime(2026, 6, 30)
                };
                var paging = new PagedRequest { PageSize = 500, SortBy = "date" };

                PagedResult<OrderSummary> page = await new OrderRepository(session).SearchAsync(filter, paging);

                Assert.True(page.TotalCount > 0);
                Assert.All(page.Items, s => Assert.Equal(1, s.CustomerId));
                Assert.All(page.Items, s => Assert.InRange(s.OrderDate, filter.FromDate.Value, filter.ToDate.Value));
            }
        }

        [Fact]
        public async Task Search_InvoicedOrders_CarryInvoiceNumber()
        {
            if (!_db.IsAvailable) { return; }

            using (DbSession session = _db.OpenSession())
            {
                var filter = new OrderFilter { Status = OrderStatus.Invoiced };

                PagedResult<OrderSummary> page = await new OrderRepository(session).SearchAsync(filter, PagedRequest.FirstPage(10));

                Assert.All(page.Items, s => Assert.StartsWith("INV-", s.InvoiceNumber));
                Assert.All(page.Items, s => Assert.NotNull(s.InvoiceStatus));
            }
        }

        [Fact]
        public async Task Insert_Draft_AssignsNumberLineIdsAndHistory()
        {
            if (!_db.IsAvailable) { return; }

            using (DbSession session = await _db.BeginSessionAsync())
            {
                var repository = new OrderRepository(session);
                Order order = await NewDraftAsync(session);

                await repository.InsertAsync(order, "tester");

                Assert.True(order.Id > 5000);
                Assert.StartsWith("ORD-" + order.OrderDate.Year + "-", order.OrderNumber);
                Assert.All(order.Lines, l => Assert.True(l.Id > 0));
                Assert.All(order.Lines, l => Assert.Equal(order.Id, l.OrderId));
                Assert.Single(order.History);
                Assert.Equal(OrderStatus.Draft, order.History[0].ToStatus);

                Order? reloaded = await repository.GetByIdAsync(order.Id);
                Assert.Equal(2, reloaded!.Lines.Count);
                Assert.Equal(order.Total, reloaded.Total);
                Assert.Equal("Springfield", reloaded.ShipTo!.City);
            }
        }

        [Fact]
        public async Task Update_ReplacesLinesAndBumpsRowVersion()
        {
            if (!_db.IsAvailable) { return; }

            using (DbSession session = await _db.BeginSessionAsync())
            {
                var repository = new OrderRepository(session);
                Order order = await NewDraftAsync(session);
                await repository.InsertAsync(order, "tester");
                byte[] before = order.RowVersion.ToArray();

                order.Lines.RemoveAt(1);
                order.Notes = "One line now";
                order.Recalculate();

                Assert.True(await repository.UpdateAsync(order, "tester"));
                Assert.False(before.SequenceEqual(order.RowVersion));

                Order? reloaded = await repository.GetByIdAsync(order.Id);
                Assert.Single(reloaded!.Lines);
                Assert.Equal(1, reloaded.Lines[0].LineNumber);
                Assert.Equal("One line now", reloaded.Notes);
                Assert.Equal(order.Total, reloaded.Total);
            }
        }

        [Fact]
        public async Task Update_StaleRowVersion_ReturnsFalseAndKeepsLines()
        {
            if (!_db.IsAvailable) { return; }

            using (DbSession session = await _db.BeginSessionAsync())
            {
                var repository = new OrderRepository(session);
                Order order = await NewDraftAsync(session);
                await repository.InsertAsync(order, "tester");

                order.RowVersion = new byte[8];
                order.Lines.Clear();

                Assert.False(await repository.UpdateAsync(order, "tester"));
                Assert.Equal(2, (await repository.GetByIdAsync(order.Id))!.Lines.Count);
            }
        }

        [Fact]
        public async Task ChangeStatus_DraftToSubmitted_WritesHistory()
        {
            if (!_db.IsAvailable) { return; }

            using (DbSession session = await _db.BeginSessionAsync())
            {
                var repository = new OrderRepository(session);
                Order order = await NewDraftAsync(session);
                await repository.InsertAsync(order, "tester");

                bool changed = await repository.ChangeStatusAsync(order.Id, OrderStatus.Draft, OrderStatus.Submitted, order.RowVersion, "tester", "ready");

                Assert.True(changed);

                Order? reloaded = await repository.GetByIdAsync(order.Id);
                Assert.Equal(OrderStatus.Submitted, reloaded!.Status);
                Assert.Equal(2, reloaded.History.Count);
                Assert.Equal(OrderStatus.Draft, reloaded.History[1].FromStatus);
                Assert.Equal("ready", reloaded.History[1].Comment);
            }
        }

        [Fact]
        public async Task ChangeStatus_WrongFromStatus_ReturnsFalse()
        {
            if (!_db.IsAvailable) { return; }

            using (DbSession session = await _db.BeginSessionAsync())
            {
                var repository = new OrderRepository(session);
                Order order = await NewDraftAsync(session);
                await repository.InsertAsync(order, "tester");

                bool changed = await repository.ChangeStatusAsync(order.Id, OrderStatus.Approved, OrderStatus.Fulfilled, order.RowVersion, "tester", null);

                Assert.False(changed);
                Assert.Single((await repository.GetByIdAsync(order.Id))!.History);
            }
        }

        [Fact]
        public async Task Fulfil_ApprovedOrderWithStock_DecrementsStockAndChangesStatus()
        {
            if (!_db.IsAvailable) { return; }

            int orderId = await _db.ScalarAsync<int>(ApprovedOrderWithStockSql);

            using (DbSession session = await _db.BeginSessionAsync())
            {
                var orders = new OrderRepository(session);
                var products = new ProductRepository(session);

                Order order = (await orders.GetByIdAsync(orderId))!;
                OrderLine line = order.Lines[0];
                decimal demand = order.Lines.Where(l => l.ProductId == line.ProductId).Sum(l => l.Quantity);
                decimal stockBefore = (await products.GetByIdAsync(line.ProductId))!.QuantityOnHand;

                Result result = await orders.FulfilAsync(order.Id, order.RowVersion, "tester", "picked", allowNegativeStock: false);

                Assert.True(result.IsSuccess, result.Message);
                Assert.Equal(OrderStatus.Fulfilled, (await orders.GetByIdAsync(orderId))!.Status);
                Assert.Equal(stockBefore - demand, (await products.GetByIdAsync(line.ProductId))!.QuantityOnHand);
            }
        }

        [Fact]
        public async Task Fulfil_DraftOrder_ReturnsInvalidStatus()
        {
            if (!_db.IsAvailable) { return; }

            int orderId = await _db.ScalarAsync<int>("SELECT MIN(Id) FROM dbo.Orders WHERE Status = 'Draft'");
            byte[] rowVersion = await _db.ScalarAsync<byte[]>("SELECT RowVersion FROM dbo.Orders WHERE Id = @Id", new { Id = orderId });

            using (DbSession session = _db.OpenSession())
            {
                Result result = await new OrderRepository(session).FulfilAsync(orderId, rowVersion, "tester", null, false);

                Assert.False(result.IsSuccess);
                Assert.Equal(ErrorCode.InvalidStatus, result.Code);
                Assert.Contains("Draft", result.Message);
            }
        }

        [Fact]
        public async Task Fulfil_StaleRowVersion_ReturnsConflict()
        {
            if (!_db.IsAvailable) { return; }

            int orderId = await _db.ScalarAsync<int>(ApprovedOrderWithStockSql);

            using (DbSession session = _db.OpenSession())
            {
                Result result = await new OrderRepository(session).FulfilAsync(orderId, new byte[8], "tester", null, false);

                Assert.Equal(ErrorCode.Conflict, result.Code);
            }
        }

        [Fact]
        public async Task Fulfil_MissingOrder_ReturnsNotFound()
        {
            if (!_db.IsAvailable) { return; }

            using (DbSession session = _db.OpenSession())
            {
                Result result = await new OrderRepository(session).FulfilAsync(999999, new byte[8], "tester", null, false);

                Assert.Equal(ErrorCode.NotFound, result.Code);
            }
        }

        [Fact]
        public async Task GetRecentHistory_IsNewestFirst()
        {
            if (!_db.IsAvailable) { return; }

            using (DbSession session = _db.OpenSession())
            {
                PagedResult<OrderStatusChange> page = await new OrderRepository(session).GetRecentHistoryAsync(PagedRequest.FirstPage(20));

                Assert.Equal(20, page.Items.Count);
                for (int i = 1; i < page.Items.Count; i++)
                {
                    Assert.True(page.Items[i - 1].ChangedAtUtc >= page.Items[i].ChangedAtUtc);
                }
            }
        }

        private static async Task<Order> NewDraftAsync(DbSession session)
        {
            IReadOnlyList<ProductLookup> products = await new ProductRepository(session).LookupAsync("", 2, activeOnly: true);

            var order = new Order
            {
                CustomerId = 1,
                OrderDate = new DateTime(2026, 9, 9),
                RequiredDate = new DateTime(2026, 9, 16),
                CustomerReference = "PO-TEST",
                TaxRate = 0.08m,
                ShipTo = new Address { Line1 = "1 Test St", City = "Springfield", Country = "USA" }
            };

            order.Lines.Add(OrderLine.FromProduct(products[0], 3));
            order.Lines.Add(OrderLine.FromProduct(products[1], 1.5m));
            order.Lines[1].DiscountPercent = 10;
            order.Recalculate();
            return order;
        }
    }
}
