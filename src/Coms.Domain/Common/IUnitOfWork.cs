using System.Threading;
using System.Threading.Tasks;

namespace Coms.Domain.Common
{
    /// <summary>
    /// A transaction boundary over the repositories used by one operation.
    /// Repositories that share a unit of work share its connection and
    /// transaction. The data layer supplies the implementation.
    /// </summary>
    public interface IUnitOfWork
    {
        bool HasActiveTransaction { get; }

        Task BeginAsync(CancellationToken cancellationToken = default);

        Task CommitAsync(CancellationToken cancellationToken = default);

        Task RollbackAsync(CancellationToken cancellationToken = default);
    }
}
