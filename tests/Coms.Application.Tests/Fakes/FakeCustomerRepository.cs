using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Coms.Domain.Common;
using Coms.Domain.Customers;

namespace Coms.Application.Tests.Fakes
{
    internal sealed class FakeCustomerRepository : ICustomerRepository
    {
        public List<Customer> Customers { get; } = new List<Customer>();

        public Dictionary<int, CustomerBalance> Balances { get; } = new Dictionary<int, CustomerBalance>();

        public Customer Add(Customer customer)
        {
            customer.Id = Customers.Count == 0 ? 1 : Customers.Max(c => c.Id) + 1;
            customer.CustomerNumber = "CUST-" + customer.Id.ToString("000000");
            customer.RowVersion = Versions.Next();
            Customers.Add(customer);
            return customer;
        }

        public Task<Customer?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Customers.FirstOrDefault(c => c.Id == id));
        }

        public Task<Customer?> GetByNumberAsync(string customerNumber, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Customers.FirstOrDefault(c => string.Equals(c.CustomerNumber, customerNumber, StringComparison.OrdinalIgnoreCase)));
        }

        public Task<PagedResult<Customer>> SearchAsync(CustomerFilter filter, PagedRequest paging, CancellationToken cancellationToken = default)
        {
            IEnumerable<Customer> query = Customers;

            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                query = query.Where(c => c.Name.StartsWith(filter.Search!, StringComparison.OrdinalIgnoreCase)
                                      || c.CustomerNumber.StartsWith(filter.Search!, StringComparison.OrdinalIgnoreCase));
            }

            if (filter.Status.HasValue)
            {
                query = query.Where(c => c.Status == filter.Status.Value);
            }

            List<Customer> all = query.OrderBy(c => c.Name).ToList();
            List<Customer> page = all.Skip(paging.Offset).Take(paging.PageSize).ToList();
            return Task.FromResult(new PagedResult<Customer>(page, all.Count, paging.Page, paging.PageSize));
        }

        public Task InsertAsync(Customer customer, string userName, CancellationToken cancellationToken = default)
        {
            Add(customer);
            customer.CreatedBy = userName;
            customer.UpdatedBy = userName;
            customer.CreatedAtUtc = customer.UpdatedAtUtc = DateTime.UtcNow;
            return Task.CompletedTask;
        }

        public Task<bool> UpdateAsync(Customer customer, string userName, CancellationToken cancellationToken = default)
        {
            Customer? stored = Customers.FirstOrDefault(c => c.Id == customer.Id);
            if (stored == null || !Versions.Match(stored.RowVersion, customer.RowVersion))
            {
                return Task.FromResult(false);
            }

            if (!ReferenceEquals(stored, customer))
            {
                Customers.Remove(stored);
                Customers.Add(customer);
            }

            customer.RowVersion = Versions.Next();
            customer.UpdatedBy = userName;
            customer.UpdatedAtUtc = DateTime.UtcNow;
            return Task.FromResult(true);
        }

        public Task<CustomerBalance?> GetBalanceAsync(int customerId, CancellationToken cancellationToken = default)
        {
            Balances.TryGetValue(customerId, out CustomerBalance? balance);
            return Task.FromResult(balance);
        }
    }
}
