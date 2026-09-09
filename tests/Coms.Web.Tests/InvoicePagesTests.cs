using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Xunit;

namespace Coms.Web.Tests
{
    [Collection(WebCollection.Name)]
    public class InvoicePagesTests
    {
        private readonly WebFixture _web;

        public InvoicePagesTests(WebFixture web)
        {
            _web = web;
        }

        [Fact]
        public async Task List_OverdueOnly_ShowsOverdueRows()
        {
            if (!_web.IsAvailable) { return; }

            HttpClient client = await _web.SignedInAsync("viewer", "Viewer#2026");

            HttpResponseMessage response = await client.GetAsync("/Invoices?overdue=true&sortBy=due");
            string body = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("<title>Overdue invoices - Order Management</title>", body);
            Assert.Contains(" d</td>", body);
        }

        [Fact]
        public async Task Details_ShowsPaymentsAndLines()
        {
            if (!_web.IsAvailable) { return; }

            int id = await _web.ScalarAsync<int>("SELECT MIN(Id) FROM dbo.Invoices WHERE Status = 'PartiallyPaid'");
            HttpClient client = await _web.SignedInAsync("viewer", "Viewer#2026");

            string body = await (await client.GetAsync("/Invoices/Details/" + id)).Content.ReadAsStringAsync();

            Assert.Contains("<h2>Payments</h2>", body);
            Assert.Contains("<h2>Lines</h2>", body);
            Assert.Contains("Balance", body);
            Assert.DoesNotContain("Record payment", body);
        }

        [Fact]
        public async Task Staff_RecordPayment_OverpayRejected_ThenPartialAccepted()
        {
            if (!_web.IsAvailable) { return; }

            int id = await _web.ScalarAsync<int>("SELECT MIN(Id) FROM dbo.Invoices WHERE Status = 'Open'");
            decimal total = await _web.ScalarAsync<decimal>("SELECT Total FROM dbo.Invoices WHERE Id = @Id", new { Id = id });
            HttpClient client = await _web.SignedInAsync("staff", "Staff#2026");

            HttpResponseMessage page = await client.GetAsync("/Invoices/Payment/" + id);
            string html = await page.Content.ReadAsStringAsync();
            string token = await WebFixture.AntiForgeryTokenAsync(page);
            string rowVersion = Regex.Match(html, "name=\"RowVersion\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;

            HttpResponseMessage rejected = await client.PostAsync("/Invoices/Payment", Form(token, new Dictionary<string, string>
            {
                ["InvoiceId"] = id.ToString(),
                ["RowVersion"] = rowVersion,
                ["Amount"] = (total + 1).ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["PaidDate"] = "2026-09-09",
                ["Method"] = "Cash"
            }));
            Assert.Equal(HttpStatusCode.OK, rejected.StatusCode);
            Assert.Contains("exceeds the outstanding balance", await rejected.Content.ReadAsStringAsync());

            HttpResponseMessage accepted = await client.PostAsync("/Invoices/Payment", Form(token, new Dictionary<string, string>
            {
                ["InvoiceId"] = id.ToString(),
                ["RowVersion"] = rowVersion,
                ["Amount"] = "10.00",
                ["PaidDate"] = "2026-09-09",
                ["Method"] = "Cheque",
                ["Reference"] = "WEB-CHK-1"
            }));
            Assert.Equal(HttpStatusCode.Redirect, accepted.StatusCode);

            string details = await (await client.GetAsync("/Invoices/Details/" + id)).Content.ReadAsStringAsync();
            Assert.Contains("Payment of 10.00 recorded", details);
            Assert.Contains("WEB-CHK-1", details);
            Assert.Contains("PartiallyPaid", details);
        }

        [Fact]
        public async Task Staff_VoidUnpaidInvoice_ReturnsOrderToFulfilled()
        {
            if (!_web.IsAvailable) { return; }

            int id = await _web.ScalarAsync<int>("SELECT MAX(Id) FROM dbo.Invoices WHERE Status = 'Open' AND AmountPaid = 0");
            int orderId = await _web.ScalarAsync<int>("SELECT OrderId FROM dbo.Invoices WHERE Id = @Id", new { Id = id });
            HttpClient client = await _web.SignedInAsync("staff", "Staff#2026");

            HttpResponseMessage page = await client.GetAsync("/Invoices/Void/" + id);
            string html = await page.Content.ReadAsStringAsync();
            string token = await WebFixture.AntiForgeryTokenAsync(page);
            string rowVersion = Regex.Match(html, "name=\"RowVersion\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;

            HttpResponseMessage voided = await client.PostAsync("/Invoices/Void", Form(token, new Dictionary<string, string>
            {
                ["Id"] = id.ToString(),
                ["RowVersion"] = rowVersion,
                ["Comment"] = "web test void"
            }));
            Assert.Equal(HttpStatusCode.Redirect, voided.StatusCode);

            string details = await (await client.GetAsync("/Invoices/Details/" + id)).Content.ReadAsStringAsync();
            Assert.Contains("voided", details);
            Assert.Contains("status-void", details);

            string order = await (await client.GetAsync("/Orders/Details/" + orderId)).Content.ReadAsStringAsync();
            Assert.Contains("Create invoice", order);
        }

        [Fact]
        public async Task Print_Invoice_Renders()
        {
            if (!_web.IsAvailable) { return; }

            int id = await _web.ScalarAsync<int>("SELECT MIN(Id) FROM dbo.Invoices WHERE Status = 'Paid'");
            HttpClient client = await _web.SignedInAsync("viewer", "Viewer#2026");

            string body = await (await client.GetAsync("/Invoices/Print/" + id)).Content.ReadAsStringAsync();

            Assert.Contains(">INVOICE<", body);
            Assert.Contains("Bill to", body);
            Assert.Contains("Balance due", body);
        }

        [Fact]
        public async Task Statement_ShowsInvoicesAndPayments()
        {
            if (!_web.IsAvailable) { return; }

            HttpClient client = await _web.SignedInAsync("viewer", "Viewer#2026");

            HttpResponseMessage response = await client.GetAsync("/Customers/Statement/1");
            string body = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("<h2>Invoices</h2>", body);
            Assert.Contains("<h2>Payments</h2>", body);
            Assert.Contains("Outstanding", body);
            Assert.Contains("INV-", body);
        }

        private static FormUrlEncodedContent Form(string token, Dictionary<string, string> fields)
        {
            fields["__RequestVerificationToken"] = token;
            return new FormUrlEncodedContent(fields);
        }
    }
}
