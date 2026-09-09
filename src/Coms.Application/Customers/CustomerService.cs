using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Coms.Domain.Common;
using Coms.Domain.Customers;

namespace Coms.Application.Customers
{
    public sealed class CustomerService : ICustomerService
    {
        private readonly ICustomerRepository _customers;
        private readonly ICurrentUser _currentUser;

        public CustomerService(ICustomerRepository customers, ICurrentUser currentUser)
        {
            _customers = customers ?? throw new ArgumentNullException(nameof(customers));
            _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
        }

        public Task<Customer?> GetAsync(int id, CancellationToken cancellationToken = default)
        {
            return _customers.GetByIdAsync(id, cancellationToken);
        }

        public Task<Customer?> GetByNumberAsync(string customerNumber, CancellationToken cancellationToken = default)
        {
            return _customers.GetByNumberAsync(customerNumber.Trim(), cancellationToken);
        }

        public Task<PagedResult<Customer>> SearchAsync(CustomerFilter filter, PagedRequest paging, CancellationToken cancellationToken = default)
        {
            return _customers.SearchAsync(filter, paging, cancellationToken);
        }

        public Task<CustomerBalance?> GetBalanceAsync(int id, CancellationToken cancellationToken = default)
        {
            return _customers.GetBalanceAsync(id, cancellationToken);
        }

        public async Task<Result<Customer>> CreateAsync(Customer customer, CancellationToken cancellationToken = default)
        {
            if (customer == null)
            {
                throw new ArgumentNullException(nameof(customer));
            }

            customer.Normalize();
            IReadOnlyList<ValidationError> errors = customer.Validate();
            if (errors.Count > 0)
            {
                return Result<Customer>.Invalid(errors);
            }

            await _customers.InsertAsync(customer, _currentUser.UserName, cancellationToken).ConfigureAwait(false);
            return Result<Customer>.Success(customer);
        }

        public async Task<Result<Customer>> UpdateAsync(Customer customer, CancellationToken cancellationToken = default)
        {
            if (customer == null)
            {
                throw new ArgumentNullException(nameof(customer));
            }

            if (customer.IsNew)
            {
                return Result<Customer>.Invalid(nameof(Customer.Id), "The customer has not been saved yet.");
            }

            customer.Normalize();
            IReadOnlyList<ValidationError> errors = customer.Validate();
            if (errors.Count > 0)
            {
                return Result<Customer>.Invalid(errors);
            }

            bool updated = await _customers.UpdateAsync(customer, _currentUser.UserName, cancellationToken).ConfigureAwait(false);
            return updated ? Result<Customer>.Success(customer) : Result<Customer>.Conflict();
        }

        public async Task<Result<Customer>> ChangeStatusAsync(int id, CustomerStatus status, byte[] rowVersion, CancellationToken cancellationToken = default)
        {
            Customer? customer = await _customers.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
            if (customer == null)
            {
                return Result<Customer>.NotFound("Customer");
            }

            if (!RowVersions.Match(customer.RowVersion, rowVersion))
            {
                return Result<Customer>.Conflict();
            }

            if (customer.Status == status)
            {
                return Result<Customer>.Success(customer);
            }

            customer.Status = status;
            return await UpdateAsync(customer, cancellationToken).ConfigureAwait(false);
        }
    }
}
