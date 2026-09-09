using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Coms.Application.Reports;
using Coms.Application.Tests.Fakes;
using Coms.Domain.Common;
using Coms.Domain.Reports;
using Xunit;

namespace Coms.Application.Tests.Reports
{
    public class ReportServiceTests
    {
        private readonly TestWorld _world = new TestWorld();

        [Fact]
        public async Task SalesByMonth_InvertedPeriod_IsRejected()
        {
            var period = new ReportPeriod(new DateTime(2026, 9, 1), new DateTime(2026, 1, 1));

            Result<IReadOnlyList<SalesByMonthRow>> result = await _world.ReportService.GetSalesByMonthAsync(period);

            Assert.Equal(ErrorCode.Validation, result.Code);
        }

        [Fact]
        public async Task SalesByMonth_PeriodOverFiveYears_IsRejected()
        {
            var period = new ReportPeriod(new DateTime(2020, 1, 1), new DateTime(2026, 9, 1));

            Result<IReadOnlyList<SalesByMonthRow>> result = await _world.ReportService.GetSalesByMonthAsync(period);

            Assert.Equal(ErrorCode.Validation, result.Code);
        }

        [Fact]
        public async Task TopCustomers_ClampsTop()
        {
            var period = ReportPeriod.LastTwelveMonths(new DateTime(2026, 9, 9));

            await _world.ReportService.GetTopCustomersAsync(period, 0);
            await _world.ReportService.GetTopCustomersAsync(period, 5000);

            Assert.Equal(ReportService.DefaultTop, _world.Reports.TopCustomerCalls[0].Top);
            Assert.Equal(ReportService.MaxTop, _world.Reports.TopCustomerCalls[1].Top);
        }

        [Fact]
        public void LastTwelveMonths_CoversWholeMonths()
        {
            ReportPeriod period = ReportPeriod.LastTwelveMonths(new DateTime(2026, 9, 9));

            Assert.Equal(new DateTime(2025, 10, 1), period.FromDate);
            Assert.Equal(new DateTime(2026, 9, 30), period.ToDate);
            Assert.Empty(period.Validate());
        }
    }
}
