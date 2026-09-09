using System.Threading.Tasks;
using Xunit;

namespace Coms.Data.Tests
{
    [Collection(DatabaseCollection.Name)]
    public class SchemaTests
    {
        private readonly DatabaseFixture _db;

        public SchemaTests(DatabaseFixture db)
        {
            _db = db;
        }

        [Fact]
        public async Task Migrations_AllRecorded()
        {
            if (!_db.IsAvailable) { return; }

            int applied = await _db.ScalarAsync<int>("SELECT COUNT(*) FROM dbo.SchemaVersions");

            Assert.Equal(11, applied);
        }

        [Theory]
        [InlineData("dbo.Customers", "U")]
        [InlineData("dbo.Products", "U")]
        [InlineData("dbo.Orders", "U")]
        [InlineData("dbo.OrderLines", "U")]
        [InlineData("dbo.OrderStatusHistory", "U")]
        [InlineData("dbo.Invoices", "U")]
        [InlineData("dbo.Payments", "U")]
        [InlineData("dbo.Notes", "U")]
        [InlineData("dbo.NumberSeries", "U")]
        [InlineData("dbo.usp_NextCustomerNumber", "P")]
        [InlineData("dbo.usp_NextOrderNumber", "P")]
        [InlineData("dbo.usp_NextInvoiceNumber", "P")]
        [InlineData("dbo.usp_Order_Fulfil", "P")]
        [InlineData("dbo.usp_Invoice_CreateFromOrder", "P")]
        [InlineData("dbo.usp_Invoice_ApplyPayment", "P")]
        [InlineData("dbo.usp_Invoice_Void", "P")]
        [InlineData("rpt.vw_OrderSummary", "V")]
        [InlineData("rpt.vw_CustomerBalance", "V")]
        [InlineData("rpt.vw_LowStock", "V")]
        [InlineData("rpt.vw_InvoiceAging", "V")]
        [InlineData("rpt.usp_SalesByMonth", "P")]
        [InlineData("rpt.usp_TopCustomers", "P")]
        [InlineData("rpt.usp_ArAging", "P")]
        [InlineData("rpt.usp_ProductSales", "P")]
        [InlineData("rpt.usp_DashboardCounts", "P")]
        public async Task Object_Exists(string name, string type)
        {
            if (!_db.IsAvailable) { return; }

            int count = await _db.ScalarAsync<int>(
                "SELECT COUNT(*) FROM sys.objects WHERE object_id = OBJECT_ID(@Name) AND type = @Type",
                new { Name = name, Type = type });

            Assert.Equal(1, count);
        }

        [Fact]
        public async Task Seed_ProducesExpectedRowCounts()
        {
            if (!_db.IsAvailable) { return; }

            Assert.Equal(200, await _db.ScalarAsync<int>("SELECT COUNT(*) FROM dbo.Customers"));
            Assert.Equal(150, await _db.ScalarAsync<int>("SELECT COUNT(*) FROM dbo.Products"));
            Assert.Equal(5000, await _db.ScalarAsync<int>("SELECT COUNT(*) FROM dbo.Orders"));
            Assert.Equal(0, await _db.ScalarAsync<int>("SELECT COUNT(*) FROM dbo.Orders o WHERE NOT EXISTS (SELECT 1 FROM dbo.OrderLines l WHERE l.OrderId = o.Id)"));
            Assert.Equal(0, await _db.ScalarAsync<int>("SELECT COUNT(*) FROM dbo.Invoices WHERE Total <> Subtotal + TaxAmount"));
        }
    }
}
