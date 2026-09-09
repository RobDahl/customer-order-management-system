using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Xunit;

namespace Coms.Web.Tests
{
    [Collection(WebCollection.Name)]
    public class OrderPagesTests
    {
        private readonly WebFixture _web;

        public OrderPagesTests(WebFixture web)
        {
            _web = web;
        }

        [Fact]
        public async Task List_FilterByStatusAndCustomer_ShowsMatches()
        {
            if (!_web.IsAvailable) { return; }

            HttpClient client = await _web.SignedInAsync("viewer", "Viewer#2026");

            HttpResponseMessage response = await client.GetAsync("/Orders?status=Invoiced&customerId=1&sortBy=total&desc=true");
            string body = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("Customer: CUST-000001", body);
            Assert.Contains("Total ▼", body);
            Assert.Contains("INV-", body);
            Assert.DoesNotContain("New order", body);
        }

        [Fact]
        public async Task Details_Viewer_SeesNoActionButtons()
        {
            if (!_web.IsAvailable) { return; }

            int id = await _web.ScalarAsync<int>("SELECT MIN(Id) FROM dbo.Orders WHERE Status = 'Approved'");
            HttpClient client = await _web.SignedInAsync("viewer", "Viewer#2026");

            string body = await (await client.GetAsync("/Orders/Details/" + id)).Content.ReadAsStringAsync();

            Assert.Contains("Status history", body);
            Assert.Contains("Print pick list", body);
            Assert.DoesNotContain(">Fulfil<", body);
            Assert.DoesNotContain(">Cancel<", body);
        }

        [Fact]
        public async Task Details_Staff_SeesTransitionsForStatus()
        {
            if (!_web.IsAvailable) { return; }

            int id = await _web.ScalarAsync<int>("SELECT MIN(Id) FROM dbo.Orders WHERE Status = 'Submitted'");
            HttpClient client = await _web.SignedInAsync("staff", "Staff#2026");

            string body = await (await client.GetAsync("/Orders/Details/" + id)).Content.ReadAsStringAsync();

            Assert.Contains("Approve", body);
            Assert.Contains("Cancel", body);
            Assert.Contains(">Edit<", body);
            Assert.DoesNotContain("Create invoice", body);
        }

