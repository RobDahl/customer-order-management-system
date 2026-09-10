using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;

namespace Coms.Web.Tests
{
    [Collection(WebCollection.Name)]
    public class DashboardAndReportTests
    {
        private readonly WebFixture _web;

        public DashboardAndReportTests(WebFixture web)
        {
            _web = web;
        }

        [Fact]
        public async Task Dashboard_ShowsCountsSalesAndLists()
        {
            if (!_web.IsAvailable) { return; }

            HttpClient client = await _web.SignedInAsync("viewer", "Viewer#2026");

            string body = await (await client.GetAsync("/")).Content.ReadAsStringAsync();

            Assert.Contains("Awaiting approval", body);
            Assert.Contains("Sales, last twelve months", body);
            Assert.Contains("class=\"bar\"", body);
            Assert.Contains("<h2>Overdue invoices</h2>", body);
            Assert.Contains("<h2>Recent orders</h2>", body);
            Assert.Contains("<h2>Low stock</h2>", body);
        }

        [Fact]
        public async Task SalesByMonth_DefaultPeriod_RendersRowsAndTotals()
        {
            if (!_web.IsAvailable) { return; }

            HttpClient client = await _web.SignedInAsync("viewer", "Viewer#2026");

            HttpResponseMessage response = await client.GetAsync("/Reports/SalesByMonth");
            string body = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("2026-08", body);
            Assert.Contains("class=\"total\"", body);
            Assert.Contains("Export CSV", body);
        }

        [Fact]
        public async Task SalesByMonth_InvertedRange_ShowsValidationMessage()
        {
            if (!_web.IsAvailable) { return; }

            HttpClient client = await _web.SignedInAsync("viewer", "Viewer#2026");

            string body = await (await client.GetAsync("/Reports/SalesByMonth?from=2026-09-01&to=2026-01-01")).Content.ReadAsStringAsync();

            Assert.Contains("must not be after", body);
        }

        [Fact]
        public async Task Report_CsvExport_ReturnsCsvWithHeader()
        {
            if (!_web.IsAvailable) { return; }

            HttpClient client = await _web.SignedInAsync("viewer", "Viewer#2026");

            HttpResponseMessage response = await client.GetAsync("/Reports/TopCustomers?top=5&format=csv");
            string body = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.StartsWith("text/csv", response.Content.Headers.ContentType!.MediaType);
            Assert.Contains("top-customers-", response.Content.Headers.ContentDisposition!.FileName);
            Assert.StartsWith("﻿Rank,CustomerId,CustomerNumber,CustomerName", body);
            Assert.Equal(6, body.Split("\r\n", System.StringSplitOptions.RemoveEmptyEntries).Length);
        }

        [Theory]
        [InlineData("/Reports")]
        [InlineData("/Reports/TopCustomers")]
        [InlineData("/Reports/ProductSales")]
        [InlineData("/Reports/ArAging")]
        [InlineData("/Reports/LowStock")]
        [InlineData("/Orders/History?toStatus=Fulfilled")]
        public async Task ReportPages_RenderForViewer(string url)
        {
            if (!_web.IsAvailable) { return; }

            HttpClient client = await _web.SignedInAsync("viewer", "Viewer#2026");

            HttpResponseMessage response = await client.GetAsync(url);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task CustomersExport_HonoursFilter()
        {
            if (!_web.IsAvailable) { return; }

            HttpClient client = await _web.SignedInAsync("viewer", "Viewer#2026");

            HttpResponseMessage response = await client.GetAsync("/Customers/Export?search=Northgate");
            string body = await response.Content.ReadAsStringAsync();

            Assert.StartsWith("text/csv", response.Content.Headers.ContentType!.MediaType);
            string[] lines = body.Split("\r\n", System.StringSplitOptions.RemoveEmptyEntries);
            Assert.Equal(11, lines.Length);   // header + 10 Northgate customers
            Assert.Contains("CustomerNumber,Name,", lines[0]);
        }

        [Fact]
        public async Task AuditTrail_FiltersByStatus()
        {
            if (!_web.IsAvailable) { return; }

            HttpClient client = await _web.SignedInAsync("viewer", "Viewer#2026");

            string body = await (await client.GetAsync("/Orders/History?toStatus=Cancelled&pageSize=5")).Content.ReadAsStringAsync();

            Assert.Contains("status-cancelled", body);
            Assert.DoesNotContain("status-invoiced", body);
            Assert.Contains("ORD-", body);
        }

        [Fact]
        public async Task Health_ReportsOkWithoutSignIn()
        {
            if (!_web.IsAvailable) { return; }

            HttpResponseMessage response = await _web.NewClient().GetAsync("/health");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("OK", await response.Content.ReadAsStringAsync());
        }
    }
}
