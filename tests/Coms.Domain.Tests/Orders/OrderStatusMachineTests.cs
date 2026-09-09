using Coms.Domain.Orders;
using Xunit;

namespace Coms.Domain.Tests.Orders
{
    public class OrderStatusMachineTests
    {
        [Theory]
        [InlineData(OrderStatus.Draft, OrderStatus.Submitted)]
        [InlineData(OrderStatus.Draft, OrderStatus.Cancelled)]
        [InlineData(OrderStatus.Submitted, OrderStatus.Approved)]
        [InlineData(OrderStatus.Submitted, OrderStatus.Cancelled)]
        [InlineData(OrderStatus.Approved, OrderStatus.Fulfilled)]
        [InlineData(OrderStatus.Approved, OrderStatus.Cancelled)]
        [InlineData(OrderStatus.Fulfilled, OrderStatus.Invoiced)]
        public void CanTransition_AllowedPairs_ReturnTrue(OrderStatus from, OrderStatus to)
        {
            Assert.True(OrderStatusMachine.CanTransition(from, to));
        }

        [Theory]
        [InlineData(OrderStatus.Draft, OrderStatus.Approved)]
        [InlineData(OrderStatus.Draft, OrderStatus.Fulfilled)]
        [InlineData(OrderStatus.Submitted, OrderStatus.Draft)]
        [InlineData(OrderStatus.Approved, OrderStatus.Submitted)]
        [InlineData(OrderStatus.Fulfilled, OrderStatus.Cancelled)]
        [InlineData(OrderStatus.Fulfilled, OrderStatus.Approved)]
        [InlineData(OrderStatus.Invoiced, OrderStatus.Cancelled)]
        [InlineData(OrderStatus.Invoiced, OrderStatus.Fulfilled)]
        [InlineData(OrderStatus.Cancelled, OrderStatus.Draft)]
        [InlineData(OrderStatus.Draft, OrderStatus.Draft)]
        public void CanTransition_DisallowedPairs_ReturnFalse(OrderStatus from, OrderStatus to)
        {
            Assert.False(OrderStatusMachine.CanTransition(from, to));
        }

        [Theory]
        [InlineData(OrderStatus.Invoiced, true)]
        [InlineData(OrderStatus.Cancelled, true)]
        [InlineData(OrderStatus.Draft, false)]
        [InlineData(OrderStatus.Fulfilled, false)]
        public void IsTerminal_MatchesTransitionTable(OrderStatus status, bool expected)
        {
            Assert.Equal(expected, OrderStatusMachine.IsTerminal(status));
        }

        [Theory]
        [InlineData(OrderStatus.Draft, true)]
        [InlineData(OrderStatus.Submitted, true)]
        [InlineData(OrderStatus.Approved, false)]
        [InlineData(OrderStatus.Fulfilled, false)]
        [InlineData(OrderStatus.Invoiced, false)]
        [InlineData(OrderStatus.Cancelled, false)]
        public void AllowsEditing_OnlyBeforeApproval(OrderStatus status, bool expected)
        {
            Assert.Equal(expected, OrderStatusMachine.AllowsEditing(status));
        }

        [Fact]
        public void AllowedTransitions_FromApproved_ListsFulfilledAndCancelled()
        {
            Assert.Equal(new[] { OrderStatus.Fulfilled, OrderStatus.Cancelled }, OrderStatusMachine.AllowedTransitions(OrderStatus.Approved));
        }

        [Fact]
        public void IsSale_OnlyFulfilledAndInvoiced()
        {
            Assert.True(OrderStatusMachine.IsSale(OrderStatus.Fulfilled));
            Assert.True(OrderStatusMachine.IsSale(OrderStatus.Invoiced));
            Assert.False(OrderStatusMachine.IsSale(OrderStatus.Approved));
            Assert.False(OrderStatusMachine.IsSale(OrderStatus.Cancelled));
        }
    }
}