        [Fact]
        public async Task Staff_CreateOrder_AddLineThenSave_ThenSubmitApproveFulfilInvoice()
        {
            if (!_web.IsAvailable) { return; }

            // Two active products with plenty of stock, so fulfilment cannot fail on availability.
            int productA = await _web.ScalarAsync<int>("SELECT MIN(Id) FROM dbo.Products WHERE IsActive = 1 AND QuantityOnHand >= 100");
            int productB = await _web.ScalarAsync<int>("SELECT MAX(Id) FROM dbo.Products WHERE IsActive = 1 AND QuantityOnHand >= 100");
            string skuA = await _web.ScalarAsync<string>("SELECT Sku FROM dbo.Products WHERE Id = @Id", new { Id = productA });

            HttpClient client = await _web.SignedInAsync("staff", "Staff#2026");
            string token = await WebFixture.AntiForgeryTokenAsync(await client.GetAsync("/Orders/Create"));

            // Add a second line: form comes back with two rows and no save.
            HttpResponseMessage added = await client.PostAsync("/Orders/Create", Form(token, new Dictionary<string, string>
            {
                ["Input.CustomerId"] = "1",
                ["Input.OrderDate"] = "2026-09-09",
                ["Input.Lines[0].ProductId"] = productA.ToString(),
                ["Input.Lines[0].Quantity"] = "3",
                ["action"] = "addLine"
            }));
            string addedBody = await added.Content.ReadAsStringAsync();
            Assert.Equal(HttpStatusCode.OK, added.StatusCode);
            Assert.Contains("Input.Lines[1].ProductId", addedBody);
            Assert.Contains("removeLine-1", addedBody);

            // Save with both lines.
            HttpResponseMessage saved = await client.PostAsync("/Orders/Create", Form(token, new Dictionary<string, string>
            {
                ["Input.CustomerId"] = "1",
                ["Input.OrderDate"] = "2026-09-09",
                ["Input.RequiredDate"] = "2026-09-20",
                ["Input.CustomerReference"] = "WEB-PO-1",
                ["Input.Lines[0].ProductId"] = productA.ToString(),
                ["Input.Lines[0].Quantity"] = "3",
                ["Input.Lines[1].ProductId"] = productB.ToString(),
                ["Input.Lines[1].Quantity"] = "1",
                ["Input.Lines[1].DiscountPercent"] = "10",
                ["action"] = "save"
            }));
            Assert.Equal(HttpStatusCode.Redirect, saved.StatusCode);
            string detailsUrl = saved.Headers.Location!.ToString();
            int orderId = int.Parse(Regex.Match(detailsUrl, @"/Orders/Details/(\d+)").Groups[1].Value);

            string details = await (await client.GetAsync(detailsUrl)).Content.ReadAsStringAsync();
            Assert.Contains("created as a draft", details);
            Assert.Contains("WEB-PO-1", details);
            Assert.Contains(skuA, details);

            // Walk the workflow: Submit -> Approve -> Fulfil, then invoice it.
            string submitted = await TransitionAsync(client, orderId, "Submitted");
            Assert.Contains("is now Submitted", submitted);
            string approved = await TransitionAsync(client, orderId, "Approved");
            Assert.Contains("is now Approved", approved);
            string fulfilled = await TransitionAsync(client, orderId, "Fulfilled");
            Assert.Contains("is now Fulfilled", fulfilled);
            Assert.Contains("Create invoice", fulfilled);

            HttpResponseMessage createPage = await client.GetAsync("/Invoices/CreateFromOrder?orderId=" + orderId);
            string createHtml = await createPage.Content.ReadAsStringAsync();
            string createToken = await WebFixture.AntiForgeryTokenAsync(createPage);
            string orderVersion = Regex.Match(createHtml, "name=\"RowVersion\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;

            HttpResponseMessage issued = await client.PostAsync("/Invoices/CreateFromOrder", Form(createToken, new Dictionary<string, string>
            {
                ["OrderId"] = orderId.ToString(),
                ["RowVersion"] = orderVersion,
                ["IssuedDate"] = "2026-09-10"
            }));
            Assert.Equal(HttpStatusCode.Redirect, issued.StatusCode);
            string invoiceUrl = issued.Headers.Location!.ToString();
            Assert.Contains("/Invoices/Details/", invoiceUrl);

            string invoice = await (await client.GetAsync(invoiceUrl)).Content.ReadAsStringAsync();
            Assert.Contains("issued.", invoice);
            Assert.Contains("Record payment", invoice);
            Assert.Contains("INV-2026-", invoice);
        }

        [Fact]
        public async Task Staff_EditApprovedOrder_IsRedirectedWithMessage()
        {
            if (!_web.IsAvailable) { return; }

            int id = await _web.ScalarAsync<int>("SELECT MIN(Id) FROM dbo.Orders WHERE Status = 'Approved'");
            HttpClient client = await _web.SignedInAsync("staff", "Staff#2026");

            HttpResponseMessage response = await client.GetAsync("/Orders/Edit/" + id);

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            string details = await (await client.GetAsync(response.Headers.Location!.ToString())).Content.ReadAsStringAsync();
            Assert.Contains("can no longer be edited", details);
        }

        [Fact]
        public async Task Transition_Cancel_FromDraft_Succeeds()
        {
            if (!_web.IsAvailable) { return; }

            int id = await _web.ScalarAsync<int>("SELECT MIN(Id) FROM dbo.Orders WHERE Status = 'Draft'");
            HttpClient client = await _web.SignedInAsync("staff", "Staff#2026");

            string body = await TransitionAsync(client, id, "Cancelled", "test cancel");

            Assert.Contains("is now Cancelled", body);
            Assert.Contains("test cancel", body);
        }

        [Fact]
        public async Task Print_ConfirmationAndPickList_Render()
        {
            if (!_web.IsAvailable) { return; }

            int id = await _web.ScalarAsync<int>("SELECT MIN(Id) FROM dbo.Orders WHERE Status = 'Approved'");
            HttpClient client = await _web.SignedInAsync("viewer", "Viewer#2026");

            string confirmation = await (await client.GetAsync("/Orders/Print/" + id)).Content.ReadAsStringAsync();
            string pickList = await (await client.GetAsync("/Orders/Print/" + id + "?kind=picklist")).Content.ReadAsStringAsync();

            Assert.Contains("ORDER CONFIRMATION", confirmation);
            Assert.Contains("Subtotal", confirmation);
            Assert.Contains("PICK LIST", pickList);
            Assert.Contains("Picked by", pickList);
            Assert.DoesNotContain("Subtotal", pickList);
        }

        private static async Task<string> TransitionAsync(HttpClient client, int orderId, string to, string? comment = null)
        {
            HttpResponseMessage page = await client.GetAsync("/Orders/Transition/" + orderId + "?to=" + to);
            Assert.Equal(HttpStatusCode.OK, page.StatusCode);
            string html = await page.Content.ReadAsStringAsync();
            string token = await WebFixture.AntiForgeryTokenAsync(page);
            string rowVersion = Regex.Match(html, "name=\"RowVersion\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;

            HttpResponseMessage response = await client.PostAsync("/Orders/Transition", Form(token, new Dictionary<string, string>
            {
                ["Id"] = orderId.ToString(),
                ["To"] = to,
                ["RowVersion"] = rowVersion,
                ["Comment"] = comment ?? ""
            }));
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

            return await (await client.GetAsync(response.Headers.Location!.ToString())).Content.ReadAsStringAsync();
        }

        private static FormUrlEncodedContent Form(string token, Dictionary<string, string> fields)
        {
            fields["__RequestVerificationToken"] = token;
            return new FormUrlEncodedContent(fields);
        }
    }
}
