using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Coms.Data.Connections;
using Coms.Data.Repositories;
using Coms.Domain.Reports;
using Xunit;

namespace Coms.Data.Tests.Repositories
{
    [Collection(DatabaseCollection.Name)]
    public class ReportRepositoryTests
    {
        private static readonly DateTime From = new DateTime(2025, 9, 1);
        private static readonly DateTime To = new DateTime(2026, 9, 1);

        private readonly DatabaseFixture _db;

        public ReportRepositoryTests(DatabaseFixture db)
        {
            _db = db;
        }

        [Fact]
        public async Task DashboardCounts_MatchSeed()
        {
            if (!_db.IsAvailable) { return; }

            using (DbSession session = _db.OpenSession())
            {
                DashboardCounts counts = await new ReportRepository(session).GetDashboardCountsAsync();

                Assert.Equal(177, counts.ActiveCustomers);
                Assert.Equal(10, counts.OnHoldCustomers);
                Assert.True(counts.OpenInvoices > 0);
                Assert.True(counts.OverdueInvoices <= counts.OpenInvoices);
                Assert.True(counts.OverdueBalance <= counts.OpenInvoiceBalance);
                Assert.Equal(DateTime.UtcNow.Date, counts.AsOfDate);
            }
        }

        [Fact]
        public async Task SalesByMonth_ReturnsEveryMonthInRange()
        {
            if (!_db.IsAvailable) { return; }

            using (DbSession session = _db.OpenSession())
            {
                IReadOnlyList<SalesByMonthRow> rows = await new ReportRepository(session).GetSalesByMonthAsync(From, To);

                Assert.Equal(13, rows.Count);
                Assert.Equal("2025-09", rows[0].MonthLabel);
                Assert.Equal("2026-09", rows[12].MonthLabel);
                Assert.All(rows, r => Assert.Equal(r.Subtotal + r.TaxAmount, r.Total));
            }
        }

        [Fact]
        public async Task TopCustomers_RanksAndShares()
        {
            if (!_db.IsAvailable) { return; }

            using (DbSession session = _db.OpenSession())
            {
                IReadOnlyList<TopCustomerRow> rows = await new ReportRepository(session).GetTopCustomersAsync(From, To, 5);

                Assert.Equal(5, rows.Count);
                Assert.Equal(new long[] { 1, 2, 3, 4, 5 }, rows.Select(r => r.Rank).ToArray());
                Assert.True(rows[0].Total >= rows[4].Total);
                Assert.True(rows.Sum(r => r.SharePercent) < 100);
            }
        }

        [Fact]
        public async Task ArAging_BucketsAddUp()
        {
            if (!_db.IsAvailable) { return; }

            using (DbSession session = _db.OpenSession())
            {
                IReadOnlyList<ArAgingRow> rows = await new ReportRepository(session).GetArAgingAsync(new DateTime(2026, 9, 1));

                Assert.NotEmpty(rows);
                Assert.All(rows, r => Assert.Equal(r.NotYetDue + r.Days1To30 + r.Days31To60 + r.Days61To90 + r.Over90, r.TotalOutstanding));
            }
        }

        [Fact]
        public async Task ProductSales_MarginIsRevenueMinusCost()
        {
            if (!_db.IsAvailable) { return; }

            using (DbSession session = _db.OpenSession())
            {
                IReadOnlyList<ProductSalesRow> rows = await new ReportRepository(session).GetProductSalesAsync(From, To);

                Assert.NotEmpty(rows);
                Assert.All(rows, r => Assert.Equal(r.Revenue - r.Cost, r.GrossMargin));
                Assert.True(rows[0].Revenue >= rows[rows.Count - 1].Revenue);
            }
        }

        [Fact]
        public async Task LowStock_CountMatchesDashboard()
        {
            if (!_db.IsAvailable) { return; }

            using (DbSession session = _db.OpenSession())
            {
                var repository = new ReportRepository(session);
                IReadOnlyList<LowStockRow> rows = await repository.GetLowStockAsync();
                DashboardCounts counts = await repository.GetDashboardCountsAsync();

                Assert.Equal(counts.LowStockProducts, rows.Count);
                Assert.All(rows, r => Assert.True(r.ProjectedOnHand <= r.ReorderLevel));
            }
        }

        [Fact]
        public async Task InvoiceAging_ForCustomer_FiltersAndBuckets()
        {
            if (!_db.IsAvailable) { return; }

            using (DbSession session = _db.OpenSession())
            {
                IReadOnlyList<InvoiceAgingRow> rows = await new ReportRepository(session).GetInvoiceAgingAsync(1);

                Assert.NotEmpty(rows);
                Assert.All(rows, r => Assert.Equal(1, r.CustomerId));
                Assert.All(rows, r => Assert.Contains(r.AgeBucket, new[] { "Current", "1-30", "31-60", "61-90", "90+" }));
                Assert.All(rows, r => Assert.True(r.DaysOverdue > 0 ? r.AgeBucket != "Current" : r.AgeBucket == "Current"));
            }
        }
    }
}
