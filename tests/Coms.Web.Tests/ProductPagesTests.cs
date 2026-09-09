using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Xunit;

namespace Coms.Web.Tests
{
    [Collection(WebCollection.Name)]
    public class ProductPagesTests
    {
        private readonly WebFixture _web;

        public ProductPagesTests(WebFixture web)
        {
            _web = web;
        }

        [Fact]
        public async Task List_LowStockFilter_ShowsOnlyLowStock()
        {
            if (!_web.IsAvailable) { return; }

            HttpClient client = await _web.SignedInAsync("viewer", "Viewer#2026");

            HttpResponseMessage response = await client.GetAsync("/Products?lowStock=true&active=active");
            string body = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("<title>Low stock - Order Management</title>", body);
            Assert.Contains("row-warn", body);
            Assert.DoesNotContain("No products match", body);
        }

        [Fact]
        public async Task List_SortByPriceDescending_MarksHeader()
        {
            if (!_web.IsAvailable) { return; }

            HttpClient client = await _web.SignedInAsync("viewer", "Viewer#2026");

            HttpResponseMessage response = await client.GetAsync("/Products?sortBy=price&desc=true");
            string body = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("Price ▼", body);
        }

        [Fact]
        public async Task Details_ShowsPricingAndStock()
        {
            if (!_web.IsAvailable) { return; }

            HttpClient client = await _web.SignedInAsync("viewer", "Viewer#2026");

            HttpResponseMessage response = await client.GetAsync("/Products/Details/1");
            string body = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("FAS-0001", body);
            Assert.Contains("Unit margin", body);
            Assert.DoesNotContain("Adjust stock", body);   // read-only users get no action buttons
        }

        [Fact]
        public async Task Staff_AdjustStock_RequiresReasonThenSaves()
        {
            if (!_web.IsAvailable) { return; }

            HttpClient client = await _web.SignedInAsync("staff", "Staff#2026");
            HttpResponseMessage page = await client.GetAsync("/Products/AdjustStock/5");
            string html = await page.Content.ReadAsStringAsync();
            string token = await WebFixture.AntiForgeryTokenAsync(page);
            string rowVersion = Regex.Match(html, "name=\"RowVersion\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;

            HttpResponseMessage rejected = await client.PostAsync("/Products/AdjustStock", Form(token, new Dictionary<string, string>
            {
                ["Id"] = "5",
                ["RowVersion"] = rowVersion,
                ["NewQuantity"] = "77",
                ["Reason"] = ""
            }));
            Assert.Equal(HttpStatusCode.OK, rejected.StatusCode);
            string rejectedBody = await rejected.Content.ReadAsStringAsync();
            int reasonAt = rejectedBody.IndexOf("Reason", System.StringComparison.Ordinal);
            Assert.True(rejectedBody.Contains("reason for the stock adjustment is required"), rejectedBody.Substring(System.Math.Max(0, reasonAt - 300), System.Math.Min(1500, rejectedBody.Length - System.Math.Max(0, reasonAt - 300))));

            HttpResponseMessage saved = await client.PostAsync("/Products/AdjustStock", Form(token, new Dictionary<string, string>
            {
                ["Id"] = "5",
                ["RowVersion"] = rowVersion,
                ["NewQuantity"] = "77",
                ["Reason"] = "Web test count"
            }));
            Assert.Equal(HttpStatusCode.Redirect, saved.StatusCode);

            string details = await (await client.GetAsync("/Products/Details/5")).Content.ReadAsStringAsync();
            Assert.Contains("set to 77", details);
        }

        [Fact]
        public async Task Staff_CreateProduct_DuplicateSkuIsRejected()
        {
            if (!_web.IsAvailable) { return; }

            HttpClient client = await _web.SignedInAsync("staff", "Staff#2026");
            string token = await WebFixture.AntiForgeryTokenAsync(await client.GetAsync("/Products/Create"));

            HttpResponseMessage response = await client.PostAsync("/Products/Create", Form(token, new Dictionary<string, string>
            {
                ["Product.Sku"] = "fas-0001",
                ["Product.Name"] = "Duplicate",
                ["Product.Category"] = "Fasteners",
                ["Product.UnitOfMeasure"] = "EA",
                ["Product.UnitPrice"] = "1.00",
                ["Product.IsActive"] = "true"
            }));
            string body = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("already used", body);
        }

        [Fact]
        public async Task Admin_CreateProduct_Succeeds()
        {
            if (!_web.IsAvailable) { return; }

            HttpClient client = await _web.SignedInAsync("admin", "Admin#2026");
            string token = await WebFixture.AntiForgeryTokenAsync(await client.GetAsync("/Products/Create"));

            HttpResponseMessage response = await client.PostAsync("/Products/Create", Form(token, new Dictionary<string, string>
            {
                ["Product.Sku"] = "WEB-0001",
                ["Product.Name"] = "Web test product",
                ["Product.Category"] = "Testing",
                ["Product.UnitOfMeasure"] = "EA",
                ["Product.UnitPrice"] = "9.99",
                ["Product.CostPrice"] = "4.00",
                ["Product.QuantityOnHand"] = "12",
                ["Product.ReorderLevel"] = "2",
                ["Product.IsActive"] = "true"
            }));

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            string details = await (await client.GetAsync(response.Headers.Location!.ToString())).Content.ReadAsStringAsync();
            Assert.Contains("WEB-0001", details);
            Assert.Contains("Web test product", details);
        }

        private static FormUrlEncodedContent Form(string token, Dictionary<string, string> fields)
        {
            fields["__RequestVerificationToken"] = token;
            return new FormUrlEncodedContent(fields);
        }
    }
}
