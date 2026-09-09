using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Coms.Application.Csv;
using Coms.Application.Tests.Fakes;
using Coms.Domain.Customers;
using Coms.Domain.Orders;
using Coms.Domain.Products;
using Coms.Domain.Reports;
using Xunit;

namespace Coms.Application.Tests.Csv
{
    public class CsvExportServiceTests
    {
        private readonly TestWorld _world = new TestWorld();

        [Fact]
        public async Task WriteCustomers_WritesHeaderAndOneRowPerCustomer()
        {
            var writer = new StringWriter();

            int count = await _world.CsvExport.WriteCustomersAsync(writer, new CustomerFilter());
            string[] lines = Lines(writer);

            Assert.Equal(2, count);
            Assert.Equal(string.Join(",", CustomerCsvRow.Headers), lines[0]);
            Assert.Equal(3, lines.Length);
            Assert.Contains(lines, l => l.StartsWith("CUST-000001,Acme Ltd,") && l.Contains("Dock 4") && l.Contains(",30,1000.00,Active,"));
        }

        [Fact]
        public async Task WriteProducts_ThenImport_RoundTrips()
        {
            var writer = new StringWriter();
            await _world.CsvExport.WriteProductsAsync(writer, new ProductFilter());

            var target = new TestWorld();
            target.Products.Products.Clear();
            CsvImportResult result = await target.CsvImport.ImportProductsAsync(new StringReader(writer.ToString()), new CsvImportOptions());

            Assert.False(result.HasErrors, string.Join("; ", result.Errors));
            Assert.Equal(3, result.ToInsert);
            Product bolt = target.Products.Products.Single(p => p.Sku == "BOLT-1");
            Assert.Equal(10m, bolt.UnitPrice);
            Assert.Equal(4m, bolt.CostPrice);
            Assert.Equal(100, bolt.QuantityOnHand);
            Assert.False(target.Products.Products.Single(p => p.Sku == "OLD-1").IsActive);
        }

        [Fact]
        public async Task WriteOrderLines_RepeatsHeaderFieldsPerLine()
        {
            Order order = _world.SeedOrder(OrderStatus.Approved);
            var writer = new StringWriter();

            int count = await _world.CsvExport.WriteOrderLinesAsync(writer, order.Id);
            string[] lines = Lines(writer);

            Assert.Equal(2, count);
            Assert.Equal("OrderNumber,Status,OrderDate,CustomerNumber,CustomerName,LineNumber,Sku,Description,Quantity,UnitPrice,DiscountPercent,LineTotal", lines[0]);
            Assert.Equal(order.OrderNumber + ",Approved,2026-09-01,CUST-000001,Acme Ltd,1,BOLT-1,Bolt,2,10.00,0,20.00", lines[1]);
            Assert.Equal(order.OrderNumber + ",Approved,2026-09-01,CUST-000001,Acme Ltd,2,NUT-1,Nut,4,2.50,0,10.00", lines[2]);
        }

        [Fact]
        public async Task WriteOrderLines_MissingOrder_WritesNothing()
        {
            var writer = new StringWriter();

            int count = await _world.CsvExport.WriteOrderLinesAsync(writer, 404);

            Assert.Equal(0, count);
            Assert.Equal(string.Empty, writer.ToString());
        }

        [Fact]
        public async Task WriteRecords_UsesPropertyNamesAsHeaders()
        {
            var rows = new[]
            {
                new SalesByMonthRow { MonthStart = new DateTime(2026, 1, 1), MonthLabel = "2026-01", OrderCount = 3, Total = 99.5m }
            };
            var writer = new StringWriter();

            int count = await _world.CsvExport.WriteRecordsAsync(writer, rows);
            string[] lines = Lines(writer);

            Assert.Equal(1, count);
            Assert.Equal("MonthStart,MonthLabel,OrderCount,Subtotal,TaxAmount,Total,AverageOrderValue", lines[0]);
            Assert.Contains("2026-01,3,", lines[1]);
        }

        [Fact]
        public async Task WriteCustomers_QuotesFieldsContainingCommas()
        {
            _world.ActiveCustomer.Name = "Acme, Ltd";
            var writer = new StringWriter();

            await _world.CsvExport.WriteCustomersAsync(writer, new CustomerFilter());

            Assert.Contains("\"Acme, Ltd\"", writer.ToString());
        }

        private static string[] Lines(StringWriter writer)
        {
            return writer.ToString().Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
        }
    }
}
