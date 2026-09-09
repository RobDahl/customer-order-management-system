using System;
using System.Collections.Generic;
using Coms.Domain.Common;
using Coms.Domain.Orders;
using Coms.Domain.Products;
using Xunit;

namespace Coms.Domain.Tests.Orders
{
    public class OrderCalculationTests
    {
        [Fact]
        public void Recalculate_RoundsEachLineThenSums()
        {
            var order = new Order { CustomerId = 1, TaxRate = 0.08m };
            order.Lines.Add(new OrderLine { ProductId = 1, Description = "A", Quantity = 3, UnitPrice = 10.005m });
            order.Lines.Add(new OrderLine { ProductId = 2, Description = "B", Quantity = 1.5m, UnitPrice = 20m, DiscountPercent = 10 });

            order.Recalculate();

            Assert.Equal(30.02m, order.Lines[0].LineTotal);   // 30.015 rounds away from zero
            Assert.Equal(27.00m, order.Lines[1].LineTotal);   // 30 less 10%
            Assert.Equal(57.02m, order.Subtotal);
            Assert.Equal(4.56m, order.TaxAmount);             // 4.5616
            Assert.Equal(61.58m, order.Total);
        }

        [Fact]
        public void Recalculate_RenumbersLinesSequentially()
        {
            var order = new Order();
            order.Lines.Add(new OrderLine { LineNumber = 7, Quantity = 1, UnitPrice = 1 });
            order.Lines.Add(new OrderLine { LineNumber = 7, Quantity = 1, UnitPrice = 1 });
            order.Lines.Add(new OrderLine { LineNumber = 2, Quantity = 1, UnitPrice = 1 });

            order.Recalculate();

            Assert.Equal(new[] { 1, 2, 3 }, new[] { order.Lines[0].LineNumber, order.Lines[1].LineNumber, order.Lines[2].LineNumber });
        }

        [Fact]
        public void Recalculate_NoLines_GivesZeroTotals()
        {
            var order = new Order { TaxRate = 0.2m, Subtotal = 99, TaxAmount = 9, Total = 108 };

            order.Recalculate();

            Assert.Equal(0m, order.Subtotal);
            Assert.Equal(0m, order.TaxAmount);
            Assert.Equal(0m, order.Total);
        }

        [Fact]
        public void FromProduct_SnapshotsPriceCostSkuAndName()
        {
            var product = new ProductLookup { Id = 9, Sku = "ABC-1", Name = "Widget", UnitPrice = 5.5m, CostPrice = 3m, UnitOfMeasure = "EA" };

            OrderLine line = OrderLine.FromProduct(product, 4);

            Assert.Equal(9, line.ProductId);
            Assert.Equal("ABC-1", line.Sku);
            Assert.Equal("Widget", line.Description);
            Assert.Equal(5.5m, line.UnitPrice);
            Assert.Equal(3m, line.UnitCost);
            Assert.Equal(4m, line.Quantity);
        }

        [Fact]
        public void Validate_ValidOrder_HasNoErrors()
        {
            Order order = ValidOrder();

            Assert.Empty(order.Validate());
        }

        [Fact]
        public void Validate_RequiredDateBeforeOrderDate_Fails()
        {
            Order order = ValidOrder();
            order.RequiredDate = order.OrderDate.AddDays(-1);

            IReadOnlyList<ValidationError> errors = order.Validate();

            Assert.Contains(errors, e => e.Field == nameof(Order.RequiredDate));
        }

        [Fact]
        public void Validate_LineErrors_ArePrefixedWithIndex()
        {
            Order order = ValidOrder();
            order.Lines[0].Quantity = 0;
            order.Lines[0].DiscountPercent = 150;

            IReadOnlyList<ValidationError> errors = order.Validate();

            Assert.Contains(errors, e => e.Field == "Lines[0].Quantity");
            Assert.Contains(errors, e => e.Field == "Lines[0].DiscountPercent");
        }

        [Fact]
        public void Validate_DuplicateLineNumbers_Fails()
        {
            Order order = ValidOrder();
            order.Lines.Add(new OrderLine { ProductId = 3, Description = "C", Quantity = 1, UnitPrice = 1, LineNumber = 1 });

            Assert.Contains(order.Validate(), e => e.Field == nameof(Order.Lines));
        }

        [Fact]
        public void Validate_TaxRateOutOfRange_Fails()
        {
            Order order = ValidOrder();
            order.TaxRate = 8;   // someone typed a percentage

            Assert.Contains(order.Validate(), e => e.Field == nameof(Order.TaxRate));
        }

        [Fact]
        public void CanTransitionTo_UsesStateMachine()
        {
            var order = new Order { Status = OrderStatus.Approved };

            Assert.True(order.CanTransitionTo(OrderStatus.Fulfilled));
            Assert.False(order.CanTransitionTo(OrderStatus.Submitted));
            Assert.False(order.CanEdit);
        }

        private static Order ValidOrder()
        {
            var order = new Order
            {
                CustomerId = 1,
                OrderDate = new DateTime(2026, 9, 9),
                RequiredDate = new DateTime(2026, 9, 20),
                TaxRate = 0.08m
            };
            order.Lines.Add(new OrderLine { ProductId = 1, Description = "A", Quantity = 2, UnitPrice = 10 });
            order.Lines.Add(new OrderLine { ProductId = 2, Description = "B", Quantity = 1, UnitPrice = 5 });
            order.Recalculate();
            return order;
        }
    }
}
