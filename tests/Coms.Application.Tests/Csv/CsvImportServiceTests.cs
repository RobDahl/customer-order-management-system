using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Coms.Application.Csv;
using Coms.Application.Tests.Fakes;
using Coms.Domain.Customers;
using Coms.Domain.Products;
using Xunit;

namespace Coms.Application.Tests.Csv
{
    public class CsvImportServiceTests
    {
        private readonly TestWorld _world = new TestWorld();

        [Fact]
        public async Task ImportCustomers_ValidFile_InsertsAllInOneTransaction()
        {
            const string file =
                "Name,BillingLine1,BillingCity,BillingCountry,Email,PaymentTermsDays,CreditLimit,Status\r\n" +
                "Alpha Co,1 A St,Town,UK,a@alpha.example,14,2500.00,Active\r\n" +
                "Beta Co,2 B St,Town,UK,,,,OnHold\r\n";

            CsvImportResult result = await _world.CsvImport.ImportCustomersAsync(new StringReader(file), new CsvImportOptions());

            Assert.False(result.HasErrors, string.Join("; ", result.Errors));
            Assert.True(result.Committed);
            Assert.Equal(2, result.TotalRows);
            Assert.Equal(2, result.ToInsert);
            Assert.Equal(0, result.ToUpdate);
            Assert.Equal(1, _world.UnitOfWork.Begins);
            Assert.Equal(1, _world.UnitOfWork.Commits);

            Customer alpha = _world.Customers.Customers.Single(c => c.Name == "Alpha Co");
            Assert.Equal(14, alpha.PaymentTermsDays);
            Assert.Equal(2500m, alpha.CreditLimit);
            Assert.Null(alpha.ShippingAddress);

            Customer beta = _world.Customers.Customers.Single(c => c.Name == "Beta Co");
            Assert.Equal(30, beta.PaymentTermsDays);
            Assert.Null(beta.CreditLimit);
            Assert.Equal(CustomerStatus.OnHold, beta.Status);
        }

        [Fact]
        public async Task ImportCustomers_AnyBadRow_ImportsNothing()
        {
            const string file =
                "Name,BillingLine1,BillingCity,BillingCountry,PaymentTermsDays,Status\r\n" +
                "Good Co,1 A St,Town,UK,30,Active\r\n" +
                "Bad Co,,Town,UK,abc,Sleeping\r\n";

            CsvImportResult result = await _world.CsvImport.ImportCustomersAsync(new StringReader(file), new CsvImportOptions());

            Assert.True(result.HasErrors);
            Assert.False(result.Committed);
            Assert.Equal(2, _world.Customers.Customers.Count);
            Assert.Equal(0, _world.UnitOfWork.Begins);
            Assert.Contains(result.Errors, e => e.RowNumber == 3 && e.Field == "BillingLine1");
            Assert.Contains(result.Errors, e => e.RowNumber == 3 && e.Field == "PaymentTermsDays" && e.Message.Contains("abc"));
            Assert.Contains(result.Errors, e => e.RowNumber == 3 && e.Field == "Status" && e.Message.Contains("Active, OnHold, Inactive"));
            Assert.Contains("Nothing was imported", result.Summary);
        }

        [Fact]
        public async Task ImportCustomers_MissingRequiredColumn_IsFileError()
        {
            const string file = "Name,BillingCity\r\nX,Y\r\n";

            CsvImportResult result = await _world.CsvImport.ImportCustomersAsync(new StringReader(file), new CsvImportOptions());

            Assert.Single(result.Errors);
            Assert.Equal(0, result.Errors[0].RowNumber);
            Assert.Contains("BillingLine1", result.Errors[0].Message);
            Assert.Contains("BillingCountry", result.Errors[0].Message);
        }

        [Fact]
        public async Task ImportCustomers_EmptyFile_IsFileError()
        {
            CsvImportResult result = await _world.CsvImport.ImportCustomersAsync(new StringReader(""), new CsvImportOptions());

            Assert.Single(result.Errors);
            Assert.Contains("empty", result.Errors[0].Message);
        }

        [Fact]
        public async Task ImportCustomers_ExistingNumberWithoutUpdateFlag_IsRejected()
        {
            string file =
                "CustomerNumber,Name,BillingLine1,BillingCity,BillingCountry\r\n" +
                _world.ActiveCustomer.CustomerNumber + ",Acme Renamed,1 High St,Town,UK\r\n";

            CsvImportResult result = await _world.CsvImport.ImportCustomersAsync(new StringReader(file), new CsvImportOptions());

            Assert.Contains(result.Errors, e => e.Field == "CustomerNumber" && e.Message.Contains("already exists"));
            Assert.Equal("Acme Ltd", _world.ActiveCustomer.Name);
        }

        [Fact]
        public async Task ImportCustomers_UpdateExisting_OverwritesMatchedRows()
        {
            string file =
                "CustomerNumber,Name,BillingLine1,BillingCity,BillingCountry,ContactName\r\n" +
                _world.ActiveCustomer.CustomerNumber + ",Acme Renamed,1 High St,Town,UK,Pat\r\n" +
                ",Brand New,9 New Rd,Ville,FR,\r\n";

            CsvImportResult result = await _world.CsvImport.ImportCustomersAsync(new StringReader(file), new CsvImportOptions { UpdateExisting = true });

            Assert.False(result.HasErrors, string.Join("; ", result.Errors));
            Assert.Equal(1, result.ToUpdate);
            Assert.Equal(1, result.ToInsert);
            Customer acme = (await _world.Customers.GetByNumberAsync(_world.ActiveCustomer.CustomerNumber))!;
            Assert.Equal("Acme Renamed", acme.Name);
            Assert.Equal("Pat", acme.ContactName);
            Assert.Equal(30, acme.PaymentTermsDays);           // blank cell keeps the existing value
            Assert.Equal(3, _world.Customers.Customers.Count);
        }

