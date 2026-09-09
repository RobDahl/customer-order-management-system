using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Coms.DbMigrator;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Coms.Web.Tests
{
    /// <summary>
    /// Hosts the web application in-process against its own database
    /// (Coms_WebTest), rebuilt from the migrations and seed once per run.
    /// The application's own start-up then applies the Identity migrations
    /// and creates the seed users from appsettings.json.
    /// </summary>
    public sealed class WebFixture : WebApplicationFactory<Program>
    {
        private const string LocalDbConnection =
            "Server=(localdb)\\MSSQLLocalDB;Database=Coms_WebTest;Integrated Security=true;TrustServerCertificate=true";

        public WebFixture()
        {
            string? configured = Environment.GetEnvironmentVariable("COMS_WEBTEST_CONNECTION");
            ConnectionString = string.IsNullOrWhiteSpace(configured) ? LocalDbConnection : configured!;
            IsAvailable = !string.IsNullOrWhiteSpace(configured) || OperatingSystem.IsWindows();

            if (IsAvailable)
            {
                MigrationRunner.Rebuild(ConnectionString, seed: true);
            }
        }

        public bool IsAvailable { get; }

        public string ConnectionString { get; }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("ConnectionStrings:Coms", ConnectionString);
        }

        /// <summary>A client that keeps cookies and does not follow redirects, so status codes can be asserted.</summary>
        public HttpClient NewClient()
        {
            return CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        }

        /// <summary>A client already signed in as one of the seed users.</summary>
        public async Task<HttpClient> SignedInAsync(string userName, string password)
        {
            HttpClient client = NewClient();

            HttpResponseMessage page = await client.GetAsync("/Account/Login");
            string token = await AntiForgeryTokenAsync(page);

            var form = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["UserName"] = userName,
                ["Password"] = password,
                ["__RequestVerificationToken"] = token
            });

            HttpResponseMessage response = await client.PostAsync("/Account/Login", form);
            if (response.StatusCode != HttpStatusCode.Redirect)
            {
                throw new InvalidOperationException("Sign in as " + userName + " failed with " + (int)response.StatusCode + ": " + await response.Content.ReadAsStringAsync());
            }

            return client;
        }

        /// <summary>Runs a scalar query against the test database, outside the application.</summary>
        public async Task<T> ScalarAsync<T>(string sql, object? parameters = null)
        {
            using (var connection = new Microsoft.Data.SqlClient.SqlConnection(ConnectionString))
            {
                await connection.OpenAsync();
                return (await Dapper.SqlMapper.ExecuteScalarAsync<T>(connection, sql, parameters))!;
            }
        }

        public static async Task<string> AntiForgeryTokenAsync(HttpResponseMessage page)
        {
            string html = await page.Content.ReadAsStringAsync();
            Match match = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
            if (!match.Success)
            {
                throw new InvalidOperationException("No anti-forgery token found in the page.");
            }

            return match.Groups[1].Value;
        }
    }

    [CollectionDefinition(Name)]
    public sealed class WebCollection : ICollectionFixture<WebFixture>
    {
        public const string Name = "Web";
    }
}
