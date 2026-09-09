using System;
using System.Linq;
using System.Threading.Tasks;
using Coms.Data.Connections;
using Coms.Data.Repositories;
using Coms.Domain.Common;
using Coms.Domain.Customers;
using Xunit;

namespace Coms.Data.Tests.Repositories
{
    [Collection(DatabaseCollection.Name)]
    public class CustomerRepositoryTests
    {
        private readonly DatabaseFixture _db;

        public CustomerRepositoryTests(DatabaseFixture db)
        {
            _db = db;
        }

        [Fact]
        public async Task GetById_SeededCustomer_LoadsBillingAddress()
        {
            if (!_db.IsAvailable) { return; }

            using (DbSession session = _db.OpenSession())
            {
                Customer? customer = await new CustomerRepository(session).GetByIdAsync(1);

                Assert.NotNull(customer);
                Assert.Equal("CUST-000001", customer!.CustomerNumber);
                Assert.Equal("Northgate Supply Co.", customer.Name);
                Assert.Equal(CustomerStatus.Active, customer.Status);
                Assert.False(string.IsNullOrEmpty(customer.BillingAddress.Line1));
                Assert.False(string.IsNullOrEmpty(customer.BillingAddress.City));
                Assert.Equal(8, customer.RowVersion.Length);
            }
        }

        [Fact]
        public async Task GetById_CustomerWithoutShippingAddress_HasNullShipping()
        {
            if (!_db.IsAvailable) { return; }

            int id = await _db.ScalarAsync<int>("SELECT MIN(Id) FROM dbo.Customers WHERE ShippingLine1 IS NULL");

            using (DbSession session = _db.OpenSession())
            {
                Customer? customer = await new CustomerRepository(session).GetByIdAsync(id);

                Assert.NotNull(customer);
                Assert.Null(customer!.ShippingAddress);
                Assert.Same(customer.BillingAddress, customer.EffectiveShippingAddress);
            }
        }

        [Fact]
        public async Task GetById_CustomerWithShippingAddress_LoadsIt()
        {
            if (!_db.IsAvailable) { return; }

            int id = await _db.ScalarAsync<int>("SELECT MIN(Id) FROM dbo.Customers WHERE ShippingLine1 IS NOT NULL");

            using (DbSession session = _db.OpenSession())
            {
                Customer? customer = await new CustomerRepository(session).GetByIdAsync(id);

                Assert.NotNull(customer!.ShippingAddress);
                Assert.False(string.IsNullOrEmpty(customer.ShippingAddress!.Line1));
            }
        }

        [Fact]
        public async Task GetById_Missing_ReturnsNull()
        {
            if (!_db.IsAvailable) { return; }

            using (DbSession session = _db.OpenSession())
            {
                Assert.Null(await new CustomerRepository(session).GetByIdAsync(999999));
            }
        }

        [Fact]
        public async Task GetByNumber_FindsCustomer()
        {
            if (!_db.IsAvailable) { return; }

            using (DbSession session = _db.OpenSession())
            {
                Customer? customer = await new CustomerRepository(session).GetByNumberAsync("CUST-000002");

                Assert.NotNull(customer);
                Assert.Equal(2, customer!.Id);
            }
        }

        [Fact]
        public async Task Search_ByNamePrefix_PagesAndCounts()
        {
            if (!_db.IsAvailable) { return; }

            using (DbSession session = _db.OpenSession())
            {
                var repository = new CustomerRepository(session);
                var filter = new CustomerFilter { Search = "Northgate" };
                var paging = new PagedRequest { Page = 1, PageSize = 3, SortBy = "name" };

                PagedResult<Customer> page = await repository.SearchAsync(filter, paging);

                Assert.Equal(10, page.TotalCount);
                Assert.Equal(3, page.Items.Count);
                Assert.Equal(4, page.TotalPages);
                Assert.All(page.Items, c => Assert.StartsWith("Northgate", c.Name));
                Assert.True(page.HasNext);
                Assert.False(page.HasPrevious);
            }
        }

        [Fact]
        public async Task Search_SortByNumberDescending_OrdersCorrectly()
        {
            if (!_db.IsAvailable) { return; }

            using (DbSession session = _db.OpenSession())
            {
                var paging = new PagedRequest { PageSize = 5, SortBy = "number", SortDescending = true };

                PagedResult<Customer> page = await new CustomerRepository(session).SearchAsync(new CustomerFilter(), paging);

                Assert.Equal("CUST-000200", page.Items[0].CustomerNumber);
                Assert.Equal(200, page.TotalCount);
            }
        }