        [Fact]
        public async Task ImportCustomers_UnknownNumber_IsRejected()
        {
            const string file = "CustomerNumber,Name,BillingLine1,BillingCity,BillingCountry\r\nCUST-999999,X,1 St,Town,UK\r\n";

            CsvImportResult result = await _world.CsvImport.ImportCustomersAsync(new StringReader(file), new CsvImportOptions { UpdateExisting = true });

            Assert.Contains(result.Errors, e => e.Message.Contains("does not exist"));
        }

        [Fact]
        public async Task ImportCustomers_DryRun_CountsWithoutWriting()
        {
            const string file = "Name,BillingLine1,BillingCity,BillingCountry\r\nDry Co,1 St,Town,UK\r\n";

            CsvImportResult result = await _world.CsvImport.ImportCustomersAsync(new StringReader(file), new CsvImportOptions { DryRun = true });

            Assert.False(result.HasErrors);
            Assert.False(result.Committed);
            Assert.Equal(1, result.ToInsert);
            Assert.Equal(2, _world.Customers.Customers.Count);
            Assert.StartsWith("Would import", result.Summary);
        }

        [Fact]
        public async Task ImportCustomers_HeadersMatchCaseInsensitively()
        {
            const string file = "name,billingline1,BILLINGCITY,BillingCountry\r\nCase Co,1 St,Town,UK\r\n";

            CsvImportResult result = await _world.CsvImport.ImportCustomersAsync(new StringReader(file), new CsvImportOptions());

            Assert.False(result.HasErrors, string.Join("; ", result.Errors));
            Assert.True(result.Committed);
        }

        [Fact]
        public async Task ImportCustomers_TooManyRows_IsRejected()
        {
            _world.Options.MaxImportRows = 1;
            const string file = "Name,BillingLine1,BillingCity,BillingCountry\r\nA,1,T,UK\r\nB,2,T,UK\r\n";

            CsvImportResult result = await _world.CsvImport.ImportCustomersAsync(new StringReader(file), new CsvImportOptions());

            Assert.Contains(result.Errors, e => e.RowNumber == 0 && e.Message.Contains("more than 1 rows"));
        }

        [Fact]
        public async Task ImportProducts_InsertsAndUpdatesBySku()
        {
            const string file =
                "Sku,Name,Category,UnitPrice,CostPrice,QuantityOnHand,ReorderLevel,IsActive\r\n" +
                "bolt-1,Bolt (renamed),Fasteners,11.00,4.50,,,yes\r\n" +
                "NEW-1,Brand new,Misc,3.25,1,50,5,no\r\n";

            CsvImportResult result = await _world.CsvImport.ImportProductsAsync(new StringReader(file), new CsvImportOptions { UpdateExisting = true });

            Assert.False(result.HasErrors, string.Join("; ", result.Errors));
            Assert.Equal(1, result.ToUpdate);
            Assert.Equal(1, result.ToInsert);
            Product bolt = (await _world.Products.GetBySkuAsync("BOLT-1"))!;
            Assert.Equal("Bolt (renamed)", bolt.Name);
            Assert.Equal(11m, bolt.UnitPrice);
            Assert.Equal(100, bolt.QuantityOnHand);          // blank cell keeps the existing value

            Product created = _world.Products.Products.Single(p => p.Sku == "NEW-1");
            Assert.False(created.IsActive);
            Assert.Equal(50, created.QuantityOnHand);
        }

        [Fact]
        public async Task ImportProducts_DuplicateSkuInFile_IsRejected()
        {
            const string file = "Sku,Name,Category,UnitPrice\r\nDUP-1,A,Misc,1\r\ndup-1,B,Misc,2\r\n";

            CsvImportResult result = await _world.CsvImport.ImportProductsAsync(new StringReader(file), new CsvImportOptions());

            Assert.Contains(result.Errors, e => e.RowNumber == 3 && e.Message.Contains("more than once"));
            Assert.Equal(3, _world.Products.Products.Count);
        }

        [Fact]
        public async Task ImportProducts_BadValues_ReportEachField()
        {
            const string file = "Sku,Name,Category,UnitPrice,IsActive\r\nBAD 1,,Misc,ten,maybe\r\n";

            CsvImportResult result = await _world.CsvImport.ImportProductsAsync(new StringReader(file), new CsvImportOptions());

            Assert.Contains(result.Errors, e => e.Field == "Sku");
            Assert.Contains(result.Errors, e => e.Field == "Name");
            Assert.Contains(result.Errors, e => e.Field == "UnitPrice" && e.Message.Contains("ten"));
            Assert.Contains(result.Errors, e => e.Field == "IsActive" && e.Message.Contains("maybe"));
        }

        [Fact]
        public void Templates_ListEveryColumn()
        {
            Assert.StartsWith("CustomerNumber,Name,", _world.CsvImport.CustomerTemplate);
            Assert.EndsWith(",Status,Notes", _world.CsvImport.CustomerTemplate);
            Assert.Equal("Sku,Name,Description,Category,UnitOfMeasure,UnitPrice,CostPrice,QuantityOnHand,ReorderLevel,IsActive", _world.CsvImport.ProductTemplate);
        }
    }
}
