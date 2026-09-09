using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;

namespace Coms.Web.Tests
{
    [Collection(WebCollection.Name)]
    public class HomePageTests
    {
        private readonly WebFixture _web;

        public HomePageTests(WebFixture web)
        {
            _web = web;
        }

        [Fact]
        public async Task Anonymous_Dashboard_RedirectsToLogin()
        {
            if (!_web.IsAvailable) { return; }

            HttpResponseMessage response = await _web.NewClient().GetAsync("/");

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Contains("/Account/Login", response.Headers.Location!.ToString());
        }

        [Fact]
        public async Task Stylesheet_IsServedWithoutSignIn()
        {
            if (!_web.IsAvailable) { return; }

            HttpResponseMessage response = await _web.NewClient().GetAsync("/css/site.css");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task SignedIn_Dashboard_ShowsUserAndRole()
        {
            if (!_web.IsAvailable) { return; }

            HttpClient client = await _web.SignedInAsync("viewer", "Viewer#2026");

            HttpResponseMessage response = await client.GetAsync("/");
            string body = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("<title>Dashboard - Order Management</title>", body);
            Assert.Contains("viewer (Read only)", body);
            Assert.Contains("Database: Coms_WebTest", body);
        }

        [Fact]
        public async Task Login_WrongPassword_StaysOnPageWithMessage()
        {
            if (!_web.IsAvailable) { return; }

            HttpClient client = _web.NewClient();
            string token = await WebFixture.AntiForgeryTokenAsync(await client.GetAsync("/Account/Login"));

            HttpResponseMessage response = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new System.Collections.Generic.Dictionary<string, string>
            {
                ["UserName"] = "viewer",
                ["Password"] = "wrong",
                ["__RequestVerificationToken"] = token
            }));
            string body = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("Invalid user name or password", body);
        }

        [Fact]
        public async Task Logout_ThenDashboard_RedirectsToLogin()
        {
            if (!_web.IsAvailable) { return; }

            HttpClient client = await _web.SignedInAsync("staff", "Staff#2026");
            string token = await WebFixture.AntiForgeryTokenAsync(await client.GetAsync("/"));

            HttpResponseMessage logout = await client.PostAsync("/Account/Logout", new FormUrlEncodedContent(new System.Collections.Generic.Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token
            }));
            HttpResponseMessage after = await client.GetAsync("/");

            Assert.Equal(HttpStatusCode.Redirect, logout.StatusCode);
            Assert.Equal(HttpStatusCode.Redirect, after.StatusCode);
            Assert.Contains("/Account/Login", after.Headers.Location!.ToString());
        }
    }
}
