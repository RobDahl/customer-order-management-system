using System.Collections.Generic;
using Coms.Domain.Common;
using Coms.Domain.Customers;
using Xunit;

namespace Coms.Domain.Tests.Customers
{
    public class CustomerValidationTests
    {
        [Fact]
        public void Validate_ValidCustomer_HasNoErrors()
        {
            Assert.Empty(ValidCustomer().Validate());
        }

        [Fact]
        public void Validate_MissingName_Fails()
        {
            Customer customer = ValidCustomer();
            customer.Name = "   ";

            Assert.Contains(customer.Validate(), e => e.Field == nameof(Customer.Name));
        }

        [Theory]
        [InlineData("not-an-email")]
        [InlineData("@example.com")]
        [InlineData("a@")]
        [InlineData("a@@b.com")]
        [InlineData("a b@c.com")]
        public void Validate_BadEmail_Fails(string email)
        {
            Customer customer = ValidCustomer();
            customer.Email = email;

            Assert.Contains(customer.Validate(), e => e.Field == nameof(Customer.Email));
        }

        [Fact]
        public void Validate_BillingAddressMissingCity_FailsWithPrefixedField()
        {
            Customer customer = ValidCustomer();
            customer.BillingAddress.City = "";

            Assert.Contains(customer.Validate(), e => e.Field == "BillingAddress.City");
        }

        [Fact]
        public void Validate_PartialShippingAddress_Fails()
        {
            Customer customer = ValidCustomer();
            customer.ShippingAddress = new Address { Line1 = "Dock 4" };

            IReadOnlyList<ValidationError> errors = customer.Validate();

            Assert.Contains(errors, e => e.Field == "ShippingAddress.City");
            Assert.Contains(errors, e => e.Field == "ShippingAddress.Country");
        }

        [Fact]
        public void Normalize_BlankShippingAddress_BecomesNull()
        {
            Customer customer = ValidCustomer();
            customer.ShippingAddress = new Address { Line1 = " ", City = "" };
            customer.ContactName = "  ";
            customer.Name = "  Acme  ";

            customer.Normalize();

            Assert.Null(customer.ShippingAddress);
            Assert.Null(customer.ContactName);
            Assert.Equal("Acme", customer.Name);
            Assert.Empty(customer.Validate());
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(366)]
        public void Validate_PaymentTermsOutOfRange_Fails(int days)
        {
            Customer customer = ValidCustomer();
            customer.PaymentTermsDays = days;

            Assert.Contains(customer.Validate(), e => e.Field == nameof(Customer.PaymentTermsDays));
        }

        [Fact]
        public void Validate_NegativeCreditLimit_Fails()
        {
            Customer customer = ValidCustomer();
            customer.CreditLimit = -1;

            Assert.Contains(customer.Validate(), e => e.Field == nameof(Customer.CreditLimit));
        }

        [Fact]
        public void CanOrder_OnlyWhenActive()
        {
            Assert.True(new Customer { Status = CustomerStatus.Active }.CanOrder);
            Assert.False(new Customer { Status = CustomerStatus.OnHold }.CanOrder);
            Assert.False(new Customer { Status = CustomerStatus.Inactive }.CanOrder);
        }

        private static Customer ValidCustomer()
        {
            return new Customer
            {
                Name = "Acme Ltd",
                Email = "orders@acme.example",
                BillingAddress = new Address { Line1 = "1 High St", City = "Town", Country = "UK" },
                PaymentTermsDays = 30
            };
        }
    }
}
