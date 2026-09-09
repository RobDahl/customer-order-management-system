using Coms.Domain.Common;
using Xunit;

namespace Coms.Domain.Tests.Common
{
    public class MoneyTests
    {
        [Theory]
        [InlineData(1.005, 1.01)]
        [InlineData(1.004, 1.00)]
        [InlineData(2.675, 2.68)]
        [InlineData(-1.005, -1.01)]
        [InlineData(0, 0)]
        public void Round_RoundsHalfAwayFromZero_ToTwoPlaces(decimal input, decimal expected)
        {
            Assert.Equal(expected, Money.Round(input));
        }

        [Fact]
        public void ApplyPercent_ComputesRoundedPortion()
        {
            Assert.Equal(12.35m, Money.ApplyPercent(123.456m, 10m));
        }
    }
}
