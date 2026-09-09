using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;

namespace Coms.Web.Tests
{
    [Collection(WebCollection.Name)]
    public class CustomerPagesTests
    {
        private readonly WebFixture _web;

        public CustomerPagesTests(WebFixture web)
        {
            _web = web;
        }

        [Fact]
        public async Task Viewer_List_ShowsSeedCustomersAndNoNewButton()
        {
            if (!_web.IsAvailable) { return; }

            HttpClient client = await _web.SignedInAsync("viewer", "Viewer#2026");

            HttpResponseMessage response = await client.GetAsync("/Customers?search=Northgate&sortBy=name");
            string body = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("CUST-000001", body);
            Assert.Contains("Northgate Supply Co.", body);
            Assert.Contains("Showing 1&ndash;10 of 10", body);
            Assert.DoesNotContain("New customer", body);
        }

        [Fact]
        public async Task Viewer_CreatePage_IsForbidden()
        {
            if (!_web.IsAvailable) { return; }

            HttpClient client = await _web.SignedInAsync("viewer", "Viewer#2026");

            HttpResponseMessage response = await client.GetAsync("/Customers/Create");

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Contains("/Account/AccessDenied", response.Headers.Location!.ToString());
        }

        [Fact]
        public async Task Details_ShowsBalanceOrdersInvoicesAndNotes()
        {
            if (!_web.IsAvailable) { return; }

            HttpClient client = await _web.SignedInAsync("viewer", "Viewer#2026");

            HttpResponseMessage response = await client.GetAsync("/Customers/Details/1");
            string body = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("Outstanding", body);
            Assert.Contains("ORD-", body);
            Assert.Contains("INV-", body);
            Assert.Contains("<h2>Notes</h2>", body);
        }

        [Fact]
        public async Task Details_Missing_Returns404()
        {
            if (!_web.IsAvailable) { return; }

            HttpClient client = await _web.SignedInAsync("viewer", "Viewer#2026");

            HttpResponseMessage response = await client.GetAsync("/Customers/Details/999999");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task Staff_CreateCustomer_ValidationThenSuccess()
        {
            if (!_web.IsAvailable) { return; }

            HttpClient client = await _web.SignedInAsync("staff", "Staff#2026");
            string token = await WebFixture.AntiForgeryTokenAsync(await client.GetAsync("/Customers/Create"));

            // Missing name and billing city: stays on the form with field errors.
            HttpResponseMessage invalid = await client.PostAsync("/Customers/Create", Form(token, new Dictionary<string, string>
            {
                ["Name"] = "",
                ["BillingAddress.Line1"] = "1 Test St",
                ["BillingAddress.Country"] = "UK",
                ["PaymentTermsDays"] = "30",
                ["Status"] = "Active"
            }));
            string invalidBody = await invalid.Content.ReadAsStringAsync();

            Assert.True(invalid.StatusCode == HttpStatusCode.OK, "Status " + invalid.StatusCode + ": " + Excerpt(invalidBody));
            Assert.Contains("Name is required.", invalidBody);
            Assert.Contains("City is required.", invalidBody);

            // Complete: redirects to the new customer's page.
            HttpResponseMessage created = await client.PostAsync("/Customers/Create", Form(token, new Dictionary<string, string>
            {
                ["Name"] = "Web Test Customer",
                ["Email"] = "web@test.example",
                ["BillingAddress.Line1"] = "1 Test St",
                ["BillingAddress.City"] = "Testville",
                ["BillingAddress.Country"] = "UK",
                ["ShippingAddress.Line1"] = "",
                ["ShippingAddress.City"] = "",
                ["ShippingAddress.Country"] = "",
                ["PaymentTermsDays"] = "45",
                ["CreditLimit"] = "2500",
                ["Status"] = "Active"
            }));

            Assert.Equal(HttpStatusCode.Redirect, created.StatusCode);
            string location = created.Headers.Location!.ToString();
            Assert.Contains("/Customers/Details/", location);

            HttpResponseMessage details = await client.GetAsync(location);
            string detailsBody = await details.Content.ReadAsStringAsync();
            Assert.Contains("Web Test Customer", detailsBody);
            Assert.Contains("45 days", detailsBody);
            Assert.Contains("Same as billing", detailsBody);
            Assert.Contains("created.", detailsBody);
        }

        [Fact]
        public async Task Staff_EditCustomer_PersistsChange()
        {
            if (!_web.IsAvailable) { return; }

            HttpClient client = await _web.SignedInAsync("staff", "Staff#2026");
            HttpResponseMessage editPage = await client.GetAsync("/Customers/Edit/2");
            string editHtml = await editPage.Content.ReadAsStringAsync();
            string token = await WebFixture.AntiForgeryTokenAsync(editPage);
            string rowVersion = System.Text.RegularExpressions.Regex.Match(editHtml, "name=\"RowVersion\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;

            HttpResponseMessage saved = await client.PostAsync("/Customers/Edit/2", Form(token, new Dictionary<string, string>
            {
                ["Id"] = "2",
                ["RowVersion"] = rowVersion,
                ["Name"] = "Harbor Supply Co.",
                ["ContactName"] = "Edited Contact",
                ["BillingAddress.Line1"] = "1 Edited St",
                ["BillingAddress.City"] = "Denver",
                ["BillingAddress.Country"] = "USA",
                ["PaymentTermsDays"] = "30",
                ["Status"] = "Active"
            }));

            Assert.Equal(HttpStatusCode.Redirect, saved.StatusCode);

            string details = await (await client.GetAsync("/Customers/Details/2")).Content.ReadAsStringAsync();
            Assert.Contains("Edited Contact", details);
            Assert.Contains("1 Edited St", details);
        }

        [Fact]
        public async Task Viewer_PostChangeStatus_IsForbidden()
        {
            if (!_web.IsAvailable) { return; }

            HttpClient client = await _web.SignedInAsync("viewer", "Viewer#2026");
            string token = await WebFixture.AntiForgeryTokenAsync(await client.GetAsync("/Customers/Details/3"));

            HttpResponseMessage response = await client.PostAsync("/Customers/ChangeStatus", Form(token, new Dictionary<string, string>
            {
                ["Id"] = "3",
                ["Status"] = "Inactive"
            }));

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Contains("/Account/AccessDenied", response.Headers.Location!.ToString());
        }

        private static string Excerpt(string html)
        {
            int at = html.IndexOf("Exception", System.StringComparison.Ordinal);
            int start = at < 0 ? 0 : System.Math.Max(0, at - 200);
            return html.Substring(start, System.Math.Min(2500, html.Length - start));
        }

        private static FormUrlEncodedContent Form(string token, Dictionary<string, string> fields)
        {
            fields["__RequestVerificationToken"] = token;
            return new FormUrlEncodedContent(fields);
        }
    }
}
