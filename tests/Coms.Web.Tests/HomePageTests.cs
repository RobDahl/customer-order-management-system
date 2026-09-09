using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Coms.Web.Tests
{
    public class HomePageTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly WebApplicationFactory<Program> _factory;

        public HomePageTests(WebApplicationFactory<Program> factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task Index_ReturnsOk_WithDashboardTitle()
        {
            HttpClient client = _factory.CreateClient();

            HttpResponseMessage response = await client.GetAsync("/");
            string body = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("<title>Dashboard - Order Management</title>", body);
        }

        [Fact]
        public async Task Stylesheet_IsServed()
        {
            HttpClient client = _factory.CreateClient();

            HttpResponseMessage response = await client.GetAsync("/css/site.css");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }
}