        [Fact]
        public async Task Search_UnknownSortKey_FallsBackToDefault()
        {
            if (!_db.IsAvailable) { return; }

            using (DbSession session = _db.OpenSession())
            {
                var paging = new PagedRequest { PageSize = 2, SortBy = "Name; DROP TABLE dbo.Customers" };

                PagedResult<Customer> page = await new CustomerRepository(session).SearchAsync(new CustomerFilter(), paging);

                Assert.Equal(2, page.Items.Count);
                Assert.True(string.CompareOrdinal(page.Items[0].Name, page.Items[1].Name) <= 0);
            }
        }

        [Fact]
        public async Task Search_ByStatus_FiltersRows()
        {
            if (!_db.IsAvailable) { return; }

            using (DbSession session = _db.OpenSession())
            {
                var filter = new CustomerFilter { Status = CustomerStatus.OnHold };

                PagedResult<Customer> page = await new CustomerRepository(session).SearchAsync(filter, PagedRequest.FirstPage());

                Assert.True(page.TotalCount > 0);
                Assert.All(page.Items, c => Assert.Equal(CustomerStatus.OnHold, c.Status));
            }
        }

        [Fact]
        public async Task Insert_AssignsNumberAuditAndRowVersion()
        {
            if (!_db.IsAvailable) { return; }

            using (DbSession session = await _db.BeginSessionAsync())
            {
                var repository = new CustomerRepository(session);
                Customer customer = NewCustomer();

                await repository.InsertAsync(customer, "tester");

                Assert.True(customer.Id > 200);
                Assert.Matches("^CUST-[0-9]{6}$", customer.CustomerNumber);
                Assert.Equal("tester", customer.CreatedBy);
                Assert.Equal(8, customer.RowVersion.Length);

                Customer? reloaded = await repository.GetByIdAsync(customer.Id);
                Assert.NotNull(reloaded);
                Assert.Equal(customer.Name, reloaded!.Name);
                Assert.Equal("Springfield", reloaded.BillingAddress.City);
                Assert.NotNull(reloaded.ShippingAddress);
                Assert.Equal("Depot", reloaded.ShippingAddress!.City);
            }
        }

        [Fact]
        public async Task Update_WithCurrentRowVersion_SucceedsAndBumpsVersion()
        {
            if (!_db.IsAvailable) { return; }

            using (DbSession session = await _db.BeginSessionAsync())
            {
                var repository = new CustomerRepository(session);
                Customer customer = (await repository.GetByIdAsync(1))!;
                byte[] before = customer.RowVersion.ToArray();

                customer.ContactName = "Changed Contact";
                bool updated = await repository.UpdateAsync(customer, "tester");

                Assert.True(updated);
                Assert.False(before.SequenceEqual(customer.RowVersion));
                Assert.Equal("tester", customer.UpdatedBy);

                Customer? reloaded = await repository.GetByIdAsync(1);
                Assert.Equal("Changed Contact", reloaded!.ContactName);
            }
        }

        [Fact]
        public async Task Update_WithStaleRowVersion_ReturnsFalse()
        {
            if (!_db.IsAvailable) { return; }

            using (DbSession session = await _db.BeginSessionAsync())
            {
                var repository = new CustomerRepository(session);
                Customer first = (await repository.GetByIdAsync(1))!;
                Customer second = (await repository.GetByIdAsync(1))!;

                first.Phone = "111";
                Assert.True(await repository.UpdateAsync(first, "user-a"));

                second.Phone = "222";
                bool updated = await repository.UpdateAsync(second, "user-b");

                Assert.False(updated);
                Assert.Equal("111", (await repository.GetByIdAsync(1))!.Phone);
            }
        }

        [Fact]
        public async Task GetBalance_TopCustomer_HasOutstandingAndExposure()
        {
            if (!_db.IsAvailable) { return; }

            using (DbSession session = _db.OpenSession())
            {
                CustomerBalance? balance = await new CustomerRepository(session).GetBalanceAsync(1);

                Assert.NotNull(balance);
                Assert.Equal("CUST-000001", balance!.CustomerNumber);
                Assert.True(balance.OutstandingBalance > 0);
                Assert.Equal(balance.OutstandingBalance + balance.CommittedTotal, balance.Exposure);
            }
        }

        private static Customer NewCustomer()
        {
            return new Customer
            {
                Name = "Test Customer " + Guid.NewGuid().ToString("N").Substring(0, 8),
                ContactName = "Pat Tester",
                Email = "pat@example.com",
                BillingAddress = new Address { Line1 = "1 Test St", City = "Springfield", Region = "IL", PostalCode = "62701", Country = "USA" },
                ShippingAddress = new Address { Line1 = "9 Dock Rd", City = "Depot", Country = "USA" },
                PaymentTermsDays = 30,
                CreditLimit = 5000m,
                Status = CustomerStatus.Active
            };
        }
    }
}
