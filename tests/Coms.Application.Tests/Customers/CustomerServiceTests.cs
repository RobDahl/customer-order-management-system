using System.Threading.Tasks;
using Coms.Application.Tests.Fakes;
using Coms.Domain.Common;
using Coms.Domain.Customers;
using Xunit;

namespace Coms.Application.Tests.Customers
{
    public class CustomerServiceTests
    {
        private readonly TestWorld _world = new TestWorld();

        [Fact]
        public async Task Create_ValidCustomer_AssignsNumberAndAudit()
        {
            var customer = new Customer
            {
                Name = "  New Co  ",
                BillingAddress = new Address { Line1 = "1 St", City = "City", Country = "UK" }
            };

            Result<Customer> result = await _world.CustomerService.CreateAsync(customer);

            Assert.True(result.IsSuccess, result.Message);
            Assert.Equal("New Co", result.Value.Name);
            Assert.StartsWith("CUST-", result.Value.CustomerNumber);
            Assert.Equal("tester", result.Value.CreatedBy);
            Assert.Equal(3, _world.Customers.Customers.Count);
        }

        [Fact]
        public async Task Create_InvalidCustomer_ReturnsValidationErrorsAndSavesNothing()
        {
            var customer = new Customer { Name = "", Email = "nope" };

            Result<Customer> result = await _world.CustomerService.CreateAsync(customer);

            Assert.Equal(ErrorCode.Validation, result.Code);
            Assert.Contains(result.Errors, e => e.Field == "Name");
            Assert.Contains(result.Errors, e => e.Field == "Email");
            Assert.Contains(result.Errors, e => e.Field == "BillingAddress.Line1");
            Assert.Equal(2, _world.Customers.Customers.Count);
        }

        [Fact]
        public async Task Update_UnsavedCustomer_IsRejected()
        {
            Result<Customer> result = await _world.CustomerService.UpdateAsync(new Customer { Name = "x" });

            Assert.Equal(ErrorCode.Validation, result.Code);
            Assert.Contains(result.Errors, e => e.Field == "Id");
        }

        [Fact]
        public async Task Update_StaleRowVersion_ReturnsConflict()
        {
            // A second client that loaded the customer earlier and holds an old rowversion.
            var stale = new Customer
            {
                Id = _world.ActiveCustomer.Id,
                Name = "Acme Ltd",
                BillingAddress = _world.ActiveCustomer.BillingAddress.Copy(),
                RowVersion = new byte[] { 9, 9, 9, 9, 9, 9, 9, 9 },
                Phone = "123"
            };

            Result<Customer> result = await _world.CustomerService.UpdateAsync(stale);

            Assert.Equal(ErrorCode.Conflict, result.Code);
            Assert.Null(_world.ActiveCustomer.Phone);
        }

        [Fact]
        public async Task Update_Valid_BumpsRowVersion()
        {
            Customer customer = _world.ActiveCustomer;
            byte[] before = (byte[])customer.RowVersion.Clone();
            customer.ContactName = "Sam";

            Result<Customer> result = await _world.CustomerService.UpdateAsync(customer);

            Assert.True(result.IsSuccess);
            Assert.NotEqual(before, customer.RowVersion);
            Assert.Equal("tester", customer.UpdatedBy);
        }

        [Fact]
        public async Task ChangeStatus_ToOnHold_Persists()
        {
            Result<Customer> result = await _world.CustomerService.ChangeStatusAsync(_world.ActiveCustomer.Id, CustomerStatus.OnHold, _world.ActiveCustomer.RowVersion);

            Assert.True(result.IsSuccess);
            Assert.Equal(CustomerStatus.OnHold, _world.ActiveCustomer.Status);
            Assert.False(_world.ActiveCustomer.CanOrder);
        }

        [Fact]
        public async Task ChangeStatus_MissingCustomer_ReturnsNotFound()
        {
            Result<Customer> result = await _world.CustomerService.ChangeStatusAsync(999, CustomerStatus.Inactive, new byte[0]);

            Assert.Equal(ErrorCode.NotFound, result.Code);
        }
    }
}
