using System;
using System.Linq;
using System.Threading.Tasks;
using Coms.Application.Orders;
using Coms.Application.Tests.Fakes;
using Coms.Domain.Common;
using Coms.Domain.Customers;
using Coms.Domain.Orders;
using Xunit;

namespace Coms.Application.Tests.Orders
{
    public class OrderServiceTests
    {
        private readonly TestWorld _world = new TestWorld();

        /* ---------------- create / update ---------------- */

        [Fact]
        public async Task CreateDraft_BuildsLinesFromProductsAndComputesTotals()
        {
            OrderInput input = _world.NewInput();

            Result<Order> result = await _world.OrderService.CreateDraftAsync(input);

            Assert.True(result.IsSuccess, result.Message);
            Order order = result.Value;
            Assert.Equal(OrderStatus.Draft, order.Status);
            Assert.StartsWith("ORD-2026-", order.OrderNumber);
            Assert.Equal(2, order.Lines.Count);
            Assert.Equal("BOLT-1", order.Lines[0].Sku);
            Assert.Equal("Bolt", order.Lines[0].Description);
            Assert.Equal(10m, order.Lines[0].UnitPrice);
            Assert.Equal(4m, order.Lines[0].UnitCost);
            Assert.Equal(20m, order.Lines[0].LineTotal);
            Assert.Equal(10m, order.Lines[1].LineTotal);
            Assert.Equal(30m, order.Subtotal);
            Assert.Equal(3m, order.TaxAmount);          // 10% from options
            Assert.Equal(33m, order.Total);
            Assert.Equal("Dock 4", order.ShipTo!.Line1); // customer's shipping address
            Assert.Single(order.History);
        }

        [Fact]
        public async Task CreateDraft_OnHoldCustomer_IsRejected()
        {
            OrderInput input = _world.NewInput(_world.OnHoldCustomer.Id);

            Result<Order> result = await _world.OrderService.CreateDraftAsync(input);

            Assert.Equal(ErrorCode.Validation, result.Code);
            Assert.Contains(result.Errors, e => e.Field == "CustomerId" && e.Message.Contains("OnHold"));
            Assert.Empty(_world.Orders.Orders);
        }

        [Fact]
        public async Task CreateDraft_UnknownAndInactiveProducts_ReportPerLine()
        {
            OrderInput input = _world.NewInput(null, (_world.Bolt, 1), (_world.Retired, 1));
            input.Lines.Add(new OrderLineInput { ProductId = 999, Quantity = 1 });

            Result<Order> result = await _world.OrderService.CreateDraftAsync(input);

            Assert.Equal(ErrorCode.Validation, result.Code);
            Assert.Contains(result.Errors, e => e.Field == "Lines[1].ProductId" && e.Message.Contains("inactive"));
            Assert.Contains(result.Errors, e => e.Field == "Lines[2].ProductId" && e.Message.Contains("not found"));
        }

        [Fact]
        public async Task CreateDraft_ZeroQuantity_IsRejected()
        {
            OrderInput input = _world.NewInput(null, (_world.Bolt, 0));

            Result<Order> result = await _world.OrderService.CreateDraftAsync(input);

            Assert.Contains(result.Errors, e => e.Field == "Lines[0].Quantity");
        }

        [Fact]
        public async Task CreateDraft_PriceOverrideAndDescription_AreHonoured()
        {
            OrderInput input = _world.NewInput(null, (_world.Bolt, 3));
            input.Lines[0].UnitPrice = 8m;
            input.Lines[0].Description = "Bolt, special";
            input.Lines[0].DiscountPercent = 50;

            Result<Order> result = await _world.OrderService.CreateDraftAsync(input);

            Assert.True(result.IsSuccess);
            Assert.Equal(8m, result.Value.Lines[0].UnitPrice);
            Assert.Equal("Bolt, special", result.Value.Lines[0].Description);
            Assert.Equal(12m, result.Value.Lines[0].LineTotal);
        }

        [Fact]
        public async Task Preview_DoesNotSave()
        {
            Result<Order> result = await _world.OrderService.PreviewAsync(_world.NewInput());

            Assert.True(result.IsSuccess);
            Assert.Equal(33m, result.Value.Total);
            Assert.Empty(_world.Orders.Orders);
        }

        [Fact]
        public async Task Update_ReplacesLinesAndKeepsTaxRate()
        {
            Order order = (await _world.OrderService.CreateDraftAsync(_world.NewInput())).Value;
            _world.Options.TaxRate = 0.5m;   // must not affect an existing order

            OrderInput input = _world.NewInput(null, (_world.Nut, 10));
            Result<Order> result = await _world.OrderService.UpdateAsync(order.Id, order.RowVersion, input);

            Assert.True(result.IsSuccess, result.Message);
            Assert.Single(result.Value.Lines);
            Assert.Equal(25m, result.Value.Subtotal);
            Assert.Equal(0.10m, result.Value.TaxRate);
            Assert.Equal(27.5m, result.Value.Total);
        }

