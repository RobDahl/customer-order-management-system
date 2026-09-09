using System.Threading;
using System.Threading.Tasks;
using Coms.Domain.Common;

namespace Coms.Domain.Customers
{
    public interface ICustomerRepository
    {
        Task<Customer?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

        Task<Customer?> GetByNumberAsync(string customerNumber, CancellationToken cancellationToken = default);

        Task<PagedResult<Customer>> SearchAsync(CustomerFilter filter, PagedRequest paging, CancellationToken cancellationToken = default);

        /// <summary>Inserts the customer, assigning Id, CustomerNumber, audit columns and RowVersion.</summary>
        Task InsertAsync(Customer customer, string userName, CancellationToken cancellationToken = default);

        /// <summary>Updates the customer. Returns false when the RowVersion is stale.</summary>
        Task<bool> UpdateAsync(Customer customer, string userName, CancellationToken cancellationToken = default);

        Task<CustomerBalance?> GetBalanceAsync(int customerId, CancellationToken cancellationToken = default);
    }
}
