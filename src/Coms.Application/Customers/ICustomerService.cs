using System.Threading;
using System.Threading.Tasks;
using Coms.Domain.Common;
using Coms.Domain.Customers;

namespace Coms.Application.Customers
{
    public interface ICustomerService
    {
        Task<Customer?> GetAsync(int id, CancellationToken cancellationToken = default);

        Task<Customer?> GetByNumberAsync(string customerNumber, CancellationToken cancellationToken = default);

        Task<PagedResult<Customer>> SearchAsync(CustomerFilter filter, PagedRequest paging, CancellationToken cancellationToken = default);

        Task<CustomerBalance?> GetBalanceAsync(int id, CancellationToken cancellationToken = default);

        /// <summary>Validates and inserts. On success the customer carries its new Id and number.</summary>
        Task<Result<Customer>> CreateAsync(Customer customer, CancellationToken cancellationToken = default);

        /// <summary>Validates and updates. Fails with Conflict when the RowVersion is stale.</summary>
        Task<Result<Customer>> UpdateAsync(Customer customer, CancellationToken cancellationToken = default);

        Task<Result<Customer>> ChangeStatusAsync(int id, CustomerStatus status, byte[] rowVersion, CancellationToken cancellationToken = default);
    }
}