        [Fact]
        public async Task Update_ApprovedOrder_IsRejected()
        {
            Order order = _world.SeedOrder(OrderStatus.Approved);

            Result<Order> result = await _world.OrderService.UpdateAsync(order.Id, order.RowVersion, _world.NewInput());

            Assert.Equal(ErrorCode.InvalidStatus, result.Code);
        }

        [Fact]
        public async Task Update_StaleRowVersion_ReturnsConflict()
        {
            Order order = _world.SeedOrder(OrderStatus.Draft);

            Result<Order> result = await _world.OrderService.UpdateAsync(order.Id, new byte[] { 1, 1, 1, 1, 1, 1, 1, 1 }, _world.NewInput());

            Assert.Equal(ErrorCode.Conflict, result.Code);
        }

        /* ---------------- submit ---------------- */

        [Fact]
        public async Task Submit_Draft_MovesToSubmittedWithHistory()
        {
            Order order = _world.SeedOrder(OrderStatus.Draft);

            Result<Order> result = await _world.OrderService.SubmitAsync(order.Id, order.RowVersion, "ready");

            Assert.True(result.IsSuccess, result.Message);
            Assert.Equal(OrderStatus.Submitted, result.Value.Status);
            Assert.Equal("ready", result.Value.History.Last().Comment);
            Assert.Equal("tester", result.Value.History.Last().ChangedBy);
        }

        [Fact]
        public async Task Submit_NoLines_ReturnsNoLines()
        {
            Order order = _world.Orders.Add(new Order { CustomerId = _world.ActiveCustomer.Id, Status = OrderStatus.Draft, OrderDate = DateTime.UtcNow.Date });

            Result<Order> result = await _world.OrderService.SubmitAsync(order.Id, order.RowVersion, null);

            Assert.Equal(ErrorCode.NoLines, result.Code);
            Assert.Equal(OrderStatus.Draft, order.Status);
        }

        [Fact]
        public async Task Submit_CustomerWentOnHold_ReturnsCustomerNotActive()
        {
            Order order = _world.SeedOrder(OrderStatus.Draft);
            _world.ActiveCustomer.Status = CustomerStatus.OnHold;

            Result<Order> result = await _world.OrderService.SubmitAsync(order.Id, order.RowVersion, null);

            Assert.Equal(ErrorCode.CustomerNotActive, result.Code);
        }

        [Fact]
        public async Task Submit_ProductWentInactive_ReturnsProductInactive()
        {
            Order order = _world.SeedOrder(OrderStatus.Draft);
            _world.Nut.IsActive = false;

            Result<Order> result = await _world.OrderService.SubmitAsync(order.Id, order.RowVersion, null);

            Assert.Equal(ErrorCode.ProductInactive, result.Code);
            Assert.Contains("NUT-1", result.Message);
        }

        [Fact]
        public async Task Submit_AlreadySubmitted_ReturnsInvalidStatus()
        {
            Order order = _world.SeedOrder(OrderStatus.Submitted);

            Result<Order> result = await _world.OrderService.SubmitAsync(order.Id, order.RowVersion, null);

            Assert.Equal(ErrorCode.InvalidStatus, result.Code);
        }

        /* ---------------- approve ---------------- */

        [Fact]
        public async Task Approve_WithinCreditLimit_Succeeds()
        {
            Order order = _world.SeedOrder(OrderStatus.Submitted);           // total 33
            _world.SetBalance(_world.ActiveCustomer.Id, creditLimit: 1000, outstanding: 500, committed: 400);

            Result<Order> result = await _world.OrderService.ApproveAsync(order.Id, order.RowVersion, null);

            Assert.True(result.IsSuccess, result.Message);
            Assert.Equal(OrderStatus.Approved, result.Value.Status);
        }

        [Fact]
        public async Task Approve_ExceedingCreditLimit_ReturnsCreditLimitExceeded()
        {
            Order order = _world.SeedOrder(OrderStatus.Submitted);           // total 33
            _world.SetBalance(_world.ActiveCustomer.Id, creditLimit: 1000, outstanding: 600, committed: 390);   // 10 available

            Result<Order> result = await _world.OrderService.ApproveAsync(order.Id, order.RowVersion, null);

            Assert.Equal(ErrorCode.CreditLimitExceeded, result.Code);
            Assert.Contains("10.00", result.Message);
            Assert.Equal(OrderStatus.Submitted, order.Status);
        }

