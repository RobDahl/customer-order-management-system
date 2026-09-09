using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Coms.Domain.Common;

namespace Coms.Data.Connections
{
    /// <summary>
    /// One connection, and optionally one transaction, shared by every
    /// repository taking part in an operation. Scoped per web request or
    /// per desktop action; disposed when the operation ends, rolling back
    /// anything not committed.
    /// </summary>
    public interface IDbSession : IUnitOfWork, IDisposable
    {
        Task<IDbConnection> GetConnectionAsync(CancellationToken cancellationToken = default);

        IDbTransaction? Transaction { get; }
    }
}
