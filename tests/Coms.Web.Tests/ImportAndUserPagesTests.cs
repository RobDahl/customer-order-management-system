using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Coms.Web.Tests
{
    [Collection(WebCollection.Name)]
    public class ImportAndUserPagesTests
    {
        private readonly WebFixture _web;

        public ImportAndUserPagesTests(WebFixture web)
        {
            _web = web;
        }

        [Fact]
        public async Task Import_Viewer_IsForbidden()
        {
            if (!_web.IsAvailable) { return; }

            HttpClient client = await _web.SignedInAsync("viewer", "Viewer#2026");

            HttpResponseMessage response = await client.GetAsync("/Import");

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Contains("/Account/AccessDenied", response.Headers.Location!.ToString());
        }

        [Fact]
        public async Task Import_Template_DownloadsHeaderRow()
        {
            if (!_web.IsAvailable) { return; }

            HttpClient client = await _web.SignedInAsync("staff", "Staff#2026");

            HttpResponseMessage response = await client.GetAsync("/Import/Template?kind=products");
            string body = await response.Content.ReadAsStringAsync();

            Assert.StartsWith("text/csv", response.Content.Headers.ContentType!.MediaType);
            Assert.Contains("Sku,Name,Description,Category", body);
        }

        [Fact]
        public async Task Import_CheckThenImport_Customers()
        {
            if (!_web.IsAvailable) { return; }

            HttpClient client = await _web.SignedInAsync("staff", "Staff#2026");
            string token = await WebFixture.AntiForgeryTokenAsync(await client.GetAsync("/Import/Upload?kind=customers"));

            const string badFile = "Name,BillingLine1,BillingCity,BillingCountry,PaymentTermsDays\r\nImport Co,1 St,Town,UK,abc\r\n";
            HttpResponseMessage checkedBad = await client.PostAsync("/Import/Upload", Upload(token, "customers", badFile, "check"));
            string checkedBody = await checkedBad.Content.ReadAsStringAsync();
            Assert.Equal(HttpStatusCode.OK, checkedBad.StatusCode);
            Assert.Contains("Check result", checkedBody);
            Assert.Contains("not a whole number", checkedBody);
            Assert.Contains("Nothing was imported", checkedBody);

            const string goodFile = "Name,BillingLine1,BillingCity,BillingCountry,PaymentTermsDays\r\nImport Co One,1 St,Town,UK,14\r\nImport Co Two,2 St,Town,UK,30\r\n";
            HttpResponseMessage checkedGood = await client.PostAsync("/Import/Upload", Upload(token, "customers", goodFile, "check"));
            Assert.Contains("Would import 2 new", await checkedGood.Content.ReadAsStringAsync());

            HttpResponseMessage imported = await client.PostAsync("/Import/Upload", Upload(token, "customers", goodFile, "import"));
            string importedBody = await imported.Content.ReadAsStringAsync();
            Assert.Contains("Imported 2 new", importedBody);

            string list = await (await client.GetAsync("/Customers?search=Import%20Co")).Content.ReadAsStringAsync();
            Assert.Contains("Import Co One", list);
            Assert.Contains("Import Co Two", list);
        }

        [Fact]
        public async Task Import_NoFile_ShowsMessage()
        {
            if (!_web.IsAvailable) { return; }

            HttpClient client = await _web.SignedInAsync("staff", "Staff#2026");
            string token = await WebFixture.AntiForgeryTokenAsync(await client.GetAsync("/Import/Upload?kind=products"));

            var form = new MultipartFormDataContent
            {
                { new StringContent(token), "__RequestVerificationToken" },
                { new StringContent("products"), "Kind" },
                { new StringContent("check"), "action" }
            };
            HttpResponseMessage response = await client.PostAsync("/Import/Upload", form);

            Assert.Contains("Choose a CSV file", await response.Content.ReadAsStringAsync());
        }

        [Fact]
        public async Task Users_StaffForbidden_AdminSeesList()
        {
            if (!_web.IsAvailable) { return; }

            HttpClient staff = await _web.SignedInAsync("staff", "Staff#2026");
            HttpResponseMessage forbidden = await staff.GetAsync("/Users");
            Assert.Equal(HttpStatusCode.Redirect, forbidden.StatusCode);
            Assert.Contains("/Account/AccessDenied", forbidden.Headers.Location!.ToString());

            HttpClient admin = await _web.SignedInAsync("admin", "Admin#2026");
            string body = await (await admin.GetAsync("/Users")).Content.ReadAsStringAsync();
            Assert.Contains("admin", body);
            Assert.Contains("staff", body);
            Assert.Contains("viewer", body);
            Assert.Contains("Administrator", body);
        }

        [Fact]
        public async Task Users_AdminCreatesUser_WeakPasswordRejectedThenSuccess()
        {
            if (!_web.IsAvailable) { return; }

            HttpClient admin = await _web.SignedInAsync("admin", "Admin#2026");
            string token = await WebFixture.AntiForgeryTokenAsync(await admin.GetAsync("/Users/Create"));

            HttpResponseMessage weak = await admin.PostAsync("/Users/Create", Form(token, new Dictionary<string, string>
            {
                ["UserName"] = "clerk",
                ["Email"] = "clerk@example.com",
                ["Password"] = "short",
                ["Role"] = "Staff"
            }));
            Assert.Equal(HttpStatusCode.OK, weak.StatusCode);
            Assert.Contains("at least 8 characters", await weak.Content.ReadAsStringAsync());

            HttpResponseMessage created = await admin.PostAsync("/Users/Create", Form(token, new Dictionary<string, string>
            {
                ["UserName"] = "clerk",
                ["Email"] = "clerk@example.com",
                ["Password"] = "Clerk#2026",
                ["Role"] = "Staff"
            }));
            Assert.Equal(HttpStatusCode.Redirect, created.StatusCode);

            string list = await (await admin.GetAsync("/Users")).Content.ReadAsStringAsync();
            Assert.Contains("clerk", list);
            Assert.Contains("User clerk created", list);

            // The new user can sign in with the Staff role.
            HttpClient clerk = await _web.SignedInAsync("clerk", "Clerk#2026");
            string dashboard = await (await clerk.GetAsync("/")).Content.ReadAsStringAsync();
            Assert.Contains("clerk (Staff)", dashboard);
        }

        [Fact]
        public async Task Users_AdminCannotRemoveOwnAdminRole()
        {
            if (!_web.IsAvailable) { return; }

            HttpClient admin = await _web.SignedInAsync("admin", "Admin#2026");
            string list = await (await admin.GetAsync("/Users")).Content.ReadAsStringAsync();
            string id = System.Text.RegularExpressions.Regex.Match(list, "<td>admin</td>.*?/Users/Edit/([^\"]+)\"", System.Text.RegularExpressions.RegexOptions.Singleline).Groups[1].Value;
            HttpResponseMessage page = await admin.GetAsync("/Users/Edit/" + id);
            string token = await WebFixture.AntiForgeryTokenAsync(page);

            HttpResponseMessage response = await admin.PostAsync("/Users/Edit", Form(token, new Dictionary<string, string>
            {
                ["Id"] = System.Net.WebUtility.UrlDecode(id),
                ["Email"] = "admin@example.com",
                ["Role"] = "Staff",
                ["IsLockedOut"] = "false"
            }));
            string body = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("cannot remove your own administrator role", body);
        }

        private static MultipartFormDataContent Upload(string token, string kind, string csv, string action)
        {
            var file = new ByteArrayContent(Encoding.UTF8.GetBytes(csv));
            file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/csv");

            return new MultipartFormDataContent
            {
                { new StringContent(token), "__RequestVerificationToken" },
                { new StringContent(kind), "Kind" },
                { new StringContent(action), "action" },
                { file, "File", "upload.csv" }
            };
        }

        private static FormUrlEncodedContent Form(string token, Dictionary<string, string> fields)
        {
            fields["__RequestVerificationToken"] = token;
            return new FormUrlEncodedContent(fields);
        }
    }
}