        [Fact]
        public async Task Approve_NoCreditLimit_AlwaysPasses()
        {
            Order order = _world.SeedOrder(OrderStatus.Submitted);
            _world.Customers.Balances[_world.ActiveCustomer.Id] = new CustomerBalance { CustomerId = _world.ActiveCustomer.Id, CreditLimit = null, OutstandingBalance = 999999 };

            Result<Order> result = await _world.OrderService.ApproveAsync(order.Id, order.RowVersion, null);

            Assert.True(result.IsSuccess, result.Message);
        }

        [Fact]
        public async Task Approve_FromDraft_ReturnsInvalidStatus()
        {
            Order order = _world.SeedOrder(OrderStatus.Draft);

            Result<Order> result = await _world.OrderService.ApproveAsync(order.Id, order.RowVersion, null);

            Assert.Equal(ErrorCode.InvalidStatus, result.Code);
            Assert.Contains("Draft", result.Message);
        }

        [Fact]
        public async Task Approve_OnHoldCustomer_ReturnsCustomerNotActive()
        {
            Order order = _world.SeedOrder(OrderStatus.Submitted);
            _world.ActiveCustomer.Status = CustomerStatus.OnHold;

            Result<Order> result = await _world.OrderService.ApproveAsync(order.Id, order.RowVersion, null);

            Assert.Equal(ErrorCode.CustomerNotActive, result.Code);
        }

        /* ---------------- fulfil ---------------- */

        [Fact]
        public async Task Fulfil_Approved_DecrementsStock()
        {
            Order order = _world.SeedOrder(OrderStatus.Approved, null, (_world.Bolt, 30));

            Result<Order> result = await _world.OrderService.FulfilAsync(order.Id, order.RowVersion, "picked");

            Assert.True(result.IsSuccess, result.Message);
            Assert.Equal(OrderStatus.Fulfilled, result.Value.Status);
            Assert.Equal(70, _world.Bolt.QuantityOnHand);
        }

        [Fact]
        public async Task Fulfil_InsufficientStock_ReturnsInsufficientStock()
        {
            Order order = _world.SeedOrder(OrderStatus.Approved, null, (_world.Nut, 50));   // only 5 on hand

            Result<Order> result = await _world.OrderService.FulfilAsync(order.Id, order.RowVersion, null);

            Assert.Equal(ErrorCode.InsufficientStock, result.Code);
            Assert.Equal(5, _world.Nut.QuantityOnHand);
            Assert.Equal(OrderStatus.Approved, order.Status);
        }

        [Fact]
        public async Task Fulfil_AllowNegativeStockOption_LetsItThrough()
        {
            _world.Options.AllowNegativeStock = true;
            Order order = _world.SeedOrder(OrderStatus.Approved, null, (_world.Nut, 50));

            Result<Order> result = await _world.OrderService.FulfilAsync(order.Id, order.RowVersion, null);

            Assert.True(result.IsSuccess);
            Assert.Equal(-45, _world.Nut.QuantityOnHand);
        }

        [Fact]
        public async Task Fulfil_FromSubmitted_ReturnsInvalidStatus()
        {
            Order order = _world.SeedOrder(OrderStatus.Submitted);

            Result<Order> result = await _world.OrderService.FulfilAsync(order.Id, order.RowVersion, null);

            Assert.Equal(ErrorCode.InvalidStatus, result.Code);
        }

        /* ---------------- cancel ---------------- */

        [Theory]
        [InlineData(OrderStatus.Draft)]
        [InlineData(OrderStatus.Submitted)]
        [InlineData(OrderStatus.Approved)]
        public async Task Cancel_BeforeFulfilment_Succeeds(OrderStatus from)
        {
            Order order = _world.SeedOrder(from);

            Result<Order> result = await _world.OrderService.CancelAsync(order.Id, order.RowVersion, "customer changed mind");

            Assert.True(result.IsSuccess, result.Message);
            Assert.Equal(OrderStatus.Cancelled, result.Value.Status);
            Assert.Equal(from, result.Value.History.Last().FromStatus);
        }

        [Theory]
        [InlineData(OrderStatus.Fulfilled)]
        [InlineData(OrderStatus.Invoiced)]
        [InlineData(OrderStatus.Cancelled)]
        public async Task Cancel_AfterFulfilment_ReturnsInvalidStatus(OrderStatus from)
        {
            Order order = _world.SeedOrder(from);

            Result<Order> result = await _world.OrderService.CancelAsync(order.Id, order.RowVersion, null);

            Assert.Equal(ErrorCode.InvalidStatus, result.Code);
        }

        [Fact]
        public async Task Transition_MissingOrder_ReturnsNotFound()
        {
            Result<Order> result = await _world.OrderService.SubmitAsync(404, new byte[0], null);

            Assert.Equal(ErrorCode.NotFound, result.Code);
        }
    }
}
